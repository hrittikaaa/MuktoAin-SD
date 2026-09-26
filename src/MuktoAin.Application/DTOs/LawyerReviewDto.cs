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
    DateTime CreatedAt,
    DateTime? ClaimedAt,
    bool CanOpen,            // false when claimed by another lawyer
    bool IsClaimed = false,  // any lawyer holds it
    bool IsMine = false      // the requesting lawyer holds it
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
    bool CitizenEdited
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
    int VersionNo,
    string DocumentText // ContentFinal if approved, else ContentDraft (what was rejected)
);

// One page of a lawyer's review history. TotalCount is the full filtered
// count (for the pager); Page is the requested page clamped to the range.
public record HistoryPageDto(
    int TotalCount,
    int Page,
    IReadOnlyList<ReviewHistoryItemDto> Items
);
