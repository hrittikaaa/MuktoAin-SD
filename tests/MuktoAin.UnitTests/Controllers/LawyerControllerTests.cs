using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MuktoAin.UnitTests.TestSupport;
using MuktoAin.Application.DTOs;
using MuktoAin.Application.Services;
using MuktoAin.Domain.Common;
using MuktoAin.Domain.Entities;
using MuktoAin.Domain.Enums;
using MuktoAin.Domain.Interfaces;
using MuktoAin.Domain.Interfaces.Repositories;
using MuktoAin.Domain.Interfaces.Services;
using MuktoAin.Web.Controllers;
using MuktoAin.Web.ViewModels;
using Xunit;

namespace MuktoAin.UnitTests.Controllers;

// FR-13/14/15/23/24 lawyer workspace. The review, verification and payment
// services are concrete, so they are built for real over mocked repositories
// (same approach as PaymentControllerTests). The signed-in lawyer is user 42
// with LawyerProfileId 7 unless a test says otherwise.
public class LawyerControllerTests
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
    private readonly Mock<IRepository<ActSection>> _sectionRepo = new();
    private readonly Mock<IRepository<Act>> _actRepo = new();
    private readonly Mock<IRepository<PaymentOrder>> _orderRepo = new();
    private readonly Mock<IRepository<PayoutRequest>> _payoutRepo = new();
    private readonly Mock<UserManager<User>> _userManager;
    private readonly LawyerProfile _profile;
    private readonly LawyerController _controller;

    public LawyerControllerTests()
    {
        _userManager = new Mock<UserManager<User>>(
            new Mock<IUserStore<User>>().Object,
            Options.Create(new IdentityOptions()),
            Mock.Of<IPasswordHasher<User>>(),
            Array.Empty<IUserValidator<User>>(),
            Array.Empty<IPasswordValidator<User>>(),
            Mock.Of<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            null!,
            Mock.Of<ILogger<UserManager<User>>>());
        _userManager.Setup(m => m.FindByIdAsync(UserId.ToString()))
            .ReturnsAsync(new User { Id = UserId, FullName = "Adv. Rahima Khatun" });

        _profile = new LawyerProfile
        {
            LawyerProfileId = ProfileId,
            UserId = UserId,
            BarRegistrationNumber = "BAR-1001",
            Specialization = "Labour law",
            VerificationStatus = VerificationStatus.Approved
        };
        _profileRepo.SetupRows(new List<LawyerProfile> { _profile });
        _profileRepo.Setup(r => r.GetByIdAsync(ProfileId)).ReturnsAsync(() => _profile);

        _docRepo.SetupRows(new List<GeneratedDocument>());
        _reviewRepo.SetupRows(new List<LawyerReview>());
        _refRepo.SetupRows(new List<CaseActReference>());
        _orderRepo.SetupRows(new List<PaymentOrder>());
        _payoutRepo.SetupRows(new List<PayoutRequest>());
        _categoryRepo.Setup(r => r.GetByIdAsync(It.IsAny<object>()))
            .ReturnsAsync(new CaseCategory { CategoryId = 1, Name = "Labour" });
        _districtRepo.Setup(r => r.GetByIdAsync(It.IsAny<object>()))
            .ReturnsAsync(new District { DistrictId = 1, Name = "Dhaka" });

        _controller = NewController(UserId.ToString());
    }

    private LawyerController NewController(string? userId)
    {
        var encryption = new Mock<IEncryptionService>();
        encryption.Setup(e => e.Decrypt(It.IsAny<string>())).Returns<string>(s => s);
        var notifications = Mock.Of<IRepository<Notification>>();
        var caseService = new CaseService(
            _caseRepo.Object, _categoryRepo.Object, _districtRepo.Object, encryption.Object, notifications);

        var reviewService = new LawyerReviewService(
            _docRepo.Object, _reviewRepo.Object, _profileRepo.Object, _caseRepo.Object,
            _categoryRepo.Object, _districtRepo.Object, _refRepo.Object, _sectionRepo.Object,
            _actRepo.Object, encryption.Object, caseService, notifications);
        var paymentService = new PaymentService(
            _orderRepo.Object, _payoutRepo.Object, _profileRepo.Object, _caseRepo.Object,
            _userManager.Object, Mock.Of<IAdminAuditService>(), notifications,
            Mock.Of<IPaymentGatewayResolver>(), Mock.Of<IAiTurnReservationStore>());

        var claims = new List<Claim> { new(ClaimTypes.Role, "Lawyer") };
        if (userId != null) claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
        };

        return new LawyerController(reviewService, paymentService, _profileRepo.Object, _userManager.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
        };
    }

    private void AddCase(int caseId, string title = "Unpaid wages", int categoryId = 1, string description = "Employer withheld three months of pay.")
    {
        var c = new Case { CaseId = caseId, Title = title, Description = description, CategoryId = categoryId, DistrictId = 1, Status = CaseStatus.UnderReview };
        _caseRepo.Setup(r => r.GetWithDocumentsAsync(caseId)).ReturnsAsync(c);
        _caseRepo.Setup(r => r.GetByIdAsync(caseId)).ReturnsAsync(c);
    }

    private GeneratedDocument AddDocument(int documentId, int caseId, DocumentStatus status = DocumentStatus.UnderReview,
        int? assignedTo = null, DateTime? createdAt = null)
    {
        var doc = new GeneratedDocument
        {
            DocumentId = documentId,
            CaseId = caseId,
            Status = status,
            ContentDraft = $"Draft {documentId}",
            AssignedLawyerProfileId = assignedTo,
            CreatedAt = createdAt ?? DateTime.UtcNow.AddHours(-documentId)
        };
        _docRepo.Setup(r => r.GetByIdAsync(documentId)).ReturnsAsync(doc);
        return doc;
    }

    private static string? RedirectAction(IActionResult result) => Assert.IsType<RedirectToActionResult>(result).ActionName;

    // ---- attributes ---------------------------------------------------------

    [Fact]
    public void Controller_IsRestrictedToLawyerRole()
    {
        var authorize = typeof(LawyerController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();
        Assert.Equal("Lawyer", authorize.Roles);
    }

    [Theory]
    [InlineData(nameof(LawyerController.Resubmit))]
    [InlineData(nameof(LawyerController.Claim))]
    [InlineData(nameof(LawyerController.SubmitReview))]
    [InlineData(nameof(LawyerController.RequestPayout))]
    public void PostActions_CarryValidateAntiForgeryToken(string actionName)
    {
        var method = typeof(LawyerController).GetMethod(actionName)!;
        Assert.NotEmpty(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true));
        Assert.NotEmpty(method.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true));
    }

    // ---- Status / Resubmit --------------------------------------------------

    [Fact]
    public async Task Status_NoProfileForUser_ReturnsNotFound()
    {
        _profile.UserId = 999;

        Assert.IsType<NotFoundResult>(await _controller.Status());
    }

    [Fact]
    public async Task Status_MissingUserIdClaim_ReturnsNotFound()
    {
        var anonymous = NewController(userId: null);

        Assert.IsType<NotFoundResult>(await anonymous.Status());
    }

    [Fact]
    public async Task Status_ShowsProfileDetails()
    {
        _profile.VerificationStatus = VerificationStatus.Rejected;
        _profile.RejectionReason = "Bar number not found";

        var result = await _controller.Status();

        var model = Assert.IsType<LawyerStatusViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Adv. Rahima Khatun", model.LawyerName);
        Assert.Equal("BAR-1001", model.BarRegistrationNumber);
        Assert.Equal("Labour law", model.Specialization);
        Assert.Equal("Rejected", model.Status);
        Assert.Equal("Bar number not found", model.RejectionReason);
    }

    [Fact]
    public async Task Status_NullSpecialization_ShowsEmptyString()
    {
        _profile.Specialization = null;

        var model = Assert.IsType<LawyerStatusViewModel>(Assert.IsType<ViewResult>(await _controller.Status()).Model);

        Assert.Equal(string.Empty, model.Specialization);
    }

    [Theory]
    [InlineData(VerificationStatus.Pending)]
    [InlineData(VerificationStatus.Approved)]
    public async Task Resubmit_WhenNotRejected_IsForbidden(VerificationStatus status)
    {
        _profile.VerificationStatus = status;

        var result = await _controller.Resubmit(new LawyerStatusViewModel { BarRegistrationNumber = "BAR-2" });

        Assert.IsType<ForbidResult>(result);
        _profileRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task Resubmit_BlankBarNumber_RedirectsWithErrorAndKeepsStatus()
    {
        _profile.VerificationStatus = VerificationStatus.Rejected;

        var result = await _controller.Resubmit(new LawyerStatusViewModel { BarRegistrationNumber = "  " });

        Assert.Equal(nameof(LawyerController.Status), RedirectAction(result));
        Assert.Equal("Bar number required.", _controller.TempData["ErrorEn"]);
        Assert.Equal(VerificationStatus.Rejected, _profile.VerificationStatus);
        _profileRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task Resubmit_Valid_ResetsToPendingWithNewDetails()
    {
        _profile.VerificationStatus = VerificationStatus.Rejected;

        var result = await _controller.Resubmit(new LawyerStatusViewModel
        {
            BarRegistrationNumber = "BAR-2002",
            Specialization = "Consumer rights"
        });

        Assert.Equal(nameof(LawyerController.Status), RedirectAction(result));
        Assert.Equal(VerificationStatus.Pending, _profile.VerificationStatus);
        Assert.Equal("BAR-2002", _profile.BarRegistrationNumber);
        Assert.Equal("Consumer rights", _profile.Specialization);
        Assert.NotNull(_controller.TempData["SuccessEn"]);
        _profileRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Resubmit_BlankSpecialization_KeepsExistingOne()
    {
        _profile.VerificationStatus = VerificationStatus.Rejected;

        await _controller.Resubmit(new LawyerStatusViewModel { BarRegistrationNumber = "BAR-3", Specialization = "" });

        Assert.Equal("Labour law", _profile.Specialization);
    }

    // ---- Queue --------------------------------------------------------------

    [Theory]
    [InlineData(VerificationStatus.Pending)]
    [InlineData(VerificationStatus.Rejected)]
    public async Task Queue_UnverifiedLawyer_IsSentToStatus(VerificationStatus status)
    {
        _profile.VerificationStatus = status;

        Assert.Equal(nameof(LawyerController.Status), RedirectAction(await _controller.Queue(null)));
    }

    [Fact]
    public async Task Queue_ListsUnderReviewDocumentsOldestFirst()
    {
        AddCase(1, "Case one");
        AddCase(2, "Case two");
        var older = AddDocument(10, 1, createdAt: DateTime.UtcNow.AddHours(-30));
        var newer = AddDocument(11, 2, createdAt: DateTime.UtcNow.AddHours(-5));
        var draft = AddDocument(12, 2, status: DocumentStatus.Draft);
        _docRepo.SetupRows(new List<GeneratedDocument> { newer, draft, older });

        var result = await _controller.Queue(null);

        var model = Assert.IsType<LawyerQueueViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(new[] { 10, 11 }, model.Items.Select(i => i.DocumentId));
        Assert.Equal(2, model.PendingCount);
        Assert.Equal("All", model.ActiveFilter);
        Assert.Equal("Adv. Rahima Khatun", model.LawyerName);
        Assert.Equal("Case one", model.Items[0].CaseTitle);
        Assert.InRange(model.Items[0].WaitingHours, 29, 30);
    }

    [Fact]
    public async Task Queue_DocumentClaimedByMe_IsMineAndOpenable()
    {
        AddCase(1);
        var mine = AddDocument(10, 1, assignedTo: ProfileId);
        _docRepo.SetupRows(new List<GeneratedDocument> { mine });

        var model = Assert.IsType<LawyerQueueViewModel>(Assert.IsType<ViewResult>(await _controller.Queue(null)).Model);

        var item = Assert.Single(model.Items);
        Assert.True(item.IsMine);
        Assert.True(item.CanOpen);
        Assert.Equal("BAR-1001", item.ClaimedBy);
    }

    [Fact]
    public async Task Queue_DocumentClaimedByAnotherLawyer_CannotBeOpened()
    {
        var other = new LawyerProfile { LawyerProfileId = 8, UserId = 50, BarRegistrationNumber = "BAR-OTHER", VerificationStatus = VerificationStatus.Approved };
        _profileRepo.Setup(r => r.GetByIdAsync(8)).ReturnsAsync(other);
        AddCase(1);
        var theirs = AddDocument(10, 1, assignedTo: 8);
        _docRepo.SetupRows(new List<GeneratedDocument> { theirs });

        var model = Assert.IsType<LawyerQueueViewModel>(Assert.IsType<ViewResult>(await _controller.Queue(null)).Model);

        var item = Assert.Single(model.Items);
        Assert.False(item.IsMine);
        Assert.False(item.CanOpen);
        Assert.Equal("BAR-OTHER", item.ClaimedBy);
    }

    [Fact]
    public async Task Queue_UnclaimedFilter_HidesClaimedDocuments()
    {
        AddCase(1);
        var open = AddDocument(10, 1);
        var claimed = AddDocument(11, 1, assignedTo: ProfileId);
        _docRepo.SetupRows(new List<GeneratedDocument> { open, claimed });

        var model = Assert.IsType<LawyerQueueViewModel>(Assert.IsType<ViewResult>(await _controller.Queue("Unclaimed")).Model);

        Assert.Equal(new[] { 10 }, model.Items.Select(i => i.DocumentId));
        Assert.Equal("Unclaimed", model.ActiveFilter);
    }

    [Fact]
    public async Task Queue_MineFilter_ShowsOnlyMyClaims()
    {
        AddCase(1);
        var open = AddDocument(10, 1);
        var claimed = AddDocument(11, 1, assignedTo: ProfileId);
        _docRepo.SetupRows(new List<GeneratedDocument> { open, claimed });

        var model = Assert.IsType<LawyerQueueViewModel>(Assert.IsType<ViewResult>(await _controller.Queue("Mine")).Model);

        Assert.Equal(new[] { 11 }, model.Items.Select(i => i.DocumentId));
    }

    [Fact]
    public async Task Queue_MyFieldWithUnusableSpecialization_FallsBackToFullPool()
    {
        _profile.Specialization = "Maritime salvage";
        AddCase(1);
        var doc = AddDocument(10, 1);
        _docRepo.SetupRows(new List<GeneratedDocument> { doc });

        var model = Assert.IsType<LawyerQueueViewModel>(Assert.IsType<ViewResult>(await _controller.Queue("MyField")).Model);

        Assert.True(model.FieldFallback);
        Assert.Single(model.Items);
    }

    [Fact]
    public async Task Queue_PageBeyondLast_IsClampedToLastPage()
    {
        AddCase(1);
        var docs = Enumerable.Range(1, 25).Select(i => AddDocument(100 + i, 1)).ToList();
        _docRepo.SetupRows(docs);

        var model = Assert.IsType<LawyerQueueViewModel>(Assert.IsType<ViewResult>(await _controller.Queue(null, page: 9)).Model);

        Assert.Equal(2, model.Page);
        Assert.Equal(25, model.TotalCount);
        Assert.Equal(20, model.PageSize);
    }

    [Fact]
    public async Task Queue_EmptyPool_ShowsPageOneWithNoItems()
    {
        var model = Assert.IsType<LawyerQueueViewModel>(Assert.IsType<ViewResult>(await _controller.Queue(null)).Model);

        Assert.Empty(model.Items);
        Assert.Equal(1, model.Page);
        Assert.Equal(0, model.PendingCount);
    }

    // ---- Claim --------------------------------------------------------------

    [Fact]
    public async Task Claim_UnverifiedLawyer_IsSentToStatus()
    {
        _profile.VerificationStatus = VerificationStatus.Pending;

        Assert.Equal(nameof(LawyerController.Status), RedirectAction(await _controller.Claim(10)));
    }

    [Fact]
    public async Task Claim_UnclaimedDocument_AssignsItAndOpensWorkspace()
    {
        var doc = AddDocument(10, 1);

        var result = await _controller.Claim(10);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(LawyerController.Review), redirect.ActionName);
        Assert.Equal(10, redirect.RouteValues!["id"]);
        Assert.Equal(ProfileId, doc.AssignedLawyerProfileId);
        Assert.NotNull(doc.ClaimedAt);
    }

    [Fact]
    public async Task Claim_DocumentHeldByAnotherLawyer_ReturnsToQueueWithError()
    {
        var doc = AddDocument(10, 1, assignedTo: 8);

        var result = await _controller.Claim(10);

        Assert.Equal(nameof(LawyerController.Queue), RedirectAction(result));
        Assert.Equal("Another lawyer claimed this — back to the queue.", _controller.TempData["ErrorEn"]);
        Assert.Equal(8, doc.AssignedLawyerProfileId);
    }

    [Fact]
    public async Task Claim_DocumentNotUnderReview_ReturnsToQueue()
    {
        AddDocument(10, 1, status: DocumentStatus.Approved);

        Assert.Equal(nameof(LawyerController.Queue), RedirectAction(await _controller.Claim(10)));
    }

    [Fact]
    public async Task Claim_ConcurrencyConflictOnSave_ReturnsToQueue()
    {
        AddDocument(10, 1);
        _docRepo.Setup(r => r.SaveChangesAsync()).ThrowsAsync(new ConcurrencyConflictException("lost the race"));

        Assert.Equal(nameof(LawyerController.Queue), RedirectAction(await _controller.Claim(10)));
    }

    // ---- Review -------------------------------------------------------------

    [Fact]
    public async Task Review_UnknownDocument_ReturnsToQueueWithError()
    {
        Assert.Equal(nameof(LawyerController.Queue), RedirectAction(await _controller.Review(404)));
        Assert.NotNull(_controller.TempData["ErrorEn"]);
    }

    [Fact]
    public async Task Review_DocumentNotClaimedByMe_ReturnsToQueue()
    {
        AddCase(1);
        AddDocument(10, 1, assignedTo: ProfileId + 1);

        Assert.Equal(nameof(LawyerController.Queue), RedirectAction(await _controller.Review(10)));
    }

    [Fact]
    public async Task Review_BuildsWorkspaceFromOriginalDraft()
    {
        AddCase(1, "Unpaid wages", description: "Employer withheld pay.");
        AddDocument(10, 1, assignedTo: ProfileId);

        var result = await _controller.Review(10);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LawyerReviewViewModel>(view.Model);
        Assert.Equal(10, model.DocumentId);
        Assert.Equal(1, model.CaseId);
        Assert.Equal("Unpaid wages", model.CaseTitle);
        Assert.Equal("Labour", model.CategoryName);
        Assert.Equal("Draft 10", model.ContentDraft);
        Assert.Equal("Draft 10", model.EditedContent);
        Assert.Equal(nameof(ReviewDecision.EditedApproved), model.Decision);
        Assert.Equal("Dhaka", model.DistrictName);
        Assert.Equal("Employer withheld pay.", model.CitizenNarrative);
        Assert.False(model.CitizenEdited);
    }

    [Fact]
    public async Task Review_CitizenEditedDraft_PrefillsEditorWithCitizenVersion()
    {
        AddCase(1);
        var doc = AddDocument(10, 1, assignedTo: ProfileId);
        doc.CitizenEdited = true;
        doc.ContentFinal = "Citizen's edited draft";
        doc.VersionNo = 3;

        var view = Assert.IsType<ViewResult>(await _controller.Review(10));

        var model = Assert.IsType<LawyerReviewViewModel>(view.Model);
        Assert.Equal("Draft 10", model.ContentDraft);
        Assert.Equal("Citizen's edited draft", model.EditedContent);
        Assert.Equal(3, model.VersionNo);
    }

    [Fact]
    public async Task Review_IncludesCitedSections()
    {
        AddCase(1);
        AddDocument(10, 1, assignedTo: ProfileId);
        _refRepo.SetupRows(new List<CaseActReference>
        {
            new() { CaseId = 1, SectionId = 55, RelevanceScore = 0.8m, RetrievalMethod = RetrievalMethod.Vector },
            new() { CaseId = 2, SectionId = 56, RelevanceScore = 0.5m }
        });
        _sectionRepo.Setup(r => r.GetByIdAsync(55)).ReturnsAsync(new ActSection { SectionId = 55, ActId = 3, SectionNumber = "121", SectionText = "Wages shall be paid..." });
        _actRepo.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(new Act { ActId = 3, Title = "Bangladesh Labour Act", Year = 2006 });

        var view = Assert.IsType<ViewResult>(await _controller.Review(10));

        var model = Assert.IsType<LawyerReviewViewModel>(view.Model);
        var citation = Assert.Single(model.Citations);
        Assert.Equal("Bangladesh Labour Act", citation.ActTitle);
        Assert.Equal("121", citation.SectionNumber);
        Assert.Equal("Vector", citation.RetrievalMethod);
    }

    [Fact]
    public async Task Review_UnverifiedLawyer_IsSentToStatus()
    {
        _profile.VerificationStatus = VerificationStatus.Rejected;

        Assert.Equal(nameof(LawyerController.Status), RedirectAction(await _controller.Review(10)));
    }

    // ---- SubmitReview -------------------------------------------------------

    [Fact]
    public async Task SubmitReview_Approve_FinalizesDocumentAndReturnsToQueue()
    {
        AddCase(1);
        var doc = AddDocument(10, 1, assignedTo: ProfileId);

        var result = await _controller.SubmitReview(new LawyerReviewViewModel
        {
            DocumentId = 10,
            Decision = nameof(ReviewDecision.Approved),
            Comments = "Looks correct."
        });

        Assert.Equal(nameof(LawyerController.Queue), RedirectAction(result));
        Assert.Equal(DocumentStatus.Approved, doc.Status);
        Assert.Equal("Draft 10", doc.ContentFinal);
        Assert.Equal("Review saved — advancing to the next document.", _controller.TempData["SuccessEn"]);
        _reviewRepo.Verify(r => r.AddAsync(It.Is<LawyerReview>(rv =>
            rv.DocumentId == 10 && rv.LawyerProfileId == ProfileId && rv.Decision == ReviewDecision.Approved)), Times.Once);
    }

    [Fact]
    public async Task SubmitReview_ApproveWithEdits_StoresEditedText()
    {
        AddCase(1);
        var doc = AddDocument(10, 1, assignedTo: ProfileId);

        await _controller.SubmitReview(new LawyerReviewViewModel
        {
            DocumentId = 10,
            Decision = nameof(ReviewDecision.EditedApproved),
            Comments = "Fixed the section reference.",
            EditedContent = "Corrected draft"
        });

        Assert.Equal(DocumentStatus.Approved, doc.Status);
        Assert.Equal("Corrected draft", doc.ContentFinal);
    }

    [Fact]
    public async Task SubmitReview_Reject_MarksDocumentRejected()
    {
        AddCase(1);
        var doc = AddDocument(10, 1, assignedTo: ProfileId);

        var result = await _controller.SubmitReview(new LawyerReviewViewModel
        {
            DocumentId = 10,
            Decision = nameof(ReviewDecision.Rejected),
            Comments = "Wrong district."
        });

        Assert.Equal(nameof(LawyerController.Queue), RedirectAction(result));
        Assert.Equal(DocumentStatus.Rejected, doc.Status);
    }

    [Fact]
    public async Task SubmitReview_MissingComments_ReturnsToWorkspaceWithError()
    {
        var doc = AddDocument(10, 1, assignedTo: ProfileId);

        var result = await _controller.SubmitReview(new LawyerReviewViewModel
        {
            DocumentId = 10,
            Decision = nameof(ReviewDecision.Approved),
            Comments = ""
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(LawyerController.Review), redirect.ActionName);
        Assert.Equal(10, redirect.RouteValues!["id"]);
        Assert.NotNull(_controller.TempData["ErrorEn"]);
        Assert.Equal(DocumentStatus.UnderReview, doc.Status);
        _reviewRepo.Verify(r => r.AddAsync(It.IsAny<LawyerReview>()), Times.Never);
    }

    [Fact]
    public async Task SubmitReview_UnknownDecision_ReshowsWorkspaceAndSavesNothing()
    {
        AddCase(1);
        var doc = AddDocument(10, 1, assignedTo: ProfileId);

        var result = await _controller.SubmitReview(new LawyerReviewViewModel
        {
            DocumentId = 10,
            Decision = "Maybe",
            Comments = "ok",
            EditedContent = "Edited by lawyer"
        });

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(nameof(LawyerController.Review), view.ViewName);
        Assert.True(_controller.ModelState.ContainsKey(nameof(LawyerReviewViewModel.Decision)));
        Assert.Equal("Edited by lawyer", Assert.IsType<LawyerReviewViewModel>(view.Model).EditedContent);
        Assert.Equal(DocumentStatus.UnderReview, doc.Status);
        Assert.Null(doc.ContentFinal);
        _reviewRepo.Verify(r => r.AddAsync(It.IsAny<LawyerReview>()), Times.Never);
    }

    [Fact]
    public async Task SubmitReview_ApproveWithEditsButNoText_ReshowsWorkspaceWithError()
    {
        AddCase(1);
        AddDocument(10, 1, assignedTo: ProfileId);

        var result = await _controller.SubmitReview(new LawyerReviewViewModel
        {
            DocumentId = 10,
            Decision = nameof(ReviewDecision.EditedApproved),
            Comments = "ok",
            EditedContent = "   "
        });

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(nameof(LawyerController.Review), view.ViewName);
        Assert.True(_controller.ModelState.ContainsKey(nameof(LawyerReviewViewModel.EditedContent)));
        Assert.Equal("ok", Assert.IsType<LawyerReviewViewModel>(view.Model).Comments);
        _reviewRepo.Verify(r => r.AddAsync(It.IsAny<LawyerReview>()), Times.Never);
    }

    [Fact]
    public async Task SubmitReview_DocumentHeldByAnotherLawyer_IsRejected()
    {
        var doc = AddDocument(10, 1, assignedTo: 8);

        var result = await _controller.SubmitReview(new LawyerReviewViewModel
        {
            DocumentId = 10,
            Decision = nameof(ReviewDecision.Approved),
            Comments = "ok"
        });

        Assert.Equal(nameof(LawyerController.Review), RedirectAction(result));
        Assert.Equal(DocumentStatus.UnderReview, doc.Status);
    }

    [Fact]
    public async Task SubmitReview_UnverifiedLawyer_IsSentToStatus()
    {
        _profile.VerificationStatus = VerificationStatus.Pending;

        var result = await _controller.SubmitReview(new LawyerReviewViewModel { DocumentId = 10, Comments = "x" });

        Assert.Equal(nameof(LawyerController.Status), RedirectAction(result));
    }

    // ---- History ------------------------------------------------------------

    private void SetUpHistory(params (int reviewId, int docId, string caseTitle, ReviewDecision decision, DateTime at)[] rows)
    {
        var reviews = new List<LawyerReview>();
        foreach (var row in rows)
        {
            var caseId = 1000 + row.reviewId;
            AddCase(caseId, row.caseTitle);
            AddDocument(row.docId, caseId, status: DocumentStatus.Approved);
            reviews.Add(new LawyerReview
            {
                ReviewId = row.reviewId,
                DocumentId = row.docId,
                LawyerProfileId = ProfileId,
                Decision = row.decision,
                Comments = $"Comment {row.reviewId}",
                ReviewedAt = row.at
            });
        }
        _reviewRepo.SetupRows(reviews);
    }

    [Fact]
    public async Task History_NoProfile_ReturnsNotFound()
    {
        _profile.UserId = 999;

        Assert.IsType<NotFoundResult>(await _controller.History(null, null, null, null));
    }

    [Fact]
    public async Task History_DefaultsToNewestFirst()
    {
        SetUpHistory(
            (1, 10, "Beta", ReviewDecision.Approved, new DateTime(2026, 9, 1)),
            (2, 11, "Alpha", ReviewDecision.Rejected, new DateTime(2026, 9, 10)));

        var model = Assert.IsType<LawyerHistoryViewModel>(Assert.IsType<ViewResult>(await _controller.History(null, null, null, null)).Model);

        Assert.Equal(new[] { 2, 1 }, model.Items.Select(i => i.ReviewId));
        Assert.Equal("date_desc", model.Sort);
        Assert.Equal("All", model.ActiveFilter);
        Assert.Equal(2, model.TotalCount);
        Assert.Equal("Rejected", model.Items[0].Decision);
    }

    [Fact]
    public async Task History_SortDateAsc_ReversesOrder()
    {
        SetUpHistory(
            (1, 10, "Beta", ReviewDecision.Approved, new DateTime(2026, 9, 1)),
            (2, 11, "Alpha", ReviewDecision.Approved, new DateTime(2026, 9, 10)));

        var model = Assert.IsType<LawyerHistoryViewModel>(Assert.IsType<ViewResult>(await _controller.History(null, null, null, "date_asc")).Model);

        Assert.Equal(new[] { 1, 2 }, model.Items.Select(i => i.ReviewId));
    }

    [Fact]
    public async Task History_SortCaseAsc_OrdersByTitleIgnoringCase()
    {
        SetUpHistory(
            (1, 10, "charlie", ReviewDecision.Approved, new DateTime(2026, 9, 1)),
            (2, 11, "Alpha", ReviewDecision.Approved, new DateTime(2026, 9, 2)),
            (3, 12, "bravo", ReviewDecision.Approved, new DateTime(2026, 9, 3)));

        var model = Assert.IsType<LawyerHistoryViewModel>(Assert.IsType<ViewResult>(await _controller.History(null, null, null, "case_asc")).Model);

        Assert.Equal(new[] { "Alpha", "bravo", "charlie" }, model.Items.Select(i => i.CaseTitle));
    }

    [Fact]
    public async Task History_DecisionFilter_IsPassedThrough()
    {
        SetUpHistory(
            (1, 10, "A", ReviewDecision.Approved, new DateTime(2026, 9, 1)),
            (2, 11, "B", ReviewDecision.Rejected, new DateTime(2026, 9, 2)));

        var model = Assert.IsType<LawyerHistoryViewModel>(Assert.IsType<ViewResult>(await _controller.History("Rejected", null, null, null)).Model);

        Assert.Equal("Rejected", model.ActiveFilter);
        Assert.Equal(new[] { 2 }, model.Items.Select(i => i.ReviewId));
    }

    [Fact]
    public async Task History_DateRange_IncludesWholeEndDay()
    {
        // Filter dates are Bangladesh days (UTC+6); ReviewedAt is stored in UTC.
        SetUpHistory(
            (1, 10, "Before", ReviewDecision.Approved, new DateTime(2026, 8, 31, 17, 0, 0)), // 31 Aug 23:00 BD
            (2, 11, "Start", ReviewDecision.Approved, new DateTime(2026, 8, 31, 18, 0, 0)),  //  1 Sep 00:00 BD
            (3, 12, "End", ReviewDecision.Approved, new DateTime(2026, 9, 5, 17, 59, 0)),    //  5 Sep 23:59 BD
            (4, 13, "After", ReviewDecision.Approved, new DateTime(2026, 9, 5, 18, 1, 0)));  //  6 Sep 00:01 BD

        var model = Assert.IsType<LawyerHistoryViewModel>(Assert.IsType<ViewResult>(
            await _controller.History(null, "2026-09-01", "2026-09-05", null)).Model);

        Assert.Equal(new[] { "End", "Start" }, model.Items.Select(i => i.CaseTitle));
        Assert.Equal("2026-09-01", model.FromDate);
        Assert.Equal("2026-09-05", model.ToDate);
    }

    [Fact]
    public async Task History_UnparseableDates_AreIgnored()
    {
        SetUpHistory((1, 10, "A", ReviewDecision.Approved, new DateTime(2020, 1, 1)));

        var model = Assert.IsType<LawyerHistoryViewModel>(Assert.IsType<ViewResult>(
            await _controller.History(null, "not-a-date", "also-bad", null)).Model);

        Assert.Single(model.Items);
    }

    [Fact]
    public async Task History_SecondPage_ShowsRemainingItems()
    {
        var rows = Enumerable.Range(1, 23)
            .Select(i => (i, 100 + i, $"Case {i:D2}", ReviewDecision.Approved, new DateTime(2026, 9, 1).AddHours(i)))
            .ToArray();
        SetUpHistory(rows);

        var model = Assert.IsType<LawyerHistoryViewModel>(Assert.IsType<ViewResult>(
            await _controller.History(null, null, null, null, page: 2)).Model);

        Assert.Equal(2, model.Page);
        Assert.Equal(3, model.Items.Count);
        Assert.Equal(23, model.TotalCount);
    }

    // ---- Payments / RequestPayout -------------------------------------------

    private void SetUpEarnings(decimal paidNet, decimal alreadyPaidOut)
    {
        _orderRepo.SetupRows(new List<PaymentOrder>
        {
            new()
            {
                PaymentOrderId = 1, CaseId = 5, LawyerProfileId = ProfileId,
                Purpose = PaymentPurpose.Honorarium, Status = PaymentStatus.Paid,
                Amount = paidNet + 100, Commission = 100, NetToLawyer = paidNet,
                PaidAt = new DateTime(2026, 9, 1)
            },
            new()
            {
                PaymentOrderId = 2, CaseId = 6, LawyerProfileId = ProfileId,
                Purpose = PaymentPurpose.Honorarium, Status = PaymentStatus.Pending,
                Amount = 999, NetToLawyer = 900
            }
        });
        _payoutRepo.SetupRows(new List<PayoutRequest>
        {
            new() { LawyerProfileId = ProfileId, Amount = alreadyPaidOut, IsPaid = true }
        });
    }

    [Fact]
    public async Task Payments_NoProfile_ReturnsNotFound()
    {
        _profile.UserId = 999;

        Assert.IsType<NotFoundResult>(await _controller.Payments());
    }

    [Fact]
    public async Task Payments_ShowsBalanceAndPaidHonorariaOnly()
    {
        SetUpEarnings(paidNet: 900, alreadyPaidOut: 300);

        var model = Assert.IsType<LawyerPaymentsViewModel>(Assert.IsType<ViewResult>(await _controller.Payments()).Model);

        Assert.Equal(600, model.Balance);
        var row = Assert.Single(model.History);
        Assert.Equal(1, row.PaymentOrderId);
        Assert.Equal(5, row.CaseId);
        Assert.Equal(1000, row.Gross);
        Assert.Equal(100, row.Commission);
        Assert.Equal(900, row.Net);
        Assert.Equal("BAR-1001", model.BarRegistrationNumber);
    }

    [Fact]
    public async Task RequestPayout_NoBalance_RedirectsWithErrorAndCreatesNothing()
    {
        SetUpEarnings(paidNet: 500, alreadyPaidOut: 500);

        var result = await _controller.RequestPayout();

        Assert.Equal(nameof(LawyerController.Payments), RedirectAction(result));
        Assert.Equal("No payable balance.", _controller.TempData["ErrorEn"]);
        _payoutRepo.Verify(r => r.AddAsync(It.IsAny<PayoutRequest>()), Times.Never);
    }

    [Fact]
    public async Task RequestPayout_WithBalance_RequestsTheFullBalance()
    {
        SetUpEarnings(paidNet: 1200, alreadyPaidOut: 200);

        var result = await _controller.RequestPayout();

        Assert.Equal(nameof(LawyerController.Payments), RedirectAction(result));
        Assert.NotNull(_controller.TempData["SuccessEn"]);
        _payoutRepo.Verify(r => r.AddAsync(It.Is<PayoutRequest>(p =>
            p.LawyerProfileId == ProfileId && p.Amount == 1000 && !p.IsPaid)), Times.Once);
        _payoutRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task RequestPayout_NoProfile_ReturnsNotFound()
    {
        _profile.UserId = 999;

        Assert.IsType<NotFoundResult>(await _controller.RequestPayout());
    }
}
