using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MuktoAin.Domain.Interfaces.Services;
using MuktoAin.Infrastructure.Email;
using Xunit;

namespace MuktoAin.UnitTests.Services;

public class SmtpEmailServiceTests
{
    private readonly Mock<ILogger<SmtpEmailService>> _loggerMock = new();

    [Fact]
    public async Task SendEmailAsync_WhenSmtpNotConfigured_LogsSimulationAndDoesNotThrow()
    {
        var settings = Options.Create(new EmailSettings { SmtpHost = "" });
        var service = new SmtpEmailService(settings, _loggerMock.Object);

        await service.SendEmailAsync("test@example.com", "Test Subject", "<p>Hello</p>");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[DEV EMAIL LOGGER]")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendCaseSubmittedAsync_WithEmptyEmail_DoesNothing()
    {
        var settings = Options.Create(new EmailSettings { SmtpHost = "" });
        var service = new SmtpEmailService(settings, _loggerMock.Object);

        await service.SendCaseSubmittedAsync("", "Case Title", "TRACK123", 101, "bn");

        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task SendCaseSubmittedAsync_Bangla_ExecutesSuccessfully()
    {
        var settings = Options.Create(new EmailSettings
        {
            SmtpHost = "",
            AppBaseUrl = "https://muktoain.test"
        });
        var service = new SmtpEmailService(settings, _loggerMock.Object);

        await service.SendCaseSubmittedAsync("citizen@example.com", "চাকরি হতে অন্যায় বরখাস্ত", "TRK-999", 55, "bn");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("মামলা আবেদন গৃহীত হয়েছে — #55")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendCaseSubmittedAsync_English_ExecutesSuccessfully()
    {
        var settings = Options.Create(new EmailSettings
        {
            SmtpHost = "",
            AppBaseUrl = "https://muktoain.test"
        });
        var service = new SmtpEmailService(settings, _loggerMock.Object);

        await service.SendCaseSubmittedAsync("citizen@example.com", "Unlawful Termination", "TRK-999", 55, "en");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Case Application Submitted — #55")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendReviewDecisionAsync_Approved_FormatsSubjectProperly()
    {
        var settings = Options.Create(new EmailSettings
        {
            SmtpHost = "",
            AppBaseUrl = "https://muktoain.test"
        });
        var service = new SmtpEmailService(settings, _loggerMock.Object);

        await service.SendReviewDecisionAsync(
            "citizen@example.com",
            "চাকরি হতে বরখাস্ত",
            "অনুমোদিত",
            "সব তথ্য সঠিক আছে।",
            "TRK-999",
            55,
            "bn");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("আপনার আইনি নথি অনুমোদিত হয়েছে — #55")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
