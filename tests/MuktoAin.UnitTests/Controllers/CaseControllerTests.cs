using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

// Citizen case flow (/Case): intake form, result page, draft edit/send/
// withdraw (FR-13/21) and tracking (FR-8). CaseService and
// LawyerQueueNotifier are concrete, so they run for real over mocked
// repositories. Result pulls a few repositories from RequestServices, so the
// fixture wires a small ServiceCollection for those. The Submit POST happy
// path goes through ChatService and is covered by the integration suite;
// only its guard clauses are exercised here.
public class CaseControllerTests
{
    private const int UserId = 42;

    private readonly Mock<ICaseRepository> _caseRepo = new();
    private readonly Mock<IRepository<CaseCategory>> _categoryRepo = new();
    private readonly Mock<IRepository<District>> _districtRepo = new();
    private readonly Mock<IRepository<GeneratedDocument>> _docRepo = new();
    private readonly Mock<IRepository<LawyerReview>> _reviewRepo = new();
    private readonly Mock<IRepository<LawyerProfile>> _lawyerProfileRepo = new();
    private readonly Mock<IRepository<Notification>> _notificationRepo = new();
    private readonly Mock<IRepository<AiLog>> _aiLogRepo = new();
    private readonly Mock<IRepository<CaseActReference>> _refRepo = new();
    private readonly Mock<IRepository<ActSection>> _sectionRepo = new();
    private readonly Mock<IRepository<Act>> _actRepo = new();
    private readonly Mock<IModerationService> _moderation = new();
    private readonly Mock<UserManager<User>> _userManager;
    private readonly CaseController _controller;

    public CaseControllerTests()
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

        _categoryRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<CaseCategory>
        {
            new() { CategoryId = 3, Name = "RTI Request" },
            new() { CategoryId = 1, Name = "Labour Complaint" },
            new() { CategoryId = 2, Name = "General Diary" }
        });
        _categoryRepo.Setup(r => r.GetByIdAsync(It.IsAny<object>()))
            .ReturnsAsync(new CaseCategory { CategoryId = 1, Name = "Labour Complaint" });
        _districtRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<District>
        {
            new() { DistrictId = 2, Name = "Sylhet" },
            new() { DistrictId = 1, Name = "Dhaka" },
            new() { DistrictId = 3, Name = "Chattogram" }
        });
        _districtRepo.Setup(r => r.GetByIdAsync(It.IsAny<object>()))
            .ReturnsAsync(new District { DistrictId = 1, Name = "Dhaka" });

        _caseRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Case>());
        _caseRepo.Setup(r => r.GetByUserIdAsync(It.IsAny<int>())).ReturnsAsync(new List<Case>());
        _reviewRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LawyerReview>());
        _lawyerProfileRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LawyerProfile>());
        _notificationRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Notification>());
        _aiLogRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<AiLog>());
        _refRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<CaseActReference>());
        _sectionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ActSection>());
        _actRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Act>());
        _moderation.Setup(m => m.IsContentAppropriate(It.IsAny<string?>())).Returns(true);

        _controller = NewController(UserId.ToString());
    }

    private CaseController NewController(string? userId, string? role = null)
    {
        var encryption = new Mock<IEncryptionService>();
        encryption.Setup(e => e.Decrypt(It.IsAny<string>())).Returns<string>(s => s);
        var caseService = new CaseService(
            _caseRepo.Object, _categoryRepo.Object, _districtRepo.Object, encryption.Object, _notificationRepo.Object);
        var notifier = new LawyerQueueNotifier(_lawyerProfileRepo.Object, _notificationRepo.Object);

        var services = new ServiceCollection()
            .AddSingleton(_userManager.Object)
            .AddSingleton(_aiLogRepo.Object)
            .AddSingleton(_refRepo.Object)
            .AddSingleton(_sectionRepo.Object)
            .AddSingleton(_actRepo.Object)
            .BuildServiceProvider();

        var claims = new List<Claim>();
        if (userId != null) claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        if (role != null) claims.Add(new Claim(ClaimTypes.Role, role));
        var httpContext = new DefaultHttpContext
        {
            Session = new TestSession(),
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, userId == null ? null : "test"))
        };

        return new CaseController(
            caseService,
            Mock.Of<IRightsExplanationService>(),
            null!, // DocumentService -- only used to generate a missing draft on legacy cases
            _caseRepo.Object,
            _categoryRepo.Object,
            _districtRepo.Object,
            _docRepo.Object,
            _reviewRepo.Object,
            _lawyerProfileRepo.Object,
            _moderation.Object,
            _notificationRepo.Object,
            notifier)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>()),
            Url = Mock.Of<IUrlHelper>()
        };
    }

    // Case owned by the signed-in user (42) with one draft document.
    private (Case c, GeneratedDocument doc) OwnedCase(
        int caseId = 5,
        CaseStatus caseStatus = CaseStatus.Submitted,
        DocumentStatus docStatus = DocumentStatus.Draft)
    {
        var doc = new GeneratedDocument
        {
            DocumentId = 50,
            CaseId = caseId,
            Status = docStatus,
            ContentDraft = "Original AI draft",
            VersionNo = 1,
            CreatedAt = DateTime.UtcNow
        };
        var c = new Case
        {
            CaseId = caseId,
            UserId = UserId,
            Title = "Unpaid wages",
            Description = "Employer withheld three months of pay.",
            CategoryId = 1,
            DistrictId = 1,
            Status = caseStatus,
            CreatedAt = new DateTime(2026, 9, 1),
            Documents = new List<GeneratedDocument> { doc }
        };
        _caseRepo.Setup(r => r.GetWithDocumentsAsync(caseId)).ReturnsAsync(c);
        _caseRepo.Setup(r => r.GetByIdAsync(caseId)).ReturnsAsync(c);
        return (c, doc);
    }

    // Anonymous case reachable only with its tracking code.
    private Case AnonymousCase(int caseId, string code)
    {
        var c = new Case
        {
            CaseId = caseId,
            IsAnonymous = true,
            UserId = null,
            AnonymousTrackingCode = code,
            Title = "Anonymous GD",
            CategoryId = 2,
            DistrictId = 1,
            Status = CaseStatus.Submitted,
            Documents = new List<GeneratedDocument>
            {
                new() { DocumentId = 70, CaseId = caseId, Status = DocumentStatus.Draft, ContentDraft = "GD draft" }
            }
        };
        _caseRepo.Setup(r => r.GetWithDocumentsAsync(caseId)).ReturnsAsync(c);
        _caseRepo.Setup(r => r.GetByIdAsync(caseId)).ReturnsAsync(c);
        return c;
    }

    private static CaseResultViewModel ResultModel(IActionResult result) =>
        Assert.IsType<CaseResultViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static RedirectToActionResult Redirect(IActionResult result) =>
        Assert.IsType<RedirectToActionResult>(result);

    // ---- attributes ---------------------------------------------------------

    [Theory]
    [InlineData(nameof(CaseController.SaveDraft))]
    [InlineData(nameof(CaseController.SendToLawyer))]
    [InlineData(nameof(CaseController.Withdraw))]
    public void PostActions_CarryValidateAntiForgeryToken(string actionName)
    {
        var method = typeof(CaseController).GetMethod(actionName)!;
        Assert.NotEmpty(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true));
        Assert.NotEmpty(method.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true));
    }

    [Fact]
    public void SubmitPost_CarriesValidateAntiForgeryToken()
    {
        var method = typeof(CaseController).GetMethods()
            .Single(m => m.Name == nameof(CaseController.Submit)
                         && m.GetParameters().Length == 1
                         && m.GetParameters()[0].ParameterType == typeof(CaseSubmitViewModel));
        Assert.NotEmpty(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true));
    }

    // ---- Submit (GET) / SubmitOptions ----------------------------------------

    [Fact]
    public async Task SubmitGet_PopulatesCategoriesByIdAndDistrictsByName()
    {
        var result = await _controller.Submit(cat: null, q: null);

        var model = Assert.IsType<CaseSubmitViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(new[] { "1", "2", "3" }, model.Categories.Select(c => c.Value));
        Assert.Equal("Labour Complaint", model.Categories[0].Text);
        Assert.Equal(new[] { "Chattogram", "Dhaka", "Sylhet" }, model.Districts.Select(d => d.Text));
        Assert.Equal(new[] { "3", "1", "2" }, model.Districts.Select(d => d.Value));
    }

    [Fact]
    public async Task SubmitGet_PrefillsCategoryAndDescriptionFromQuery()
    {
        var result = await _controller.Submit(cat: 2, q: "Police refused to record my GD");

        var model = Assert.IsType<CaseSubmitViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(2, model.CategoryId);
        Assert.Equal("Police refused to record my GD", model.Description);
    }

    [Fact]
    public async Task SubmitGet_BlankQuery_LeavesDescriptionEmpty()
    {
        var model = Assert.IsType<CaseSubmitViewModel>(Assert.IsType<ViewResult>(await _controller.Submit(null, "   ")).Model);

        Assert.Equal(string.Empty, model.Description);
    }

    [Fact]
    public async Task SubmitOptions_ReturnsDistrictsSortedByName()
    {
        var result = await _controller.SubmitOptions();

        var json = Assert.IsType<JsonResult>(result);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(json.Value));
        var districts = doc.RootElement.GetProperty("districts").EnumerateArray().ToList();
        Assert.Equal(new[] { "Chattogram", "Dhaka", "Sylhet" }, districts.Select(d => d.GetProperty("name").GetString()));
        Assert.Equal(3, districts[0].GetProperty("id").GetInt32());
    }

    // ---- Submit (POST) guard clauses ----------------------------------------

    [Fact]
    public async Task SubmitPost_InvalidModel_RedisplaysFormWithDropdowns()
    {
        _controller.ModelState.AddModelError(nameof(CaseSubmitViewModel.Title), "Required");
        var vm = new CaseSubmitViewModel { Description = "Something" };

        var result = await _controller.Submit(vm);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(vm, view.Model);
        Assert.Equal(3, vm.Categories.Count);
        Assert.Equal(3, vm.Districts.Count);
        _moderation.Verify(m => m.IsContentAppropriate(It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SubmitPost_InappropriateTitle_IsBlockedWithModelError()
    {
        _moderation.Setup(m => m.IsContentAppropriate("bad title")).Returns(false);
        var vm = new CaseSubmitViewModel { Title = "bad title", Description = "fine", CategoryId = 1, DistrictId = 1 };

        var result = await _controller.Submit(vm);

        Assert.IsType<ViewResult>(result);
        Assert.False(_controller.ModelState.IsValid);
        Assert.Contains("inappropriate content", _controller.ModelState[string.Empty]!.Errors.Single().ErrorMessage);
        Assert.NotEmpty(vm.Categories);
        _caseRepo.Verify(r => r.AddAsync(It.IsAny<Case>()), Times.Never);
    }

    [Fact]
    public async Task SubmitPost_InappropriateDescription_IsBlocked()
    {
        _moderation.Setup(m => m.IsContentAppropriate("abusive text")).Returns(false);
        var vm = new CaseSubmitViewModel { Title = "fine", Description = "abusive text", CategoryId = 1, DistrictId = 1 };

        var result = await _controller.Submit(vm);

        Assert.IsType<ViewResult>(result);
        Assert.False(_controller.ModelState.IsValid);
    }

    // ---- Result -------------------------------------------------------------

    [Fact]
    public async Task Result_UnknownCase_ReturnsNotFound()
    {
        Assert.IsType<NotFoundResult>(await _controller.Result(404, null));
    }

    [Fact]
    public async Task Result_SomeoneElsesCase_ReturnsNotFound()
    {
        var (c, _) = OwnedCase();
        c.UserId = 777;

        Assert.IsType<NotFoundResult>(await _controller.Result(5, null));
    }

    [Fact]
    public async Task Result_OwnedDraftCase_MapsCaseAndDocument()
    {
        OwnedCase();

        var model = ResultModel(await _controller.Result(5, null));

        Assert.Equal(5, model.CaseId);
        Assert.Equal("Unpaid wages", model.Title);
        Assert.Equal("Submitted", model.Status);
        Assert.Equal("Labour Complaint", model.CategoryName);
        Assert.Equal("Dhaka", model.DistrictName);
        Assert.Equal(50, model.DocumentId);
        Assert.Equal("Original AI draft", model.DocumentContent);
        Assert.Equal("Draft", model.DocumentStatus);
        Assert.True(model.CanEdit);
        Assert.False(model.CanDownloadPdf);
        Assert.Equal("DraftReady", model.TimelineCurrent);
        Assert.Equal(string.Empty, model.TrackingCode);
    }

    [Fact]
    public async Task Result_UsesLatestDocumentWhenSeveralExist()
    {
        var (c, _) = OwnedCase();
        c.Documents.Add(new GeneratedDocument { DocumentId = 51, CaseId = 5, Status = DocumentStatus.Draft, ContentDraft = "Newer draft" });

        var model = ResultModel(await _controller.Result(5, null));

        Assert.Equal(51, model.DocumentId);
        Assert.Equal("Newer draft", model.DocumentContent);
    }

    [Theory]
    [InlineData(CaseStatus.Submitted, DocumentStatus.Draft, "DraftReady", true, false)]
    [InlineData(CaseStatus.UnderReview, DocumentStatus.UnderReview, "UnderReview", false, false)]
    [InlineData(CaseStatus.Finalized, DocumentStatus.Approved, "Approved", false, true)]
    [InlineData(CaseStatus.UnderReview, DocumentStatus.Rejected, "Rejected", true, false)]
    public async Task Result_TimelineAndPermissionsFollowStatus(
        CaseStatus caseStatus, DocumentStatus docStatus, string timeline, bool canEdit, bool canDownload)
    {
        OwnedCase(caseStatus: caseStatus, docStatus: docStatus);

        var model = ResultModel(await _controller.Result(5, null));

        Assert.Equal(timeline, model.TimelineCurrent);
        Assert.Equal(canEdit, model.CanEdit);
        Assert.Equal(canDownload, model.CanDownloadPdf);
    }

    [Fact]
    public async Task Result_UsesLatestRightsExplanationFromAiLog()
    {
        OwnedCase();
        _aiLogRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<AiLog>
        {
            new() { CaseId = 5, RequestType = AiRequestType.RightsExplanation, ResponseText = "old", CreatedAt = new DateTime(2026, 9, 1) },
            new() { CaseId = 5, RequestType = AiRequestType.RightsExplanation, ResponseText = "latest", CreatedAt = new DateTime(2026, 9, 3) },
            new() { CaseId = 6, RequestType = AiRequestType.RightsExplanation, ResponseText = "other case", CreatedAt = new DateTime(2026, 9, 9) }
        });

        var model = ResultModel(await _controller.Result(5, null));

        Assert.Equal("latest", model.RightsExplanation);
    }

    [Fact]
    public async Task Result_NoRightsExplanationYet_ShowsPreparingPlaceholder()
    {
        OwnedCase();

        var model = ResultModel(await _controller.Result(5, null));

        Assert.Equal("আইনি অধিকার বিশ্লেষণ প্রস্তুত হচ্ছে...", model.RightsExplanation);
    }

    [Fact]
    public async Task Result_MapsCitedSectionsWithActTitleAndPercentScore()
    {
        OwnedCase();
        _refRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<CaseActReference>
        {
            new() { CaseId = 5, SectionId = 100, RelevanceScore = 0.873m },
            new() { CaseId = 5, SectionId = 101, RelevanceScore = 0.5m },
            new() { CaseId = 9, SectionId = 100, RelevanceScore = 0.99m }
        });
        _sectionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ActSection>
        {
            new() { SectionId = 100, ActId = 1, SectionNumber = "121", SectionText = "Wages shall be paid..." },
            new() { SectionId = 101, ActId = 1, SectionNumber = null, SectionText = "Preamble" }
        });
        _actRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Act> { new() { ActId = 1, Title = "Bangladesh Labour Act" } });

        var model = ResultModel(await _controller.Result(5, null));

        Assert.Equal(2, model.CitedSections.Count);
        Assert.Equal("Bangladesh Labour Act", model.CitedSections[0].ActTitle);
        Assert.Equal("ধারা 121", model.CitedSections[0].SectionNumber);
        Assert.Equal("87%", model.CitedSections[0].RelevanceScore);
        Assert.Equal(string.Empty, model.CitedSections[1].SectionNumber);
        Assert.Equal("50%", model.CitedSections[1].RelevanceScore);
    }

    [Fact]
    public async Task Result_CitedSectionMissingFromCorpus_RendersBlankFields()
    {
        OwnedCase();
        _refRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<CaseActReference>
        {
            new() { CaseId = 5, SectionId = 999, RelevanceScore = 0.4m }
        });

        var cited = Assert.Single(ResultModel(await _controller.Result(5, null)).CitedSections);

        Assert.Equal(string.Empty, cited.ActTitle);
        Assert.Equal(string.Empty, cited.SectionText);
        Assert.Equal("40%", cited.RelevanceScore);
    }

    [Fact]
    public async Task Result_RejectedDocument_ShowsLawyerAndRejectionReason()
    {
        var (_, doc) = OwnedCase(caseStatus: CaseStatus.UnderReview, docStatus: DocumentStatus.Rejected);
        doc.AssignedLawyerProfileId = 8;
        _reviewRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LawyerReview>
        {
            new() { ReviewId = 1, DocumentId = 50, LawyerProfileId = 8, Decision = ReviewDecision.Rejected, Comments = "Wrong district named." }
        });
        _lawyerProfileRepo.Setup(r => r.GetByIdAsync(8)).ReturnsAsync(new LawyerProfile { LawyerProfileId = 8, UserId = 300, BarRegistrationNumber = "BAR-8" });
        _userManager.Setup(m => m.FindByIdAsync("300")).ReturnsAsync(new User { Id = 300, FullName = "Adv. Karim" });

        var model = ResultModel(await _controller.Result(5, null));

        Assert.Equal("Adv. Karim", model.LawyerName);
        Assert.Equal("BAR-8", model.LawyerBarNumber);
        Assert.Equal("Rejected", model.LawyerDecision);
        Assert.Equal("Wrong district named.", model.LawyerComments);
        Assert.Equal("Wrong district named.", model.RejectionReason);
    }

    [Fact]
    public async Task Result_ApprovedDocument_HasNoRejectionReason()
    {
        OwnedCase(caseStatus: CaseStatus.Finalized, docStatus: DocumentStatus.Approved);
        _reviewRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LawyerReview>
        {
            new() { ReviewId = 1, DocumentId = 50, LawyerProfileId = 8, Decision = ReviewDecision.Approved, Comments = "Good to file." }
        });
        _lawyerProfileRepo.Setup(r => r.GetByIdAsync(8)).ReturnsAsync(new LawyerProfile { LawyerProfileId = 8, UserId = 300, BarRegistrationNumber = "BAR-8" });

        var model = ResultModel(await _controller.Result(5, null));

        Assert.Null(model.RejectionReason);
        Assert.Equal("Good to file.", model.LawyerComments);
        Assert.Equal("BAR-8", model.LawyerBarNumber);
    }

    [Fact]
    public async Task Result_NoReviewYet_HasNoLawyerDetails()
    {
        OwnedCase();

        var model = ResultModel(await _controller.Result(5, null));

        Assert.Null(model.LawyerName);
        Assert.Null(model.LawyerDecision);
    }

    [Fact]
    public async Task Result_AnonymousCaseWithoutCode_ReturnsNotFound()
    {
        AnonymousCase(8, "secret-code");

        Assert.IsType<NotFoundResult>(await NewController(userId: null).Result(8, null));
    }

    [Fact]
    public async Task Result_AnonymousCaseWithWrongCode_ReturnsNotFound()
    {
        AnonymousCase(8, "secret-code");

        Assert.IsType<NotFoundResult>(await NewController(userId: null).Result(8, "guess"));
    }

    [Fact]
    public async Task Result_AnonymousCaseWithCodeInQuery_IsShown()
    {
        AnonymousCase(8, "secret-code");

        var model = ResultModel(await NewController(userId: null).Result(8, "secret-code"));

        Assert.Equal("secret-code", model.TrackingCode);
        Assert.Equal(70, model.DocumentId);
    }

    [Fact]
    public async Task Result_AnonymousCaseWithCodeRememberedInSession_IsShown()
    {
        AnonymousCase(8, "secret-code");
        var guest = NewController(userId: null);
        TrackedCases.Remember(guest.HttpContext.Session, 8, "secret-code");

        var model = ResultModel(await guest.Result(8, null));

        Assert.Equal(8, model.CaseId);
    }

    [Fact]
    public async Task Result_AnonymousCaseWithCodeInTempData_IsShown()
    {
        AnonymousCase(8, "secret-code");
        var guest = NewController(userId: null);
        guest.TempData["TrackingCode"] = "secret-code";

        var model = ResultModel(await guest.Result(8, null));

        Assert.Equal(8, model.CaseId);
    }

    [Fact]
    public async Task Result_LawyerCanViewAnyCase()
    {
        var (c, _) = OwnedCase();
        c.UserId = 777;

        var model = ResultModel(await NewController("300", role: nameof(UserRole.Lawyer)).Result(5, null));

        Assert.Equal(5, model.CaseId);
    }

    // ---- SaveDraft ------------------------------------------------------------

    [Fact]
    public async Task SaveDraft_UnknownCase_ReturnsNotFound()
    {
        Assert.IsType<NotFoundResult>(await _controller.SaveDraft(404, "text", null));
    }

    [Theory]
    [InlineData(DocumentStatus.UnderReview)]
    [InlineData(DocumentStatus.Approved)]
    public async Task SaveDraft_LockedDocument_IsForbidden(DocumentStatus status)
    {
        OwnedCase(docStatus: status);

        Assert.IsType<ForbidResult>(await _controller.SaveDraft(5, "edited", null));
        _docRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task SaveDraft_NewContent_BumpsVersionAndMarksCitizenEdited()
    {
        var (_, doc) = OwnedCase();

        var result = await _controller.SaveDraft(5, "My corrected draft", null);

        var redirect = Redirect(result);
        Assert.Equal(nameof(CaseController.Result), redirect.ActionName);
        Assert.Equal(5, redirect.RouteValues!["id"]);
        Assert.Equal("My corrected draft", doc.ContentFinal);
        Assert.Equal(2, doc.VersionNo);
        Assert.True(doc.CitizenEdited);
        Assert.Equal("Draft saved (version 2).", _controller.TempData["SuccessEn"]);
        _docRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task SaveDraft_RejectedDocument_CanStillBeEdited()
    {
        var (_, doc) = OwnedCase(caseStatus: CaseStatus.UnderReview, docStatus: DocumentStatus.Rejected);

        await _controller.SaveDraft(5, "Fixed after rejection", null);

        Assert.Equal("Fixed after rejection", doc.ContentFinal);
    }

    [Fact]
    public async Task SaveDraft_SameAsCurrentFinal_DoesNotBumpVersion()
    {
        var (_, doc) = OwnedCase();
        doc.ContentFinal = "Already saved";
        doc.VersionNo = 3;

        await _controller.SaveDraft(5, "Already saved", null);

        Assert.Equal(3, doc.VersionNo);
        _docRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveDraft_BlankContent_IsIgnored(string content)
    {
        var (_, doc) = OwnedCase();

        await _controller.SaveDraft(5, content, null);

        Assert.Null(doc.ContentFinal);
        Assert.Equal(1, doc.VersionNo);
        _docRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task SaveDraft_ContentMatchingOriginalExceptWhitespace_IsNotCitizenEdited()
    {
        var (_, doc) = OwnedCase();

        await _controller.SaveDraft(5, "  Original AI draft \n", null);

        Assert.Equal(2, doc.VersionNo);
        Assert.False(doc.CitizenEdited);
    }

    [Fact]
    public async Task SaveDraft_KeepsTrackingCodeOnRedirect()
    {
        AnonymousCase(8, "secret-code");

        var redirect = Redirect(await NewController(userId: null).SaveDraft(8, "edit", "secret-code"));

        Assert.Equal("secret-code", redirect.RouteValues!["code"]);
    }

    // ---- SendToLawyer ---------------------------------------------------------

    [Fact]
    public async Task SendToLawyer_UnknownCase_ReturnsNotFound()
    {
        Assert.IsType<NotFoundResult>(await _controller.SendToLawyer(404, null));
    }

    [Fact]
    public async Task SendToLawyer_Draft_MovesDocumentAndCaseIntoReview()
    {
        var (c, doc) = OwnedCase();

        var result = await _controller.SendToLawyer(5, null);

        Assert.Equal(nameof(CaseController.Result), Redirect(result).ActionName);
        Assert.Equal(DocumentStatus.UnderReview, doc.Status);
        Assert.Equal(CaseStatus.UnderReview, c.Status);
        Assert.Equal("Your draft was sent to the lawyer pool.", _controller.TempData["SuccessEn"]);
    }

    [Fact]
    public async Task SendToLawyer_NotifiesVerifiedLawyersInMatchingField()
    {
        OwnedCase();
        _lawyerProfileRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LawyerProfile>
        {
            new() { LawyerProfileId = 1, UserId = 101, Specialization = "Labour and employment", VerificationStatus = VerificationStatus.Approved },
            new() { LawyerProfileId = 2, UserId = 102, Specialization = "Criminal", VerificationStatus = VerificationStatus.Approved },
            new() { LawyerProfileId = 3, UserId = 103, Specialization = "Labour", VerificationStatus = VerificationStatus.Pending }
        });

        await _controller.SendToLawyer(5, null);

        _notificationRepo.Verify(r => r.AddAsync(It.Is<Notification>(n =>
            n.UserId == 101 && n.Type == NotificationType.NewCaseInQueue && n.RelatedCaseId == 5 && n.RelatedDocumentId == 50)), Times.Once);
        _notificationRepo.Verify(r => r.AddAsync(It.Is<Notification>(n => n.UserId == 102 || n.UserId == 103)), Times.Never);
    }

    [Fact]
    public async Task SendToLawyer_ResubmittedRejectedDraft_NotifiesOnlyHoldingLawyer()
    {
        var (_, doc) = OwnedCase(caseStatus: CaseStatus.UnderReview, docStatus: DocumentStatus.Rejected);
        doc.AssignedLawyerProfileId = 2;
        _lawyerProfileRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LawyerProfile>
        {
            new() { LawyerProfileId = 1, UserId = 101, Specialization = "Labour", VerificationStatus = VerificationStatus.Approved },
            new() { LawyerProfileId = 2, UserId = 102, Specialization = "Labour", VerificationStatus = VerificationStatus.Approved }
        });

        await _controller.SendToLawyer(5, null);

        Assert.Equal(DocumentStatus.UnderReview, doc.Status);
        _notificationRepo.Verify(r => r.AddAsync(It.Is<Notification>(n =>
            n.UserId == 102 && n.Type == NotificationType.DocumentResubmitted)), Times.Once);
        _notificationRepo.Verify(r => r.AddAsync(It.Is<Notification>(n => n.UserId == 101)), Times.Never);
    }

    [Theory]
    [InlineData(DocumentStatus.UnderReview)]
    [InlineData(DocumentStatus.Approved)]
    public async Task SendToLawyer_DocumentNotEditable_IsForbidden(DocumentStatus status)
    {
        OwnedCase(docStatus: status);

        Assert.IsType<ForbidResult>(await _controller.SendToLawyer(5, null));
        _docRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task SendToLawyer_CaseWithoutDocument_ReturnsNotFound()
    {
        var (c, _) = OwnedCase();
        c.Documents.Clear();

        Assert.IsType<NotFoundResult>(await _controller.SendToLawyer(5, null));
    }

    // ---- Withdraw -------------------------------------------------------------

    [Fact]
    public async Task Withdraw_UnknownCase_ReturnsNotFound()
    {
        Assert.IsType<NotFoundResult>(await _controller.Withdraw(404, null));
    }

    [Fact]
    public async Task Withdraw_WhileLawyerHoldsDocument_IsForbidden()
    {
        var (c, _) = OwnedCase(caseStatus: CaseStatus.UnderReview, docStatus: DocumentStatus.UnderReview);

        Assert.IsType<ForbidResult>(await _controller.Withdraw(5, null));
        Assert.Equal(CaseStatus.UnderReview, c.Status);
    }

    [Fact]
    public async Task Withdraw_RejectedDraft_FinalizesCase()
    {
        var (c, _) = OwnedCase(caseStatus: CaseStatus.UnderReview, docStatus: DocumentStatus.Rejected);

        var result = await _controller.Withdraw(5, null);

        Assert.Equal(nameof(CaseController.Result), Redirect(result).ActionName);
        Assert.Equal(CaseStatus.Finalized, c.Status);
        Assert.Equal("Case withdrawn — you can still view it anytime.", _controller.TempData["SuccessEn"]);
    }

    [Fact]
    public async Task Withdraw_FromSubmitted_IsNotAValidTransition()
    {
        var (c, _) = OwnedCase(caseStatus: CaseStatus.Submitted, docStatus: DocumentStatus.Draft);

        Assert.IsType<ForbidResult>(await _controller.Withdraw(5, null));
        Assert.Equal(CaseStatus.Submitted, c.Status);
    }

    // ---- Track ----------------------------------------------------------------

    [Fact]
    public async Task Track_ValidCode_RedirectsToThatCase()
    {
        _caseRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Case>
        {
            new() { CaseId = 8, AnonymousTrackingCode = "abc123" }
        });

        var result = await NewController(userId: null).Track(null, "  abc123  ");

        var redirect = Redirect(result);
        Assert.Equal(nameof(CaseController.Result), redirect.ActionName);
        Assert.Equal(8, redirect.RouteValues!["id"]);
        Assert.Equal("abc123", redirect.RouteValues!["code"]);
    }

    [Fact]
    public async Task Track_UnknownCode_ShowsErrorAndList()
    {
        var guest = NewController(userId: null);

        var result = await guest.Track(null, "nope");

        var model = Assert.IsType<CaseTrackViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Code did not match — try again.", guest.TempData["ErrorEn"]);
        Assert.Equal("nope", model.LookupCode);
        Assert.Empty(model.Cases);
    }

    [Fact]
    public async Task Track_SignedInUser_ListsOwnCasesWithUnreadFlag()
    {
        _caseRepo.Setup(r => r.GetByUserIdAsync(UserId)).ReturnsAsync(new List<Case>
        {
            new() { CaseId = 1, UserId = UserId, Title = "First", Status = CaseStatus.Submitted },
            new() { CaseId = 2, UserId = UserId, Title = "Second", Status = CaseStatus.Finalized }
        });
        _notificationRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Notification>
        {
            new() { UserId = UserId, Type = NotificationType.DocumentDecided, RelatedCaseId = 2, IsRead = false },
            new() { UserId = UserId, Type = NotificationType.DocumentDecided, RelatedCaseId = 1, IsRead = true },
            new() { UserId = 999, Type = NotificationType.DocumentDecided, RelatedCaseId = 1, IsRead = false }
        });

        var model = Assert.IsType<CaseTrackViewModel>(Assert.IsType<ViewResult>(await _controller.Track(null, null)).Model);

        Assert.Equal(2, model.TotalCount);
        Assert.False(model.Cases.Single(c => c.CaseId == 1).HasUnread);
        Assert.True(model.Cases.Single(c => c.CaseId == 2).HasUnread);
        Assert.Equal("All", model.ActiveStatusFilter);
    }

    [Fact]
    public async Task Track_ApprovedFilter_ShowsFinalizedCases()
    {
        _caseRepo.Setup(r => r.GetByUserIdAsync(UserId)).ReturnsAsync(new List<Case>
        {
            new() { CaseId = 1, UserId = UserId, Status = CaseStatus.Submitted },
            new() { CaseId = 2, UserId = UserId, Status = CaseStatus.Finalized },
            new() { CaseId = 3, UserId = UserId, Status = CaseStatus.UnderReview }
        });

        var model = Assert.IsType<CaseTrackViewModel>(Assert.IsType<ViewResult>(await _controller.Track("Approved", null)).Model);

        Assert.Equal(new[] { 2 }, model.Cases.Select(c => c.CaseId));
        Assert.Equal("Approved", model.ActiveStatusFilter);
    }

    [Fact]
    public async Task Track_UnderReviewFilter_MatchesStatusDirectly()
    {
        _caseRepo.Setup(r => r.GetByUserIdAsync(UserId)).ReturnsAsync(new List<Case>
        {
            new() { CaseId = 1, UserId = UserId, Status = CaseStatus.Submitted },
            new() { CaseId = 3, UserId = UserId, Status = CaseStatus.UnderReview }
        });

        var model = Assert.IsType<CaseTrackViewModel>(Assert.IsType<ViewResult>(await _controller.Track("UnderReview", null)).Model);

        Assert.Equal(new[] { 3 }, model.Cases.Select(c => c.CaseId));
    }

    [Fact]
    public async Task Track_PagesTenAtATimeAndClampsPage()
    {
        _caseRepo.Setup(r => r.GetByUserIdAsync(UserId)).ReturnsAsync(
            Enumerable.Range(1, 23).Select(i => new Case { CaseId = i, UserId = UserId, Status = CaseStatus.Submitted }).ToList());

        var page2 = Assert.IsType<CaseTrackViewModel>(Assert.IsType<ViewResult>(await _controller.Track(null, null, page: 2)).Model);
        var beyond = Assert.IsType<CaseTrackViewModel>(Assert.IsType<ViewResult>(await _controller.Track(null, null, page: 99)).Model);
        var negative = Assert.IsType<CaseTrackViewModel>(Assert.IsType<ViewResult>(await _controller.Track(null, null, page: -3)).Model);

        Assert.Equal(Enumerable.Range(11, 10), page2.Cases.Select(c => c.CaseId));
        Assert.Equal(23, page2.TotalCount);
        Assert.Equal(3, beyond.Page);
        Assert.Equal(3, beyond.Cases.Count);
        Assert.Equal(1, negative.Page);
    }

    [Fact]
    public async Task Track_Guest_ListsCasesRememberedInSession()
    {
        AnonymousCase(8, "code-8");
        AnonymousCase(9, "code-9");
        var guest = NewController(userId: null);
        TrackedCases.Remember(guest.HttpContext.Session, 8, "code-8");
        TrackedCases.Remember(guest.HttpContext.Session, 9, "wrong-code");

        var model = Assert.IsType<CaseTrackViewModel>(Assert.IsType<ViewResult>(await guest.Track(null, null)).Model);

        var item = Assert.Single(model.Cases);
        Assert.Equal(8, item.CaseId);
        Assert.Equal("code-8", item.TrackingCode);
        _caseRepo.Verify(r => r.GetByUserIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Track_SessionCaseAlreadyOwned_IsNotListedTwice()
    {
        _caseRepo.Setup(r => r.GetByUserIdAsync(UserId)).ReturnsAsync(new List<Case>
        {
            new() { CaseId = 8, UserId = UserId, Status = CaseStatus.Submitted }
        });
        TrackedCases.Remember(_controller.HttpContext.Session, 8, "code-8");

        var model = Assert.IsType<CaseTrackViewModel>(Assert.IsType<ViewResult>(await _controller.Track(null, null)).Model);

        Assert.Single(model.Cases);
    }
}
