using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using MuktoAin.Application.DTOs;
using MuktoAin.Application.Services;
using MuktoAin.Domain.Entities;
using MuktoAin.Domain.Enums;
using MuktoAin.Domain.Interfaces.Repositories;
using MuktoAin.Web.ViewModels;

namespace MuktoAin.Web.Controllers;

// FR-13/14/15/23. Review is LAWYER-ONLY (admin manages, never reviews).
[Authorize(Roles = "Lawyer")]
public class LawyerController : Controller
{
    private readonly LawyerReviewService _reviewService;
    private readonly LawyerVerificationService _verificationService;
    private readonly PaymentService _paymentService;
    private readonly IRepository<LawyerProfile> _profileRepo;
    private readonly UserManager<User> _userManager;

    public LawyerController(
        LawyerReviewService reviewService,
        LawyerVerificationService verificationService,
        PaymentService paymentService,
        IRepository<LawyerProfile> profileRepo,
        UserManager<User> userManager)
    {
        _reviewService = reviewService;
        _verificationService = verificationService;
        _paymentService = paymentService;
        _profileRepo = profileRepo;
        _userManager = userManager;
    }

    private async Task<LawyerProfile?> MyProfileAsync()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(idStr, out var userId)) return null;
        var all = await _profileRepo.GetAllAsync();
        return all.FirstOrDefault(p => p.UserId == userId);
    }

    // Unverified lawyers land here instead of the queue.
    [HttpGet]
    public async Task<IActionResult> Status()
    {
        var profile = await MyProfileAsync();
        if (profile == null) return NotFound();
        var user = await _userManager.FindByIdAsync(profile.UserId.ToString());

        var vm = new LawyerStatusViewModel
        {
            LawyerName = user?.FullName ?? "",
            BarRegistrationNumber = profile.BarRegistrationNumber,
            Specialization = profile.Specialization ?? "",
            Status = profile.VerificationStatus.ToString(),
            RejectionReason = profile.RejectionReason,
            SubmittedAt = profile.VerifiedAt ?? DateTime.UtcNow // display only
        };
        return View(vm);
    }

    // Rejected lawyers resubmit their bar number from the Status page.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resubmit(LawyerStatusViewModel vm)
    {
        var profile = await MyProfileAsync();
        if (profile == null) return NotFound();
        if (profile.VerificationStatus != VerificationStatus.Rejected) return Forbid();
        if (string.IsNullOrWhiteSpace(vm.BarRegistrationNumber))
        {
            // ModelState does not survive the redirect, so surface it as a toast.
            TempData["Error"] = "বার নম্বর আবশ্যক।";
            TempData["ErrorEn"] = "Bar number required.";
            return RedirectToAction(nameof(Status));
        }

        profile.BarRegistrationNumber = vm.BarRegistrationNumber;
        if (!string.IsNullOrWhiteSpace(vm.Specialization))
            profile.Specialization = vm.Specialization;
        profile.VerificationStatus = VerificationStatus.Pending;
        await _profileRepo.SaveChangesAsync();

        TempData["Success"] = "আবেদন পুনরায় জমা হয়েছে — ২৪–৪৮ ঘণ্টার মধ্যে যাচাই হবে।";
        TempData["SuccessEn"] = "Application resubmitted — verification typically takes 24–48h.";
        return RedirectToAction(nameof(Status));
    }

    private const int QueuePageSize = 20;

    // Queue: documents in the pool, oldest-first (SLA). Filter chips:
    // All (default) · Unclaimed · Mine (my claimed docs, re-enterable) ·
    // MyField (cases matching my specialization).
    [HttpGet]
    public async Task<IActionResult> Queue(string? filter, int page = 1)
    {
        var profile = await MyProfileAsync();
        if (profile == null || profile.VerificationStatus != VerificationStatus.Approved)
            return RedirectToAction(nameof(Status));

        var queue = await _reviewService.GetQueueAsync(profile.LawyerProfileId, filter, page, QueuePageSize);

        var totalPages = Math.Max((int)Math.Ceiling(queue.TotalCount / (double)QueuePageSize), 1);
        var vm = new LawyerQueueViewModel
        {
            LawyerName = (await _userManager.FindByIdAsync(profile.UserId.ToString()))?.FullName ?? "",
            BarRegistrationNumber = profile.BarRegistrationNumber,
            Specialization = profile.Specialization ?? "",
            PendingCount = queue.TotalCount, // KPI shows the full backlog, not the page
            ActiveFilter = filter ?? "All",
            FieldFallback = queue.FieldFallback,
            Page = Math.Max(1, Math.Min(page, totalPages)),
            PageSize = QueuePageSize,
            TotalCount = queue.TotalCount,
            Items = queue.Items.Select(q => new LawyerQueueItemViewModel
            {
                DocumentId = q.DocumentId,
                CaseId = q.CaseId,
                CaseTitle = q.CaseTitle,
                CategoryName = q.CategoryName,
                DistrictName = q.DistrictName,
                CitizenEdited = q.CitizenEdited,
                VersionNo = q.VersionNo,
                ClaimedBy = q.ClaimedBy,
                IsClaimed = q.IsClaimed,
                IsMine = q.IsMine,
                WaitingHours = (int)Math.Max(0, (DateTime.UtcNow - q.CreatedAt).TotalHours),
                CanOpen = q.CanOpen
            }).ToList()
        };
        return View(vm);
    }

    // Claim-on-open (optimistic lock) then straight into the workspace.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Claim(int documentId)
    {
        var profile = await MyProfileAsync();
        if (profile == null || profile.VerificationStatus != VerificationStatus.Approved)
            return RedirectToAction(nameof(Status));

        var ok = await _reviewService.ClaimAsync(documentId, profile.LawyerProfileId);
        if (!ok)
        {
            TempData["Error"] = "অন্য আইনজীবী এটি নিয়েছেন — সারিতে ফিরে যান।";
            TempData["ErrorEn"] = "Another lawyer claimed this — back to the queue.";
            return RedirectToAction(nameof(Queue));
        }
        return RedirectToAction(nameof(Review), new { id = documentId });
    }

    [HttpGet]
    public async Task<IActionResult> Review(int id)
    {
        var profile = await MyProfileAsync();
        if (profile == null || profile.VerificationStatus != VerificationStatus.Approved)
            return RedirectToAction(nameof(Status));

        return await ReviewWorkspaceAsync(id, profile.LawyerProfileId, posted: null);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitReview(LawyerReviewViewModel vm)
    {
        var profile = await MyProfileAsync();
        if (profile == null || profile.VerificationStatus != VerificationStatus.Approved)
            return RedirectToAction(nameof(Status));

        // Decision/Comments rules live on LawyerReviewViewModel (DataAnnotations).
        // An unknown decision is an error, never a silent default (#8).
        var isDecision = Enum.GetNames<ReviewDecision>().Contains(vm.Decision);
        if (!isDecision && ModelState.GetFieldValidationState(nameof(vm.Decision)) != ModelValidationState.Invalid)
            ModelState.AddModelError(nameof(vm.Decision),
                "সিদ্ধান্ত অবশ্যই Approved, EditedApproved অথবা Rejected হতে হবে / Decision must be Approved, EditedApproved or Rejected");
        if (vm.Decision == nameof(ReviewDecision.EditedApproved) && string.IsNullOrWhiteSpace(vm.EditedContent))
            ModelState.AddModelError(nameof(vm.EditedContent),
                "সম্পাদনাসহ অনুমোদনের জন্য সম্পাদিত পাঠ্য আবশ্যক / Edited text is required to approve with edits");

        // Re-show the workspace with the lawyer's own text and comment instead
        // of redirecting (a redirect reloads the document and loses them, #6).
        if (!ModelState.IsValid)
            return await ReviewWorkspaceAsync(vm.DocumentId, profile.LawyerProfileId, posted: vm);

        var decision = Enum.Parse<ReviewDecision>(vm.Decision);
        var ok = await _reviewService.SubmitReviewAsync(new SubmitReviewDto(
            vm.DocumentId,
            profile.LawyerProfileId,
            decision,
            vm.Comments.Trim(),
            decision == ReviewDecision.EditedApproved ? vm.EditedContent : null));

        if (!ok)
        {
            // Input was valid, so the document changed underneath (claim lost,
            // already decided, or a concurrent save) -- reload is correct here.
            TempData["Error"] = "পর্যালোচনা সংরক্ষণ হয়নি — নথিটি ইতিমধ্যে পরিবর্তিত হয়েছে। সারি থেকে আবার খুলুন।";
            TempData["ErrorEn"] = "Review not saved — the document changed in the meantime. Open it again from the queue.";
            return RedirectToAction(nameof(Review), new { id = vm.DocumentId });
        }

        TempData["Success"] = "পর্যালোচনা সম্পন্ন — পরবর্তী নথিতে যাচ্ছেন।";
        TempData["SuccessEn"] = "Review saved — advancing to the next document.";
        return RedirectToAction(nameof(Queue));
    }

    // Builds the review page from the lawyer's own active claim. `posted`
    // (an invalid submission) keeps the lawyer's decision, edited text and
    // comment on top of the freshly loaded workspace context.
    private async Task<IActionResult> ReviewWorkspaceAsync(int documentId, int lawyerProfileId, LawyerReviewViewModel? posted)
    {
        var ws = await _reviewService.GetForReviewAsync(documentId, lawyerProfileId);
        if (ws == null)
        {
            TempData["Error"] = "এই নথিটি আপনার নেওয়া পর্যালোচনাধীন নথি নয় — সারি থেকে খুলুন।";
            TempData["ErrorEn"] = "This document isn't under review in your name — open it from the queue.";
            return RedirectToAction(nameof(Queue));
        }

        var vm = new LawyerReviewViewModel
        {
            DocumentId = ws.DocumentId,
            CaseId = ws.CaseId,
            CaseTitle = ws.CaseTitle,
            CategoryName = ws.CategoryName,
            ContentDraft = ws.OriginalDraft,
            EditedContent = posted != null ? posted.EditedContent : ws.CitizenEditedDraft ?? ws.OriginalDraft,
            Decision = posted?.Decision ?? nameof(ReviewDecision.EditedApproved),
            Comments = posted?.Comments ?? string.Empty
        };
        // Context extras for the view
        ViewData["DistrictName"] = ws.DistrictName;
        ViewData["CitizenNarrative"] = ws.CitizenNarrative;
        ViewData["Citations"] = ws.Citations;
        ViewData["VersionNo"] = ws.VersionNo;
        ViewData["CitizenEdited"] = ws.CitizenEdited;
        return View(nameof(Review), vm);
    }

    private const int HistoryPageSize = 20;

    // "What did I review" — every decision this lawyer has submitted, newest
    // first. Filter chips: All/Approved/EditedApproved/Rejected + date range +
    // sort (date_desc default | date_asc | case_asc).
    [HttpGet]
    public async Task<IActionResult> History(string? decision, string? from, string? to, string? sort, int page = 1)
    {
        var profile = await MyProfileAsync();
        if (profile == null) return NotFound();

        // Dates are Dhaka calendar days (what the lawyer picked); ReviewedAt is
        // UTC, so convert the whole-day bounds. A reversed range is swapped.
        DateOnly? fromDay = BdTime.TryParseDay(from, out var f) ? f : null;
        DateOnly? toDay = BdTime.TryParseDay(to, out var t) ? t : null;
        if (fromDay > toDay)
        {
            (fromDay, toDay) = (toDay, fromDay);
            (from, to) = (to, from);
        }
        DateTime? fromDate = fromDay.HasValue ? BdTime.DayStartUtc(fromDay.Value) : null;
        DateTime? toDate = toDay.HasValue ? BdTime.DayEndUtc(toDay.Value) : null;

        var history = await _reviewService.GetHistoryAsync(profile.LawyerProfileId, decision, fromDate, toDate);
        // Service already returns newest-first; only re-sort for the other options.
        IEnumerable<ReviewHistoryItemDto> sorted = sort switch
        {
            "date_asc" => history.OrderBy(h => h.ReviewedAt),
            "case_asc" => history.OrderBy(h => h.CaseTitle, StringComparer.OrdinalIgnoreCase),
            _ => history
        };

        var vm = new LawyerHistoryViewModel
        {
            LawyerName = (await _userManager.FindByIdAsync(profile.UserId.ToString()))?.FullName ?? "",
            BarRegistrationNumber = profile.BarRegistrationNumber,
            ActiveFilter = decision ?? "All",
            FromDate = from,
            ToDate = to,
            Sort = sort ?? "date_desc",
            TotalCount = history.Count
        };
        vm.Page = Math.Max(1, Math.Min(page, Math.Max((int)Math.Ceiling(vm.TotalCount / (double)HistoryPageSize), 1)));
        vm.Items = sorted.Skip((vm.Page - 1) * HistoryPageSize).Take(HistoryPageSize)
            .Select(h => new LawyerHistoryItemViewModel
            {
                ReviewId = h.ReviewId,
                DocumentId = h.DocumentId,
                CaseId = h.CaseId,
                CaseTitle = h.CaseTitle,
                CategoryName = h.CategoryName,
                DistrictName = h.DistrictName,
                Decision = h.Decision.ToString(),
                Comments = h.Comments,
                ReviewedAt = h.ReviewedAt,
                VersionNo = h.VersionNo,
                DocumentText = h.DocumentText
            }).ToList();

        return View(vm);
    }

    // FR-24 (lawyer variant): balance + honorarium history, moved off the
    // shared Account/Profile page into its own lawyer-scoped route.
    [HttpGet]
    public async Task<IActionResult> Payments()
    {
        var profile = await MyProfileAsync();
        if (profile == null) return NotFound();

        var earnings = await _paymentService.GetLawyerEarningsAsync(profile.LawyerProfileId);
        var vm = new LawyerPaymentsViewModel
        {
            LawyerName = (await _userManager.FindByIdAsync(profile.UserId.ToString()))?.FullName ?? "",
            BarRegistrationNumber = profile.BarRegistrationNumber,
            Balance = earnings.Balance,
            History = earnings.History.Select(h => new EarningRowViewModel
            {
                PaymentOrderId = h.PaymentOrderId,
                CaseId = h.CaseId,
                Gross = h.Gross,
                Commission = h.Commission,
                Net = h.Net,
                PaidAt = h.PaidAt
            }).ToList()
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestPayout()
    {
        var profile = await MyProfileAsync();
        if (profile == null) return NotFound();

        var earnings = await _paymentService.GetLawyerEarningsAsync(profile.LawyerProfileId);
        if (earnings.Balance <= 0)
        {
            TempData["Error"] = "পরিশোধযোগ্য ব্যালেন্স নেই।";
            TempData["ErrorEn"] = "No payable balance.";
            return RedirectToAction(nameof(Payments));
        }
        await _paymentService.RequestPayoutAsync(profile.LawyerProfileId, earnings.Balance);
        TempData["Success"] = "পরিশোধের অনুরোধ জমা হয়েছে (স্যান্ডবক্স)।";
        TempData["SuccessEn"] = "Payout request submitted (sandbox).";
        return RedirectToAction(nameof(Payments));
    }
}
