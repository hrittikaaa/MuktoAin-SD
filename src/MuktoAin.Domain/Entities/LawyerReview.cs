using MuktoAin.Domain.Enums;

namespace MuktoAin.Domain.Entities;

public class LawyerReview
{
    public int ReviewId { get; set; }

    // Reaches Case solely through Document (design.md 2.5)
    public int DocumentId { get; set; }
    public GeneratedDocument Document { get; set; } = null!;

    public int LawyerProfileId { get; set; }
    public LawyerProfile LawyerProfile { get; set; } = null!;

    public ReviewDecision Decision { get; set; }
    public string Comments { get; set; } = string.Empty;
    public DateTime ReviewedAt { get; set; }

    // Snapshot of the document when the decision was made (#17,
    // scripts/21_review_timing_and_snapshot.sql). Null on older reviews.
    public int? ReviewedVersionNo { get; set; }
    public string? ReviewedContent { get; set; }
}
