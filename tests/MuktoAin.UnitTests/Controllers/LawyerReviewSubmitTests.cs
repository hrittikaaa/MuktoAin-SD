using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MuktoAin.UnitTests.TestSupport;
using MuktoAin.Application.Services;
using MuktoAin.Domain.Entities;
using MuktoAin.Domain.Enums;
using MuktoAin.Domain.Interfaces;
using MuktoAin.Domain.Interfaces.Repositories;
using MuktoAin.Domain.Interfaces.Services;
using MuktoAin.Web.Controllers;
using MuktoAin.Web.ViewModels;

namespace MuktoAin.UnitTests.Controllers;

// Review submit workflow (#6, #8): an invalid submission must re-show the
// workspace with the lawyer's own edited text and comment, never reload the
// document from the database and never save anything. The services are real,
// over mocked repositories; the signed-in lawyer holds profile 7 and has
// claimed document 10.
public class LawyerReviewSubmitTests
{
    private const int UserId = 42;
    private const int ProfileId = 7;

    private readonly Mock<IRepository<GeneratedDocument>> _docRepo = new();
    private readonly Mock<IRepository<LawyerReview>> _reviewRepo = new();
    private readonly Mock<IRepository<LawyerProfile>> _profileRepo = new();
    private readonly Mock<ICaseRepository> _caseRepo = new();
    private readonly Mock<IRepository<CaseCategory>> _categoryRepo = new();
    private readonly Mock<IRepository<District>> _districtRepo = new();
    private readonly Mock<IRepository<CaseActReference>> _refRepo = new();
    private readonly GeneratedDocument _doc;
    private readonly LawyerController _controller;

    public LawyerReviewSubmitTests()
    {
        var userManager = new Mock<UserManager<User>>(
            new Mock<IUserStore<User>>().Object, Options.Create(new IdentityOptions()),
            Mock.Of<IPasswordHasher<User>>(), Array.Empty<IUserValidator<User>>(),
            Array.Empty<IPasswordValidator<User>>(), Mock.Of<ILookupNormalizer>(),
            new IdentityErrorDescriber(), null!, Mock.Of<ILogger<UserManager<User>>>());

        var profile = new LawyerProfile
        {
            LawyerProfileId = ProfileId, UserId = UserId, BarRegistrationNumber = "BAR-1001",
            VerificationStatus = VerificationStatus.Approved
        };
        _profileRepo.SetupRows(new List<LawyerProfile> { profile });

        _doc = new GeneratedDocument
        {
            DocumentId = 10, CaseId = 1, Status = DocumentStatus.UnderReview,
            AssignedLawyerProfileId = ProfileId, ContentDraft = "AI draft"
        };
        _docRepo.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(_doc);
        var c = new Case { CaseId = 1, Title = "Unpaid wages", Description = "Narrative", CategoryId = 1, DistrictId = 1, Status = CaseStatus.UnderReview };
        _caseRepo.Setup(r => r.GetWithDocumentsAsync(1)).ReturnsAsync(c);
        _caseRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(c);
        _categoryRepo.Setup(r => r.GetByIdAsync(It.IsAny<object>())).ReturnsAsync(new CaseCategory { CategoryId = 1, Name = "Labour" });
        _districtRepo.Setup(r => r.GetByIdAsync(It.IsAny<object>())).ReturnsAsync(new District { DistrictId = 1, Name = "Dhaka" });
        _refRepo.SetupRows(new List<CaseActReference>());

        var encryption = new Mock<IEncryptionService>();
        encryption.Setup(e => e.Decrypt(It.IsAny<string>())).Returns<string>(s => s);
        var notifications = Mock.Of<IRepository<Notification>>();
        var caseService = new CaseService(_caseRepo.Object, _categoryRepo.Object, _districtRepo.Object, encryption.Object, notifications);
        var reviewService = new LawyerReviewService(
            _docRepo.Object, _reviewRepo.Object, _profileRepo.Object, _caseRepo.Object,
            _categoryRepo.Object, _districtRepo.Object, _refRepo.Object, Mock.Of<IRepository<ActSection>>(),
            Mock.Of<IRepository<Act>>(), encryption.Object, caseService, notifications);
        var paymentService = new PaymentService(
            Mock.Of<IRepository<PaymentOrder>>(), Mock.Of<IRepository<PayoutRequest>>(), _profileRepo.Object, _caseRepo.Object,
            userManager.Object, Mock.Of<IAdminAuditService>(), notifications,
            Mock.Of<IPaymentGatewayResolver>(), Mock.Of<IAiTurnReservationStore>());

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, UserId.ToString()), new Claim(ClaimTypes.Role, "Lawyer")
            }, "test"))
        };
        _controller = new LawyerController(reviewService, paymentService, _profileRepo.Object, userManager.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>())
        };
    }

    // Mirrors MVC model binding: DataAnnotations errors land in ModelState.
    private async Task<IActionResult> Submit(LawyerReviewViewModel vm)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(vm, new ValidationContext(vm), results, validateAllProperties: true);
        foreach (var r in results)
            foreach (var member in r.MemberNames)
                _controller.ModelState.AddModelError(member, r.ErrorMessage ?? "invalid");
        return await _controller.SubmitReview(vm);
    }

    private void AssertNothingSaved()
    {
        _reviewRepo.Verify(r => r.AddAsync(It.IsAny<LawyerReview>()), Times.Never);
        Assert.Equal(DocumentStatus.UnderReview, _doc.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MissingComment_RedisplaysWorkspaceWithPostedEdits(string comment)
    {
        var result = await Submit(new LawyerReviewViewModel
        {
            DocumentId = 10, Decision = "EditedApproved", EditedContent = "My careful edits", Comments = comment
        });

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Review", view.ViewName);
        var model = Assert.IsType<LawyerReviewViewModel>(view.Model);
        Assert.Equal("My careful edits", model.EditedContent);
        Assert.Equal("AI draft", model.ContentDraft); // workspace context rebuilt
        Assert.Equal("Unpaid wages", model.CaseTitle);
        Assert.False(_controller.ModelState.IsValid);
        AssertNothingSaved();
    }

    [Fact]
    public async Task EditedApprovedWithEmptyText_RedisplaysWithCommentKept()
    {
        var result = await Submit(new LawyerReviewViewModel
        {
            DocumentId = 10, Decision = "EditedApproved", EditedContent = "  ", Comments = "Looks right"
        });

        var model = Assert.IsType<LawyerReviewViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Looks right", model.Comments);
        Assert.True(_controller.ModelState.ContainsKey(nameof(LawyerReviewViewModel.EditedContent)));
        AssertNothingSaved();
    }

    // #8: an unknown or numeric decision was silently treated as EditedApproved.
    [Theory]
    [InlineData("Bogus")]
    [InlineData("7")]
    [InlineData("approved")]
    public async Task UnknownDecision_IsRejectedNotDefaulted(string decision)
    {
        var result = await Submit(new LawyerReviewViewModel
        {
            DocumentId = 10, Decision = decision, EditedContent = "text", Comments = "ok"
        });

        Assert.IsType<ViewResult>(result);
        Assert.True(_controller.ModelState.ContainsKey(nameof(LawyerReviewViewModel.Decision)));
        AssertNothingSaved();
    }

    [Fact]
    public async Task ValidRejection_SavesAndReturnsToQueue()
    {
        var result = await Submit(new LawyerReviewViewModel
        {
            DocumentId = 10, Decision = "Rejected", EditedContent = "ignored", Comments = "Cite section 33"
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(LawyerController.Queue), redirect.ActionName);
        _reviewRepo.Verify(r => r.AddAsync(It.Is<LawyerReview>(rv =>
            rv.Decision == ReviewDecision.Rejected && rv.Comments == "Cite section 33")), Times.Once);
        Assert.Equal(DocumentStatus.Rejected, _doc.Status);
    }
}
