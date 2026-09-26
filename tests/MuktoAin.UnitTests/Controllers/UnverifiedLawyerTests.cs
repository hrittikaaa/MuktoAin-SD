using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MuktoAin.Application.Services;
using MuktoAin.Domain.Entities;
using MuktoAin.Domain.Enums;
using MuktoAin.Domain.Interfaces;
using MuktoAin.Domain.Interfaces.Repositories;
using MuktoAin.Domain.Interfaces.Services;
using MuktoAin.UnitTests.TestSupport;
using MuktoAin.Web.Controllers;
using MuktoAin.Web.ViewModels;

namespace MuktoAin.UnitTests.Controllers;

// A lawyer whose bar verification was rejected (profile 7, user 42).
// #5: resubmitting must not take a bar number another lawyer holds (UNIQUE
// column -> used to be a 500) or one longer than the column.
// #19: pages for verified lawyers only (History, Payments, RequestPayout)
// send them to Status, like Queue and Review already do.
public class UnverifiedLawyerTests
{
    private readonly Mock<IRepository<LawyerProfile>> _profileRepo = new();
    private readonly Mock<IRepository<PayoutRequest>> _payoutRepo = new();
    private readonly LawyerProfile _me = new()
    {
        LawyerProfileId = 7, UserId = 42, BarRegistrationNumber = "BAR-OLD",
        VerificationStatus = VerificationStatus.Rejected, RejectionReason = "Not found"
    };
    private readonly LawyerController _controller;

    public UnverifiedLawyerTests()
    {
        _profileRepo.SetupRows(new List<LawyerProfile>
        {
            _me,
            new() { LawyerProfileId = 8, UserId = 50, BarRegistrationNumber = "BAR-TAKEN", VerificationStatus = VerificationStatus.Approved }
        });

        var userManager = new Mock<UserManager<User>>(
            new Mock<IUserStore<User>>().Object, Options.Create(new IdentityOptions()),
            Mock.Of<IPasswordHasher<User>>(), Array.Empty<IUserValidator<User>>(),
            Array.Empty<IPasswordValidator<User>>(), Mock.Of<ILookupNormalizer>(),
            new IdentityErrorDescriber(), null!, Mock.Of<ILogger<UserManager<User>>>());
        var notifications = Mock.Of<IRepository<Notification>>();
        var caseRepo = Mock.Of<ICaseRepository>();
        var caseService = new CaseService(caseRepo, Mock.Of<IRepository<CaseCategory>>(), Mock.Of<IRepository<District>>(),
            Mock.Of<IEncryptionService>(), notifications);
        var reviewService = new LawyerReviewService(
            Mock.Of<IRepository<GeneratedDocument>>(), Mock.Of<IRepository<LawyerReview>>(), _profileRepo.Object, caseRepo,
            Mock.Of<IRepository<CaseCategory>>(), Mock.Of<IRepository<District>>(), Mock.Of<IRepository<CaseActReference>>(),
            Mock.Of<IRepository<ActSection>>(), Mock.Of<IRepository<Act>>(), Mock.Of<IEncryptionService>(), caseService, notifications);
        var paymentService = new PaymentService(
            Mock.Of<IRepository<PaymentOrder>>(), _payoutRepo.Object, _profileRepo.Object, caseRepo,
            userManager.Object, Mock.Of<IAdminAuditService>(), notifications,
            Mock.Of<IPaymentGatewayResolver>(), Mock.Of<IAiTurnReservationStore>());

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "42"), new Claim(ClaimTypes.Role, "Lawyer")
            }, "test"))
        };
        _controller = new LawyerController(reviewService, paymentService, _profileRepo.Object, userManager.Object,
            new NotificationService(notifications, userManager.Object, Mock.Of<ILogger<NotificationService>>()))
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>())
        };
    }

    private void AssertUnchanged()
    {
        Assert.Equal("BAR-OLD", _me.BarRegistrationNumber);
        Assert.Equal(VerificationStatus.Rejected, _me.VerificationStatus);
        _profileRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task BarNumberHeldByAnotherLawyer_IsRefusedWithMessage()
    {
        var result = await _controller.Resubmit(new LawyerStatusViewModel { BarRegistrationNumber = " BAR-TAKEN " });

        Assert.Equal(nameof(LawyerController.Status), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Contains("already registered", (string)_controller.TempData["ErrorEn"]!);
        AssertUnchanged();
    }

    [Fact]
    public async Task BarNumberLongerThanTheColumn_IsRefused()
    {
        await _controller.Resubmit(new LawyerStatusViewModel { BarRegistrationNumber = new string('9', 101) });

        Assert.NotNull(_controller.TempData["ErrorEn"]);
        AssertUnchanged();
    }

    [Fact]
    public async Task KeepingOwnBarNumber_IsAllowedAndTrimmed()
    {
        await _controller.Resubmit(new LawyerStatusViewModel { BarRegistrationNumber = "  BAR-OLD  ", Specialization = "  Labour law " });

        Assert.Equal("BAR-OLD", _me.BarRegistrationNumber);
        Assert.Equal("Labour law", _me.Specialization);
        Assert.Equal(VerificationStatus.Pending, _me.VerificationStatus);
        _profileRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // #13: a resubmission is a fresh application -- the old decision goes.
    [Fact]
    public async Task Resubmit_ClearsThePreviousDecision()
    {
        _me.VerifiedAt = new DateTime(2026, 9, 1);
        _me.VerifiedByAdminId = 1;

        await _controller.Resubmit(new LawyerStatusViewModel { BarRegistrationNumber = "BAR-NEW" });

        Assert.Equal(VerificationStatus.Pending, _me.VerificationStatus);
        Assert.Null(_me.RejectionReason);
        Assert.Null(_me.VerifiedAt);
        Assert.Null(_me.VerifiedByAdminId);
    }

    [Fact]
    public async Task SpecializationLongerThanTheColumn_IsRefused()
    {
        await _controller.Resubmit(new LawyerStatusViewModel
        {
            BarRegistrationNumber = "BAR-NEW", Specialization = new string('x', 201)
        });

        Assert.NotNull(_controller.TempData["ErrorEn"]);
        AssertUnchanged();
    }

    private static void AssertSentToStatus(IActionResult result) =>
        Assert.Equal(nameof(LawyerController.Status), Assert.IsType<RedirectToActionResult>(result).ActionName);

    [Fact]
    public async Task History_IsForVerifiedLawyersOnly() =>
        AssertSentToStatus(await _controller.History(null, null, null, null));

    [Fact]
    public async Task Payments_IsForVerifiedLawyersOnly() =>
        AssertSentToStatus(await _controller.Payments());

    [Fact]
    public async Task RequestPayout_IsForVerifiedLawyersOnly_AndCreatesNothing()
    {
        AssertSentToStatus(await _controller.RequestPayout());
        _payoutRepo.Verify(r => r.AddAsync(It.IsAny<PayoutRequest>()), Times.Never);
    }
}
