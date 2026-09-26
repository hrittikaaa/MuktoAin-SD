namespace MuktoAin.Domain.Interfaces.Services;

public interface IEmailService
{
    Task SendCaseSubmittedAsync(
        string toEmail,
        string caseTitle,
        string? trackingCode,
        int caseId,
        string language = "bn",
        CancellationToken ct = default);

    Task SendReviewDecisionAsync(
        string toEmail,
        string caseTitle,
        string decisionStatus,
        string? comments,
        string? trackingCode,
        int caseId,
        string language = "bn",
        CancellationToken ct = default);

    Task SendLawyerNewCaseQueuedAsync(
        string lawyerEmail,
        string lawyerName,
        string caseTitle,
        int caseId,
        int documentId,
        string categoryName,
        string districtName,
        string language = "bn",
        CancellationToken ct = default);

    Task SendDocumentSentToLawyerAsync(
        string toEmail,
        string caseTitle,
        string? trackingCode,
        int caseId,
        string language = "bn",
        CancellationToken ct = default);

    Task SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken ct = default);
}
