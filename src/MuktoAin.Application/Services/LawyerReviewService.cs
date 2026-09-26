using System.Linq.Expressions;
using System.Text.RegularExpressions;
using MuktoAin.Application.DTOs;
using MuktoAin.Domain.Common;
using MuktoAin.Domain.Entities;
using MuktoAin.Domain.Enums;
using MuktoAin.Domain.Interfaces;
using MuktoAin.Domain.Interfaces.Repositories;

namespace MuktoAin.Application.Services;

// FR-13/14/23: shared review pool (with an optional "My field" filter by
// specialization) and a claim-based optimistic lock (opening a doc claims
// it; one active claim per lawyer, #10). A claim can be released, and lapses
// after ClaimTtl so an abandoned document returns to the pool (#9), decisions
// with mandatory comments. Rejection reason flows to the citizen's case page and
// (via the chat return link) into the salvage conversation.
public class LawyerReviewService
{
    private readonly IRepository<GeneratedDocument> _docRepo;
    private readonly IRepository<LawyerReview> _reviewRepo;
    private readonly IRepository<LawyerProfile> _profileRepo;
    private readonly ICaseRepository _caseRepo;
    private readonly IRepository<CaseCategory> _categoryRepo;
    private readonly IRepository<District> _districtRepo;
    private readonly IRepository<CaseActReference> _refRepo;
    private readonly IRepository<ActSection> _sectionRepo;
    private readonly IRepository<Act> _actRepo;
    private readonly IEncryptionService _encryptionService;
    private readonly CaseService _caseService;
    private readonly IRepository<Notification> _notificationRepo;

    public LawyerReviewService(
        IRepository<GeneratedDocument> docRepo,
        IRepository<LawyerReview> reviewRepo,
        IRepository<LawyerProfile> profileRepo,
        ICaseRepository caseRepo,
        IRepository<CaseCategory> categoryRepo,
        IRepository<District> districtRepo,
        IRepository<CaseActReference> refRepo,
        IRepository<ActSection> sectionRepo,
        IRepository<Act> actRepo,
        IEncryptionService encryptionService,
        CaseService caseService,
        IRepository<Notification> notificationRepo)
    {
        _docRepo = docRepo;
        _reviewRepo = reviewRepo;
        _profileRepo = profileRepo;
        _caseRepo = caseRepo;
        _categoryRepo = categoryRepo;
        _districtRepo = districtRepo;
        _refRepo = refRepo;
        _sectionRepo = sectionRepo;
        _actRepo = actRepo;
        _encryptionService = encryptionService;
        _caseService = caseService;
        _notificationRepo = notificationRepo;
    }

    // Queue = documents in UnderReview, oldest-first (SLA age shown by the view).
    // filter: "All" (default) | "Unclaimed" | "Mine" | "MyField". CanOpen
    // allows re-entry into the lawyer's OWN claimed doc (ClaimAsync
    // auto-allows same lawyer).
    // MyField: documents the lawyer can open whose case category matches their
    // Specialization (same keyword rule as LawyerQueueNotifier). A blank or
    // unmatched specialization falls back to the full pool with FieldFallback set.
    // AUD-8: paged — the page slice is taken BEFORE the expensive per-document
    // enrichment loop so a large backlog enriches only the visible page.
    // How long a claim holds a document before another lawyer may take it over.
    // The holder keeps it until someone else does (or it is decided/released).
    public static readonly TimeSpan ClaimTtl = TimeSpan.FromHours(24);

    public async Task<QueuePageDto> GetQueueAsync(
        int? lawyerProfileId = null, string? filter = "All", int page = 1, int pageSize = 20)
    {
        var cutoff = DateTime.UtcNow - ClaimTtl;
        // The pool (and the claim filters) are selected in the database, not by
        // loading every document ever generated. A lapsed claim counts as unclaimed.
        Expression<Func<GeneratedDocument, bool>> pool = filter switch
        {
            "Unclaimed" => d => d.Status == DocumentStatus.UnderReview
                && (d.AssignedLawyerProfileId == null || d.ClaimedAt == null || d.ClaimedAt < cutoff),
            "Mine" when lawyerProfileId.HasValue =>
                d => d.Status == DocumentStatus.UnderReview && d.AssignedLawyerProfileId == lawyerProfileId,
            _ => d => d.Status == DocumentStatus.UnderReview
        };
        IEnumerable<GeneratedDocument> docs = await _docRepo.FindAsync(pool);
        var fieldFallback = false;

        if (filter == "MyField" && lawyerProfileId.HasValue)
        {
            var profile = await _profileRepo.GetByIdAsync(lawyerProfileId.Value);
            var specialization = profile?.Specialization;
            if (LawyerQueueNotifier.MatchesAnyCategory(specialization))
            {
                // Only the cases behind the pooled documents, not every case.
                var caseIds = docs.Select(d => d.CaseId).Distinct().ToList();
                var categoryByCase = (await _caseRepo.FindAsync(c => caseIds.Contains(c.CaseId)))
                    .ToDictionary(c => c.CaseId, c => c.CategoryId);
                docs = docs.Where(d =>
                    (!IsActiveClaim(d, cutoff) || d.AssignedLawyerProfileId == lawyerProfileId.Value)
                    && categoryByCase.TryGetValue(d.CaseId, out var categoryId)
                    && LawyerQueueNotifier.MatchesCategory(specialization, categoryId));
            }
            else
            {
                fieldFallback = true;
            }
        }

        // Oldest wait first: time since the citizen sent it (#16).
        var ordered = docs.OrderBy(d => d.SubmittedForReviewAt ?? d.CreatedAt).ToList();
        var totalCount = ordered.Count;
        // Clamp before slicing, so a page past the end shows the last page's
        // items rather than an empty table labelled as the last page (#14).
        var totalPages = Math.Max((int)Math.Ceiling(totalCount / (double)pageSize), 1);
        page = Math.Clamp(page, 1, totalPages);
        var poolCount = await _docRepo.CountAsync(d => d.Status == DocumentStatus.UnderReview);

        var result = new List<QueueItemDto>();
        foreach (var d in ordered.Skip((page - 1) * pageSize).Take(pageSize))
        {
            var c = await _caseRepo.GetWithDocumentsAsync(d.CaseId);
            if (c == null) continue;
            var category = await _categoryRepo.GetByIdAsync(c.CategoryId);
            var district = await _districtRepo.GetByIdAsync(c.DistrictId);
            var claimActive = IsActiveClaim(d, cutoff);
            var isMine = lawyerProfileId.HasValue && d.AssignedLawyerProfileId == lawyerProfileId;
            string? claimedBy = null;
            if (claimActive && d.AssignedLawyerProfileId is int holderId)
            {
                var p = await _profileRepo.GetByIdAsync(holderId);
                claimedBy = p?.BarRegistrationNumber; // admin-safe identifier
            }
            result.Add(new QueueItemDto(
                d.DocumentId,
                d.CaseId,
                SafeDecrypt(c.Title),
                category?.Name ?? "",
                district?.Name ?? "",
                d.Status,
                d.CitizenEdited,
                d.VersionNo,
                claimedBy,
                d.SubmittedForReviewAt ?? d.CreatedAt,
                d.ClaimedAt,
                CanOpen: !claimActive || isMine,
                IsClaimed: claimActive || isMine,
                IsMine: isMine));
        }
        return new QueuePageDto(totalCount, result, fieldFallback, page, poolCount);
    }

    // A claim is active while it is younger than ClaimTtl. Claims written
    // without a ClaimedAt (old rows) are treated as lapsed.
    private static bool IsActiveClaim(GeneratedDocument d, DateTime cutoff) =>
        d.AssignedLawyerProfileId.HasValue && d.ClaimedAt.HasValue && d.ClaimedAt >= cutoff;

    // Claim = optimistic lock. Returns false if another lawyer holds an active
    // claim on it, or this lawyer already has a different active review (#10;
    // see GetOtherActiveClaimAsync). Re-opening one's own claim renews it, and
    // a lapsed claim of another lawyer can be taken over (#9).
    public async Task<bool> ClaimAsync(int documentId, int lawyerProfileId)
    {
        var d = await _docRepo.GetByIdAsync(documentId);
        if (d == null || d.Status != DocumentStatus.UnderReview) return false;
        var cutoff = DateTime.UtcNow - ClaimTtl;
        if (d.AssignedLawyerProfileId != lawyerProfileId)
        {
            if (IsActiveClaim(d, cutoff)) return false;
            if (await GetOtherActiveClaimAsync(lawyerProfileId, documentId) != null) return false;
        }

        d.AssignedLawyerProfileId = lawyerProfileId;
        d.ClaimedAt = DateTime.UtcNow;
        try
        {
            await _docRepo.SaveChangesAsync();
            return true;
        }
        catch (ConcurrencyConflictException)
        {
            return false; // another lawyer claimed it first (AUD-4)
        }
    }

    // The lawyer's active claim on a document other than `exceptDocumentId`,
    // if any -- the review they must finish or release before taking another.
    public async Task<int?> GetOtherActiveClaimAsync(int lawyerProfileId, int exceptDocumentId)
    {
        var cutoff = DateTime.UtcNow - ClaimTtl;
        var held = await _docRepo.FindAsync(d => d.Status == DocumentStatus.UnderReview
            && d.AssignedLawyerProfileId == lawyerProfileId
            && d.DocumentId != exceptDocumentId
            && d.ClaimedAt != null && d.ClaimedAt >= cutoff);
        return held?.OrderBy(d => d.ClaimedAt).Select(d => (int?)d.DocumentId).FirstOrDefault();
    }

    // Hands a claimed document back to the pool (#9). Only the holder can
    // release, and only while it is still under review.
    public async Task<bool> ReleaseAsync(int documentId, int lawyerProfileId)
    {
        var d = await _docRepo.GetByIdAsync(documentId);
        if (d == null || d.Status != DocumentStatus.UnderReview
            || d.AssignedLawyerProfileId != lawyerProfileId) return false;

        d.AssignedLawyerProfileId = null;
        d.ClaimedAt = null;
        try
        {
            await _docRepo.SaveChangesAsync();
            return true;
        }
        catch (ConcurrencyConflictException)
        {
            return false;
        }
    }

    // The workspace carries the decrypted citizen narrative, so it opens only
    // for a document under review that this lawyer has claimed (Claim first).
    public async Task<ReviewWorkspaceDto?> GetForReviewAsync(int documentId, int lawyerProfileId)
    {
        var d = await _docRepo.GetByIdAsync(documentId);
        if (d == null || d.Status != DocumentStatus.UnderReview
            || d.AssignedLawyerProfileId != lawyerProfileId) return null;
        return await BuildWorkspaceAsync(d, claimedByMe: true);
    }

    // Read-only look at a document before claiming it: the draft(s) and the
    // cited sections, but not the citizen narrative, which stays with the
    // claim holder (#2). Null when the document is not under review or another
    // lawyer's claim on it is still active.
    public async Task<ReviewWorkspaceDto?> GetPreviewAsync(int documentId, int lawyerProfileId)
    {
        var d = await _docRepo.GetByIdAsync(documentId);
        if (d == null || d.Status != DocumentStatus.UnderReview) return null;
        if (d.AssignedLawyerProfileId != lawyerProfileId
            && IsActiveClaim(d, DateTime.UtcNow - ClaimTtl)) return null;
        return await BuildWorkspaceAsync(d, claimedByMe: false);
    }

    private async Task<ReviewWorkspaceDto?> BuildWorkspaceAsync(GeneratedDocument d, bool claimedByMe)
    {
        var c = await _caseRepo.GetWithDocumentsAsync(d.CaseId);
        if (c == null) return null;

        var category = await _categoryRepo.GetByIdAsync(c.CategoryId);
        var district = await _districtRepo.GetByIdAsync(c.DistrictId);

        var citations = new List<CitedSectionDto>();
        var refs = (await _refRepo.GetAllAsync()).Where(r => r.CaseId == d.CaseId);
        foreach (var r in refs)
        {
            var s = await _sectionRepo.GetByIdAsync(r.SectionId);
            var a = s != null ? await _actRepo.GetByIdAsync(s.ActId) : null;
            citations.Add(new CitedSectionDto(
                r.SectionId,
                a?.Title ?? "",
                s?.SectionNumber ?? "",
                s?.SectionText ?? "",
                (float)r.RelevanceScore,
                r.RetrievalMethod.ToString(),
                a?.ActNumber ?? "",
                a?.Year ?? 0));
        }

        return new ReviewWorkspaceDto(
            d.DocumentId,
            d.CaseId,
            SafeDecrypt(c.Title),
            category?.Name ?? "",
            district?.Name ?? "",
            claimedByMe ? SafeDecrypt(c.Description) : string.Empty,
            citations,
            d.ContentDraft,
            d.CitizenEdited ? d.ContentFinal : null,
            d.VersionNo,
            d.CitizenEdited,
            claimedByMe);
    }

    public async Task<bool> SubmitReviewAsync(SubmitReviewDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Comments)) return false; // mandatory
        if (dto.Decision == ReviewDecision.EditedApproved
            && string.IsNullOrWhiteSpace(dto.EditedContent)) return false;

        var d = await _docRepo.GetByIdAsync(dto.DocumentId);
        if (d == null || d.Status != DocumentStatus.UnderReview) return false;
        // A decision needs this lawyer's own claim -- no implicit claim here.
        if (d.AssignedLawyerProfileId != dto.LawyerProfileId) return false;

        // The case must be under review for any decision (#11). Approval moves
        // it to Finalized; a rejection leaves it UnderReview (UnderReview +
        // Rejected document = the citizen edit & resubmit loop). Checked before
        // anything is changed, so a refused transition writes nothing.
        if (dto.Decision == ReviewDecision.Rejected)
        {
            var c = await _caseRepo.GetByIdAsync(d.CaseId);
            if (c == null || c.Status != CaseStatus.UnderReview) return false;
        }
        else if (!await _caseService.ApplyStatusTransitionAsync(d.CaseId, CaseStatus.Finalized))
        {
            return false;
        }

        ApplyDocumentDecision(d,
            dto.Decision == ReviewDecision.Rejected ? DocumentStatus.Rejected : DocumentStatus.Approved,
            dto.Decision == ReviewDecision.EditedApproved ? dto.EditedContent : null);

        await _reviewRepo.AddAsync(new LawyerReview
        {
            DocumentId = dto.DocumentId,
            LawyerProfileId = dto.LawyerProfileId,
            Decision = dto.Decision,
            Comments = dto.Comments,
            ReviewedAt = DateTime.UtcNow,
            // What the decision was made on, so history doesn't follow later edits (#17).
            ReviewedVersionNo = d.VersionNo,
            ReviewedContent = d.ContentFinal ?? d.ContentDraft
        });

        try
        {
            // Review row, claim, document decision and case status all share
            // the request's DbContext, so this one save commits them together:
            // a rowversion conflict on the document or case persists none (AUD-4).
            await _reviewRepo.SaveChangesAsync();
        }
        catch (ConcurrencyConflictException)
        {
            return false;
        }

        var c2 = await _caseRepo.GetByIdAsync(d.CaseId);
        if (c2 is { UserId: not null, IsAnonymous: false })
        {
            try
            {
                await _notificationRepo.AddAsync(new Notification
                {
                    UserId = c2.UserId.Value,
                    Type = NotificationType.DocumentDecided,
                    RelatedCaseId = c2.CaseId,
                    RelatedDocumentId = d.DocumentId,
                    CreatedAt = DateTime.UtcNow
                });
                await _notificationRepo.SaveChangesAsync();
            }
            catch
            {
                // A notification-write failure must not fail the review submission it's attached to.
            }
        }
        return true;
    }

    // History = every decision this lawyer has submitted, newest first.
    // decisionFilter: null/"All" | "Approved" | "EditedApproved" | "Rejected".
    // from/to bound ReviewedAt (inclusive) when given.
    public async Task<IReadOnlyList<ReviewHistoryItemDto>> GetHistoryAsync(
        int lawyerProfileId, string? decisionFilter = null, DateTime? from = null, DateTime? to = null)
    {
        var reviews = await FindReviewsAsync(lawyerProfileId, decisionFilter, from, to);
        return await EnrichHistoryAsync(reviews.OrderByDescending(r => r.ReviewedAt));
    }

    // One page of history. sort: "date_desc" (default) | "date_asc" | "case_asc".
    // Date sorts slice the page BEFORE the per-review enrichment (as the queue
    // does, AUD-8). Case titles are encrypted, so "case_asc" has to decrypt
    // every matching row to order them.
    public async Task<HistoryPageDto> GetHistoryPageAsync(
        int lawyerProfileId, string? decisionFilter, DateTime? from, DateTime? to,
        string? sort, int page, int pageSize)
    {
        var reviews = await FindReviewsAsync(lawyerProfileId, decisionFilter, from, to);
        var totalPages = Math.Max((int)Math.Ceiling(reviews.Count / (double)pageSize), 1);
        page = Math.Clamp(page, 1, totalPages);
        var skip = (page - 1) * pageSize;

        IReadOnlyList<ReviewHistoryItemDto> items;
        if (sort == "case_asc")
        {
            var all = await EnrichHistoryAsync(reviews.OrderByDescending(r => r.ReviewedAt));
            items = all.OrderBy(h => h.CaseTitle, StringComparer.OrdinalIgnoreCase)
                .Skip(skip).Take(pageSize).ToList();
        }
        else
        {
            var ordered = sort == "date_asc"
                ? reviews.OrderBy(r => r.ReviewedAt)
                : reviews.OrderByDescending(r => r.ReviewedAt);
            items = await EnrichHistoryAsync(ordered.Skip(skip).Take(pageSize));
        }
        return new HistoryPageDto(reviews.Count, page, items);
    }

    private async Task<IReadOnlyList<LawyerReview>> FindReviewsAsync(
        int lawyerProfileId, string? decisionFilter, DateTime? from, DateTime? to)
    {
        ReviewDecision? decision = !string.IsNullOrWhiteSpace(decisionFilter) && decisionFilter != "All"
            && Enum.TryParse<ReviewDecision>(decisionFilter, out var parsed) ? parsed : null;

        return await _reviewRepo.FindAsync(r => r.LawyerProfileId == lawyerProfileId
            && (decision == null || r.Decision == decision)
            && (from == null || r.ReviewedAt >= from)
            && (to == null || r.ReviewedAt <= to));
    }

    private async Task<IReadOnlyList<ReviewHistoryItemDto>> EnrichHistoryAsync(IEnumerable<LawyerReview> reviews)
    {
        var result = new List<ReviewHistoryItemDto>();
        foreach (var r in reviews)
        {
            var d = await _docRepo.GetByIdAsync(r.DocumentId);
            if (d == null) continue;
            var c = await _caseRepo.GetByIdAsync(d.CaseId);
            if (c == null) continue;
            var category = await _categoryRepo.GetByIdAsync(c.CategoryId);
            var district = await _districtRepo.GetByIdAsync(c.DistrictId);

            result.Add(new ReviewHistoryItemDto(
                r.ReviewId, r.DocumentId, c.CaseId, SafeDecrypt(c.Title),
                category?.Name ?? "", district?.Name ?? "", r.Decision, r.Comments, r.ReviewedAt,
                r.ReviewedVersionNo ?? d.VersionNo,
                r.ReviewedContent ?? d.ContentFinal ?? d.ContentDraft));
        }
        return result;
    }

    private static void ApplyDocumentDecision(GeneratedDocument d, DocumentStatus status, string? edited)
    {
        // EditedApproved -> ContentFinal = the lawyer's edit. Approved -> the
        // version under review is published unchanged: the citizen's edit when
        // there is one (same rule as GetForReviewAsync), else the AI draft.
        // Saved by the caller together with the review (see SubmitReviewAsync).
        d.Status = status;
        if (edited != null)
            d.ContentFinal = edited;
        else if (status == DocumentStatus.Approved)
            d.ContentFinal = d.CitizenEdited && d.ContentFinal != null ? d.ContentFinal : d.ContentDraft;
    }

    // Case.Title/Description are field-level-encrypted PII (S-1.7). Decrypt
    // failures fall into two very different buckets:
    //   - genuine legacy plaintext rows (predate encryption): Decrypt throws
    //     immediately on the non-base64url text, and `value` IS the correct
    //     human-readable title -- must return it as-is.
    //   - orphaned ciphertext (e.g. a rotated/lost Data Protection key ring --
    //     see AUD-2): Decrypt throws too, but `value` is an opaque encrypted
    //     blob. Returning it verbatim used to leak raw ciphertext straight
    //     into the lawyer dashboard ("doc title coming crypted"). Detect that
    //     shape and show a safe placeholder instead of the blob.
    private static readonly Regex CiphertextShape = new(@"^[A-Za-z0-9\-_]{40,}$", RegexOptions.Compiled);
    private const string UndecryptablePlaceholder = "শিরোনাম উদ্ধার করা যায়নি / Title unavailable (decryption failed)";

    private string SafeDecrypt(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        try { return _encryptionService.Decrypt(value); }
        catch { return CiphertextShape.IsMatch(value) ? UndecryptablePlaceholder : value; }
    }
}
