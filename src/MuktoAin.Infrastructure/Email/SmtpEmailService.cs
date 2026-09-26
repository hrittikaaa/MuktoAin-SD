using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MuktoAin.Domain.Interfaces.Services;

namespace MuktoAin.Infrastructure.Email;

public class SmtpEmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(
        IOptions<EmailSettings> options,
        ILogger<SmtpEmailService> logger)
    {
        _settings = options?.Value ?? new EmailSettings();
        _logger = logger;
    }

    public async Task SendCaseSubmittedAsync(
        string toEmail,
        string caseTitle,
        string? trackingCode,
        int caseId,
        string language = "bn",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return;

        var isBangla = language != "en";
        var subject = isBangla
            ? $"[মুক্ত আইন] মামলা আবেদন গৃহীত হয়েছে — #{caseId}"
            : $"[MuktoAin] Case Application Submitted — #{caseId}";

        var baseUrl = string.IsNullOrWhiteSpace(_settings.AppBaseUrl)
            ? "https://localhost:7001"
            : _settings.AppBaseUrl.TrimEnd('/');

        var trackUrl = !string.IsNullOrEmpty(trackingCode)
            ? $"{baseUrl}/Case/Result?id={caseId}&code={trackingCode}"
            : $"{baseUrl}/Case/Result?id={caseId}";

        var body = isBangla
            ? $"""
            <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px; background-color: #ffffff;">
                <div style="background-color: #1a365d; color: #ffffff; padding: 16px 20px; border-radius: 6px 6px 0 0; text-align: center;">
                    <h2 style="margin: 0; font-size: 22px;">মুক্ত আইন (MuktoAin)</h2>
                    <p style="margin: 4px 0 0; font-size: 13px; opacity: 0.9;">আইনি সহায়তা ও খসড়া প্ল্যাটফর্ম</p>
                </div>
                <div style="padding: 20px;">
                    <h3 style="color: #2b6cb0; margin-top: 0;">আপনার মামলার খসড়া আবেদন সফলভাবে জমা হয়েছে</h3>
                    <p>প্রিয় নাগরিক,</p>
                    <p>আপনার <strong>"{WebUtility.HtmlEncode(caseTitle)}"</strong> সংক্রান্ত মামলার খসড়া নথি সিস্টেমে সংরক্ষিত হয়েছে এবং আইনজীবীর পর্যালোচনার জন্য অপেক্ষমাণ রয়েছে।</p>
                    
                    <div style="background-color: #f7fafc; border-left: 4px solid #3182ce; padding: 12px 16px; margin: 18px 0; border-radius: 4px;">
                        <p style="margin: 4px 0;"><strong>মামলা নম্বর:</strong> #{caseId}</p>
                        {(trackingCode != null ? $"<p style=\"margin: 4px 0;\"><strong>ট্র্যাকিং কোড:</strong> <code style=\"background: #edf2f7; padding: 2px 6px; border-radius: 4px;\">{trackingCode}</code></p>" : "")}
                        <p style="margin: 4px 0;"><strong>বর্তমান অবস্থা:</strong> আইনজীবীর পর্যালোচনার অপেক্ষায় (Submitted)</p>
                    </div>

                    <p style="margin: 20px 0; text-align: center;">
                        <a href="{trackUrl}" style="background-color: #d69e2e; color: #1a202c; font-weight: bold; padding: 12px 24px; text-decoration: none; border-radius: 6px; display: inline-block;">মামলার অগ্রগতি দেখুন</a>
                    </p>

                    <div style="background-color: #fffaf0; border: 1px solid #feebc8; border-radius: 4px; padding: 10px; margin-top: 20px; font-size: 12px; color: #744210;">
                        <strong>সতর্কতা:</strong> এটি একটি স্বয়ংক্রিয় বার্তা। এই ড্রাফটটি চূড়ান্তভাবে ডাউনলোড বা ব্যবহারের পূর্বে একজন যাচাইকৃত আইনজীবীর অনুমোদন বাধ্যতামূলক।
                    </div>
                </div>
                <div style="border-top: 1px solid #edf2f7; padding: 12px 20px; text-align: center; font-size: 11px; color: #718096;">
                    © {DateTime.UtcNow.Year} মুক্ত আইন (MuktoAin) — বাংলাদেশ
                </div>
            </div>
            """
            : $"""
            <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px; background-color: #ffffff;">
                <div style="background-color: #1a365d; color: #ffffff; padding: 16px 20px; border-radius: 6px 6px 0 0; text-align: center;">
                    <h2 style="margin: 0; font-size: 22px;">MuktoAin (মুক্ত আইন)</h2>
                    <p style="margin: 4px 0 0; font-size: 13px; opacity: 0.9;">Legal-Aid & Document Drafting Platform</p>
                </div>
                <div style="padding: 20px;">
                    <h3 style="color: #2b6cb0; margin-top: 0;">Your Case Draft Has Been Submitted</h3>
                    <p>Dear Citizen,</p>
                    <p>Your legal document draft for <strong>"{WebUtility.HtmlEncode(caseTitle)}"</strong> has been submitted and is currently awaiting review by a verified lawyer.</p>
                    
                    <div style="background-color: #f7fafc; border-left: 4px solid #3182ce; padding: 12px 16px; margin: 18px 0; border-radius: 4px;">
                        <p style="margin: 4px 0;"><strong>Case ID:</strong> #{caseId}</p>
                        {(trackingCode != null ? $"<p style=\"margin: 4px 0;\"><strong>Tracking Code:</strong> <code style=\"background: #edf2f7; padding: 2px 6px; border-radius: 4px;\">{trackingCode}</code></p>" : "")}
                        <p style="margin: 4px 0;"><strong>Status:</strong> Awaiting Lawyer Review (Submitted)</p>
                    </div>

                    <p style="margin: 20px 0; text-align: center;">
                        <a href="{trackUrl}" style="background-color: #d69e2e; color: #1a202c; font-weight: bold; padding: 12px 24px; text-decoration: none; border-radius: 6px; display: inline-block;">Track Your Case</a>
                    </p>

                    <div style="background-color: #fffaf0; border: 1px solid #feebc8; border-radius: 4px; padding: 10px; margin-top: 20px; font-size: 12px; color: #744210;">
                        <strong>Disclaimer:</strong> Mandatory verified-lawyer review is required before this document can be finalized or downloaded.
                    </div>
                </div>
                <div style="border-top: 1px solid #edf2f7; padding: 12px 20px; text-align: center; font-size: 11px; color: #718096;">
                    © {DateTime.UtcNow.Year} MuktoAin — Bangladesh
                </div>
            </div>
            """;

        await SendEmailAsync(toEmail, subject, body, ct);
    }

    public async Task SendReviewDecisionAsync(
        string toEmail,
        string caseTitle,
        string decisionStatus,
        string? comments,
        string? trackingCode,
        int caseId,
        string language = "bn",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return;

        var isBangla = language != "en";
        var isApproved = decisionStatus.Contains("Approve", StringComparison.OrdinalIgnoreCase)
            || decisionStatus.Contains("অনুমোদিত", StringComparison.OrdinalIgnoreCase);

        var subject = isBangla
            ? (isApproved
                ? $"[মুক্ত আইন] আপনার আইনি নথি অনুমোদিত হয়েছে — #{caseId}"
                : $"[মুক্ত আইন] মামলা পর্যালোচনার ফলাফল — #{caseId}")
            : (isApproved
                ? $"[MuktoAin] Legal Document Approved — #{caseId}"
                : $"[MuktoAin] Lawyer Review Update — #{caseId}");

        var baseUrl = string.IsNullOrWhiteSpace(_settings.AppBaseUrl)
            ? "https://localhost:7001"
            : _settings.AppBaseUrl.TrimEnd('/');

        var trackUrl = !string.IsNullOrEmpty(trackingCode)
            ? $"{baseUrl}/Case/Result?id={caseId}&code={trackingCode}"
            : $"{baseUrl}/Case/Result?id={caseId}";

        var statusBg = isApproved ? "#38a169" : "#e53e3e";

        var body = isBangla
            ? $"""
            <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px; background-color: #ffffff;">
                <div style="background-color: #1a365d; color: #ffffff; padding: 16px 20px; border-radius: 6px 6px 0 0; text-align: center;">
                    <h2 style="margin: 0; font-size: 22px;">মুক্ত আইন (MuktoAin)</h2>
                    <p style="margin: 4px 0 0; font-size: 13px; opacity: 0.9;">আইনজীবী পর্যালোচনা আপডেট</p>
                </div>
                <div style="padding: 20px;">
                    <h3 style="color: #2b6cb0; margin-top: 0;">আপনার মামলার নথি পর্যালোচনা সম্পন্ন হয়েছে</h3>
                    <p>প্রিয় নাগরিক,</p>
                    <p>আপনার <strong>"{WebUtility.HtmlEncode(caseTitle)}"</strong> সংক্রান্ত মামলার নথির পর্যালোচনা সম্পন্ন হয়েছে।</p>
                    
                    <div style="background-color: #f7fafc; border-left: 4px solid {statusBg}; padding: 12px 16px; margin: 18px 0; border-radius: 4px;">
                        <p style="margin: 4px 0;"><strong>মামলা নম্বর:</strong> #{caseId}</p>
                        <p style="margin: 4px 0;"><strong>আইনজীবীর সিদ্ধান্ত:</strong> <span style="color: {statusBg}; font-weight: bold;">{WebUtility.HtmlEncode(decisionStatus)}</span></p>
                        {(string.IsNullOrWhiteSpace(comments) ? "" : $"<p style=\"margin: 6px 0 2px;\"><strong>আইনজীবীর মন্তব্য / পরামর্শ:</strong></p><p style=\"margin: 0; font-style: italic; background: #edf2f7; padding: 8px 12px; border-radius: 4px;\">{WebUtility.HtmlEncode(comments)}</p>")}
                    </div>

                    <p style="margin: 20px 0; text-align: center;">
                        <a href="{trackUrl}" style="background-color: #3182ce; color: #ffffff; font-weight: bold; padding: 12px 24px; text-decoration: none; border-radius: 6px; display: inline-block;">চূড়ান্ত নথি দেখতে ক্লিক করুন</a>
                    </p>
                </div>
                <div style="border-top: 1px solid #edf2f7; padding: 12px 20px; text-align: center; font-size: 11px; color: #718096;">
                    © {DateTime.UtcNow.Year} মুক্ত আইন (MuktoAin) — বাংলাদেশ
                </div>
            </div>
            """
            : $"""
            <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px; background-color: #ffffff;">
                <div style="background-color: #1a365d; color: #ffffff; padding: 16px 20px; border-radius: 6px 6px 0 0; text-align: center;">
                    <h2 style="margin: 0; font-size: 22px;">MuktoAin (মুক্ত আইন)</h2>
                    <p style="margin: 4px 0 0; font-size: 13px; opacity: 0.9;">Lawyer Review Update</p>
                </div>
                <div style="padding: 20px;">
                    <h3 style="color: #2b6cb0; margin-top: 0;">Lawyer Review Completed for Your Case</h3>
                    <p>Dear Citizen,</p>
                    <p>A verified lawyer has completed reviewing your legal draft for <strong>"{WebUtility.HtmlEncode(caseTitle)}"</strong>.</p>
                    
                    <div style="background-color: #f7fafc; border-left: 4px solid {statusBg}; padding: 12px 16px; margin: 18px 0; border-radius: 4px;">
                        <p style="margin: 4px 0;"><strong>Case ID:</strong> #{caseId}</p>
                        <p style="margin: 4px 0;"><strong>Lawyer Decision:</strong> <span style="color: {statusBg}; font-weight: bold;">{WebUtility.HtmlEncode(decisionStatus)}</span></p>
                        {(string.IsNullOrWhiteSpace(comments) ? "" : $"<p style=\"margin: 6px 0 2px;\"><strong>Lawyer Notes / Feedback:</strong></p><p style=\"margin: 0; font-style: italic; background: #edf2f7; padding: 8px 12px; border-radius: 4px;\">{WebUtility.HtmlEncode(comments)}</p>")}
                    </div>

                    <p style="margin: 20px 0; text-align: center;">
                        <a href="{trackUrl}" style="background-color: #3182ce; color: #ffffff; font-weight: bold; padding: 12px 24px; text-decoration: none; border-radius: 6px; display: inline-block;">View Document & Details</a>
                    </p>
                </div>
                <div style="border-top: 1px solid #edf2f7; padding: 12px 20px; text-align: center; font-size: 11px; color: #718096;">
                    © {DateTime.UtcNow.Year} MuktoAin — Bangladesh
                </div>
            </div>
            """;

        await SendEmailAsync(toEmail, subject, body, ct);
    }

    public async Task SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return;

        if (string.IsNullOrWhiteSpace(_settings.SmtpHost))
        {
            _logger.LogInformation(
                "[DEV EMAIL LOGGER] To: {To} | Subject: {Subject} | (SMTP Host not configured, simulated send successfully)",
                toEmail,
                subject);
            return;
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_settings.SenderEmail, _settings.SenderName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(toEmail));

            using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
            {
                EnableSsl = _settings.EnableSsl
            };

            if (!string.IsNullOrWhiteSpace(_settings.SmtpUser) && !string.IsNullOrWhiteSpace(_settings.SmtpPass))
            {
                client.Credentials = new NetworkCredential(_settings.SmtpUser, _settings.SmtpPass);
            }

            await client.SendMailAsync(message, ct);
            _logger.LogInformation("Notification email sent to {To} via SMTP {Host}", toEmail, _settings.SmtpHost);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send email to {To} via SMTP {Host}", toEmail, _settings.SmtpHost);
        }
    }
}
