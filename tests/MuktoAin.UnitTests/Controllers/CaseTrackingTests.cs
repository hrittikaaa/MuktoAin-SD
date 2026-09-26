using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using MuktoAin.Application.Services;
using MuktoAin.Domain.Entities;
using MuktoAin.Domain.Enums;
using MuktoAin.Domain.Interfaces;
using MuktoAin.Domain.Interfaces.Repositories;
using MuktoAin.Web.Controllers;
using MuktoAin.Web.Session;
using MuktoAin.Web.ViewModels;
using Xunit;

namespace MuktoAin.UnitTests.Controllers;

public class CaseTrackingTests
{
    private readonly Mock<ICaseRepository> _caseRepo = new();
    private readonly Mock<IRepository<CaseCategory>> _categoryRepo = new();
    private readonly Mock<IRepository<District>> _districtRepo = new();
    private readonly Mock<IRepository<GeneratedDocument>> _docRepo = new();
    private readonly Mock<IRepository<LawyerReview>> _reviewRepo = new();
    private readonly Mock<IRepository<LawyerProfile>> _lawyerProfileRepo = new();
    private readonly Mock<IModerationService> _moderation = new();
    private readonly Mock<IRepository<Notification>> _notificationRepo = new();
    private readonly Mock<IEncryptionService> _encryption = new();
    private readonly CaseService _caseService;
    private readonly DefaultHttpContext _httpContext;
    private readonly CaseController _controller;

    public CaseTrackingTests()
    {
        _encryption.Setup(e => e.Decrypt(It.IsAny<string>()))
            .Returns<string>(s => s ?? string.Empty);
        _encryption.Setup(e => e.Encrypt(It.IsAny<string>()))
            .Returns<string>(s => s ?? string.Empty);

        _categoryRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync(new CaseCategory { CategoryId = 1, Name = "Labour" });
        _districtRepo.Setup(r => r.GetByIdAsync(It.IsAny<byte>()))
            .ReturnsAsync(new District { DistrictId = 1, Name = "Dhaka" });
        _notificationRepo.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<Notification>());

        _caseService = new CaseService(
            _caseRepo.Object, _categoryRepo.Object, _districtRepo.Object, _encryption.Object, _notificationRepo.Object);

        _httpContext = new DefaultHttpContext
        {
            Session = new TestSession(),
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "42")
            }))
        };

        _controller = new CaseController(
            _caseService,
            null!,
            null!,
            _caseRepo.Object,
            _categoryRepo.Object,
            _districtRepo.Object,
            _docRepo.Object,
            _reviewRepo.Object,
            _lawyerProfileRepo.Object,
            _moderation.Object,
            _notificationRepo.Object,
            null!)
        {
            ControllerContext = new ControllerContext { HttpContext = _httpContext },
            TempData = new TempDataDictionary(_httpContext, Mock.Of<ITempDataProvider>())
        };
    }

    [Fact]
    public async Task Track_ValidTrackingCode_RemembersInSessionAndRedirects()
    {
        var matched = new Case
        {
            CaseId = 101,
            AnonymousTrackingCode = "TRACK-12345",
            IsAnonymous = true,
            Title = "Anon Case"
        };
        _caseRepo.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<Case, bool>>>()))
            .ReturnsAsync(new List<Case> { matched });

        var result = await _controller.Track(status: null, code: " TRACK-12345 ") as RedirectToActionResult;

        Assert.NotNull(result);
        Assert.Equal(nameof(CaseController.Result), result!.ActionName);
        Assert.Equal(101, result.RouteValues!["id"]);
        Assert.Equal("TRACK-12345", result.RouteValues!["code"]);
        Assert.Equal("TRACK-12345", TrackedCases.Resolve(_httpContext.Session, 101));
    }

    [Fact]
    public async Task Track_InvalidTrackingCode_SetsErrorAndRendersView()
    {
        _caseRepo.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<Case, bool>>>()))
            .ReturnsAsync(new List<Case>());
        _caseRepo.Setup(r => r.GetByUserIdAsync(42))
            .ReturnsAsync(new List<Case>());

        var result = await _controller.Track(status: null, code: "INVALID-CODE") as ViewResult;

        Assert.NotNull(result);
        Assert.True(_controller.TempData.ContainsKey("Error"));
        Assert.True(_controller.TempData.ContainsKey("ErrorEn"));
    }

    [Fact]
    public async Task Track_OrdersCasesByCreatedAtDescending()
    {
        var now = DateTime.UtcNow;
        var cases = new List<Case>
        {
            new() { CaseId = 1, UserId = 42, Title = "Old", CreatedAt = now.AddDays(-10), CategoryId = 1, DistrictId = 1 },
            new() { CaseId = 2, UserId = 42, Title = "Newest", CreatedAt = now.AddMinutes(-5), CategoryId = 1, DistrictId = 1 },
            new() { CaseId = 3, UserId = 42, Title = "Middle", CreatedAt = now.AddDays(-2), CategoryId = 1, DistrictId = 1 }
        };
        _caseRepo.Setup(r => r.GetByUserIdAsync(42)).ReturnsAsync(cases);

        var result = await _controller.Track(status: null, code: null) as ViewResult;

        Assert.NotNull(result);
        var vm = Assert.IsType<CaseTrackViewModel>(result!.Model);
        Assert.Equal(3, vm.Cases.Count);
        Assert.Equal(2, vm.Cases[0].CaseId);
        Assert.Equal(3, vm.Cases[1].CaseId);
        Assert.Equal(1, vm.Cases[2].CaseId);
    }

    [Fact]
    public async Task Track_RejectedDocument_ShowsRejectedStatusAndFiltersProperly()
    {
        var now = DateTime.UtcNow;
        var rejectedCase = new Case
        {
            CaseId = 5,
            UserId = 42,
            Title = "Rejected Case",
            Status = CaseStatus.UnderReview,
            CreatedAt = now,
            CategoryId = 1,
            DistrictId = 1,
            Documents = new List<GeneratedDocument>
            {
                new() { DocumentId = 10, CaseId = 5, Status = DocumentStatus.Rejected }
            }
        };
        var normalCase = new Case
        {
            CaseId = 6,
            UserId = 42,
            Title = "Normal Case",
            Status = CaseStatus.Submitted,
            CreatedAt = now.AddHours(-1),
            CategoryId = 1,
            DistrictId = 1
        };

        _caseRepo.Setup(r => r.GetByUserIdAsync(42)).ReturnsAsync(new List<Case> { rejectedCase, normalCase });

        // Filter: Rejected
        var result = await _controller.Track(status: "Rejected", code: null) as ViewResult;
        Assert.NotNull(result);
        var vm = Assert.IsType<CaseTrackViewModel>(result!.Model);
        Assert.Single(vm.Cases);
        Assert.Equal(5, vm.Cases[0].CaseId);
        Assert.Equal("Rejected", vm.Cases[0].Status);

        // Filter: Submitted
        var subResult = await _controller.Track(status: "Submitted", code: null) as ViewResult;
        Assert.NotNull(subResult);
        var subVm = Assert.IsType<CaseTrackViewModel>(subResult!.Model);
        Assert.Single(subVm.Cases);
        Assert.Equal(6, subVm.Cases[0].CaseId);
    }
}
