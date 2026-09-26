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

    Task SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken ct = default);
}
