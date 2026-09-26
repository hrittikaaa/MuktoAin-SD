using MuktoAin.Domain.Enums;

namespace MuktoAin.Application.DTOs;

public record QueueItemDto(
    int DocumentId,
    int CaseId,
    string CaseTitle,
    string CategoryName,
    string DistrictName,
    DocumentStatus Status,
    bool CitizenEdited,
    int VersionNo,
    string? ClaimedBy,       // claiming lawyer's bar number (admin-facing; not shown to other lawyers)
    DateTime WaitingSince,   // sent to review (SubmittedForReviewAt), else CreatedAt for older rows
    DateTime? ClaimedAt,
    bool CanOpen,            // false when another lawyer's claim is still active
    bool IsClaimed = false,  // any lawyer holds an active claim on it
    bool IsMine = false,     // the requesting lawyer holds it
    string CategoryNameBn = "" // CategoryName is English; this is the Bangla name
);

// AUD-8: queue paging envelope — TotalCount is the FULL filtered pool size
// (for the pager), Items is the current page slice only.
// FieldFallback: "MyField" was asked for but the lawyer's Specialization is
// blank or matches no category, so the full pool was returned instead.
// Page: the requested page clamped to the range. PoolCount: every document
// awaiting review, whatever the filter (the "Pending" KPI).
public record QueuePageDto(
    int TotalCount,
    IReadOnlyList<QueueItemDto> Items,
    bool FieldFallback = false,
    int Page = 1,
    int PoolCount = 0
);

public record ReviewWorkspaceDto(
    int DocumentId,
    int CaseId,
    string CaseTitle,
    string CategoryName,
    string DistrictName,
    string CitizenNarrative, // decrypted case description (PII — session-scoped)
    IReadOnlyList<CitedSectionDto> Citations,
    string OriginalDraft,     // ContentDraft — immutable
    string? CitizenEditedDraft, // ContentFinal if CitizenEdited
    int VersionNo,
    bool CitizenEdited,
    bool IsClaimedByMe = true, // false = read-only preview (no narrative, no decision form)
    string CategoryNameBn = ""
);

public record SubmitReviewDto(
    int DocumentId,
    int LawyerProfileId,
    ReviewDecision Decision,
    string Comments,          // MANDATORY for every decision; rejection shows to citizen
    string? EditedContent    // required when Decision == EditedApproved
);

// "What did I review" history row -- one per LawyerReview, newest first.
public record ReviewHistoryItemDto(
    int ReviewId,
    int DocumentId,
    int CaseId,
    string CaseTitle,
    string CategoryName,
    string DistrictName,
    ReviewDecision Decision,
    string Comments,
    DateTime ReviewedAt,
    int VersionNo,      // version decided on (snapshot; current version for older reviews)
    string DocumentText, // text as it stood after the decision (snapshot; current text for older reviews)
    string CategoryNameBn = ""
);

// One page of a lawyer's review history. TotalCount is the full filtered
// count (for the pager); Page is the requested page clamped to the range.
public record HistoryPageDto(
    int TotalCount,
    int Page,
    IReadOnlyList<ReviewHistoryItemDto> Items
);
