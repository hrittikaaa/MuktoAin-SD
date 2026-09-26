using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using MuktoAin.Application.DTOs;
using MuktoAin.Application.Services;
using MuktoAin.Domain.Entities;
using MuktoAin.Domain.Enums;
using MuktoAin.Domain.Interfaces.Repositories;
using MuktoAin.Web.ViewModels;

namespace MuktoAin.Web.Controllers;

// FR-13/14/15/23. Review is LAWYER-ONLY (admin manages, never reviews).
[Authorize(Roles = "Lawyer")]
public class LawyerController : Controller
{
    private readonly LawyerReviewService _reviewService;
    private readonly PaymentService _paymentService;
    private readonly IRepository<LawyerProfile> _profileRepo;
    private readonly UserManager<User> _userManager;

    public LawyerController(
        LawyerReviewService reviewService,
        PaymentService paymentService,
        IRepository<LawyerProfile> profileRepo,
        UserManager<User> userManager)
    {
        _reviewService = reviewService;
        _paymentService = paymentService;
        _profileRepo = profileRepo;
        _userManager = userManager;
    }

    private async Task<LawyerProfile?> MyProfileAsync()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(idStr, out var userId)) return null;
        return (await _profileRepo.FindAsync(p => p.UserId == userId)).FirstOrDefault();
    }

    // The sign-in cookie already carries the display name (FullName claim);
    // fall back to the user record only when it is missing.
    private async Task<string> MyNameAsync(LawyerProfile profile) =>
        User.FindFirst("FullName")?.Value
        ?? (await _userManager.FindByIdAsync(profile.UserId.ToString()))?.FullName
        ?? "";

    // Unverified lawyers land here instead of the queue.
    [HttpGet]
    public async Task<IActionResult> Status()
    {
        var profile = await MyProfileAsync();
        if (profile == null) return NotFound();
        var vm = new LawyerStatusViewModel
        {
            LawyerName = await MyNameAsync(profile),
            BarRegistrationNumber = profile.BarRegistrationNumber,
            Specialization = profile.Specialization ?? "",
            Status = profile.VerificationStatus.ToString(),
            RejectionReason = profile.RejectionReason
        };
        return View(vm);
    }

    // Rejected lawyers resubmit their bar number from the Status page.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resubmit(LawyerStatusViewModel vm)
    {
        var profile = await MyProfileAsync();
        if (profile == null) return NotFound();
        if (profile.VerificationStatus != VerificationStatus.Rejected) return Forbid();
        if (string.IsNullOrWhiteSpace(vm.BarRegistrationNumber))
        {
            // ModelState does not survive the redirect, so surface it as a toast.
            TempData["Error"] = "বার নম্বর আবশ্যক।";
            TempData["ErrorEn"] = "Bar number required.";
            return RedirectToAction(nameof(Status));
        }

        // #5: BarRegistrationNumber is UNIQUE (and NVARCHAR(100)) -- refuse a
        // number another lawyer holds or one that won't fit, instead of a 500.
        var barNumber = vm.BarRegistrationNumber.Trim();
        if (barNumber.Length > MaxBarNumberLength)
        {
            TempData["Error"] = $"বার নম্বর সর্বোচ্চ {MaxBarNumberLength} অক্ষরের হতে পারে।";
            TempData["ErrorEn"] = $"Bar number can be at most {MaxBarNumberLength} characters.";
            return RedirectToAction(nameof(Status));
        }
        var takenByOther = (await _profileRepo.FindAsync(p =>
            p.BarRegistrationNumber == barNumber && p.LawyerProfileId != profile.LawyerProfileId)).Count > 0;
        if (takenByOther)
        {
            TempData["Error"] = "এই বার রেজিস্ট্রেশন নম্বরটি ইতিমধ্যে নিবন্ধিত।";
            TempData["ErrorEn"] = "This bar registration number is already registered.";
            return RedirectToAction(nameof(Status));
        }

        profile.BarRegistrationNumber = barNumber;
        if (!string.IsNullOrWhiteSpace(vm.Specialization))
            profile.Specialization = vm.Specialization.Trim();
        profile.VerificationStatus = VerificationStatus.Pending;
        await _profileRepo.SaveChangesAsync();

        TempData["Success"] = "আবেদন পুনরায় জমা হয়েছে — ২৪–৪৮ ঘণ্টার মধ্যে যাচাই হবে।";
        TempData["SuccessEn"] = "Application resubmitted — verification typically takes 24–48h.";
        return RedirectToAction(nameof(Status));
    }

    private const int MaxBarNumberLength = 100; // LAWYER_PROFILE.BarRegistrationNumber NVARCHAR(100)

    private const int QueuePageSize = 20;

    // Queue: documents in the pool, oldest-first (SLA). Filter chips:
    // All (default) · Unclaimed · Mine (my claimed docs, re-enterable) ·
    // MyField (cases matching my specialization).
    [HttpGet]
    public async Task<IActionResult> Queue(string? filter, int page = 1)
    {
        var profile = await MyProfileAsync();
        if (profile == null || profile.VerificationStatus != VerificationStatus.Approved)
            return RedirectToAction(nameof(Status));

        var queue = await _reviewService.GetQueueAsync(profile.LawyerProfileId, filter, page, QueuePageSize);

        var vm = new LawyerQueueViewModel
        {
            LawyerName = await MyNameAsync(profile),
            BarRegistrationNumber = profile.BarRegistrationNumber,
            Specialization = profile.Specialization ?? "",
            PendingCount = queue.PoolCount, // whole backlog, whatever filter is active
            ActiveFilter = filter ?? "All",
            FieldFallback = queue.FieldFallback,
            Page = queue.Page,
            PageSize = QueuePageSize,
            TotalCount = queue.TotalCount,
            Items = queue.Items.Select(q => new LawyerQueueItemViewModel
            {
                DocumentId = q.DocumentId,
                CaseId = q.CaseId,
                CaseTitle = q.CaseTitle,
                CategoryName = q.CategoryName,
                DistrictName = q.DistrictName,
                CitizenEdited = q.CitizenEdited,
                VersionNo = q.VersionNo,
                ClaimedBy = q.ClaimedBy,
                IsClaimed = q.IsClaimed,
                IsMine = q.IsMine,
                WaitingHours = (int)Math.Max(0, (DateTime.UtcNow - q.CreatedAt).TotalHours),
                CanOpen = q.CanOpen
            }).ToList()
        };
        return View(vm);
    }

    // Claim-on-open (optimistic lock) then straight into the workspace.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Claim(int documentId)
    {
        var profile = await MyProfileAsync();
        if (profile == null || profile.VerificationStatus != VerificationStatus.Approved)
            return RedirectToAction(nameof(Status));

        var ok = await _reviewService.ClaimAsync(documentId, profile.LawyerProfileId);
        if (!ok)
        {
            TempData["Error"] = "অন্য আইনজীবী এটি নিয়েছেন — সারিতে ফিরে যান।";
            TempData["ErrorEn"] = "Another lawyer claimed this — back to the queue.";
            return RedirectToAction(nameof(Queue));
        }
        return RedirectToAction(nameof(Review), new { id = documentId });
    }

    [HttpGet]
    public async Task<IActionResult> Review(int id)
    {
        var profile = await MyProfileAsync();
        if (profile == null || profile.VerificationStatus != VerificationStatus.Approved)
            return RedirectToAction(nameof(Status));

        return await ReviewWorkspaceAsync(id, profile.LawyerProfileId, posted: null);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitReview(LawyerReviewViewModel vm)
    {
        var profile = await MyProfileAsync();
        if (profile == null || profile.VerificationStatus != VerificationStatus.Approved)
            return RedirectToAction(nameof(Status));

        // Decision/Comments rules live on LawyerReviewViewModel (DataAnnotations).
        // An unknown decision is an error, never a silent default (#8).
        var isDecision = Enum.GetNames<ReviewDecision>().Contains(vm.Decision);
        if (!isDecision && ModelState.GetFieldValidationState(nameof(vm.Decision)) != ModelValidationState.Invalid)
            ModelState.AddModelError(nameof(vm.Decision),
                "সিদ্ধান্ত অবশ্যই Approved, EditedApproved অথবা Rejected হতে হবে / Decision must be Approved, EditedApproved or Rejected");
        if (vm.Decision == nameof(ReviewDecision.EditedApproved) && string.IsNullOrWhiteSpace(vm.EditedContent))
            ModelState.AddModelError(nameof(vm.EditedContent),
                "সম্পাদনাসহ অনুমোদনের জন্য সম্পাদিত পাঠ্য আবশ্যক / Edited text is required to approve with edits");

        // Re-show the workspace with the lawyer's own text and comment instead
        // of redirecting (a redirect reloads the document and loses them, #6).
        if (!ModelState.IsValid)
            return await ReviewWorkspaceAsync(vm.DocumentId, profile.LawyerProfileId, posted: vm);

        var decision = Enum.Parse<ReviewDecision>(vm.Decision);
        var ok = await _reviewService.SubmitReviewAsync(new SubmitReviewDto(
            vm.DocumentId,
            profile.LawyerProfileId,
            decision,
            vm.Comments.Trim(),
            decision == ReviewDecision.EditedApproved ? vm.EditedContent : null));

        if (!ok)
        {
            // Input was valid, so the document changed underneath (claim lost,
            // already decided, or a concurrent save) -- reload is correct here.
            TempData["Error"] = "পর্যালোচনা সংরক্ষণ হয়নি — নথিটি ইতিমধ্যে পরিবর্তিত হয়েছে। সারি থেকে আবার খুলুন।";
            TempData["ErrorEn"] = "Review not saved — the document changed in the meantime. Open it again from the queue.";
            return RedirectToAction(nameof(Review), new { id = vm.DocumentId });
        }

        TempData["Success"] = "পর্যালোচনা সম্পন্ন — পরবর্তী নথিতে যাচ্ছেন।";
        TempData["SuccessEn"] = "Review saved — advancing to the next document.";
        return RedirectToAction(nameof(Queue));
    }

    // Builds the review page from the lawyer's own active claim. `posted`
    // (an invalid submission) keeps the lawyer's decision, edited text and
    // comment on top of the freshly loaded workspace context.
    private async Task<IActionResult> ReviewWorkspaceAsync(int documentId, int lawyerProfileId, LawyerReviewViewModel? posted)
    {
        var ws = await _reviewService.GetForReviewAsync(documentId, lawyerProfileId);
        if (ws == null)
        {
            TempData["Error"] = "এই নথিটি আপনার নেওয়া পর্যালোচনাধীন নথি নয় — সারি থেকে খুলুন।";
            TempData["ErrorEn"] = "This document isn't under review in your name — open it from the queue.";
            return RedirectToAction(nameof(Queue));
        }

        var vm = new LawyerReviewViewModel
        {
            DocumentId = ws.DocumentId,
            CaseId = ws.CaseId,
            CaseTitle = ws.CaseTitle,
            CategoryName = ws.CategoryName,
            ContentDraft = ws.OriginalDraft,
            EditedContent = posted != null ? posted.EditedContent : ws.CitizenEditedDraft ?? ws.OriginalDraft,
            Decision = posted?.Decision ?? nameof(ReviewDecision.EditedApproved),
            Comments = posted?.Comments ?? string.Empty,
            DistrictName = ws.DistrictName,
            CitizenNarrative = ws.CitizenNarrative,
            Citations = ws.Citations,
            VersionNo = ws.VersionNo,
            CitizenEdited = ws.CitizenEdited
        };
        return View(nameof(Review), vm);
    }

    private const int HistoryPageSize = 20;

    // "What did I review" — every decision this lawyer has submitted, newest
    // first. Filter chips: All/Approved/EditedApproved/Rejected + date range +
    // sort (date_desc default | date_asc | case_asc).
    [HttpGet]
    public async Task<IActionResult> History(string? decision, string? from, string? to, string? sort, int page = 1)
    {
        var profile = await MyProfileAsync();
        if (profile == null) return NotFound();
        if (profile.VerificationStatus != VerificationStatus.Approved) return RedirectToAction(nameof(Status));

        // Dates are Dhaka calendar days (what the lawyer picked); ReviewedAt is
        // UTC, so convert the whole-day bounds. A reversed range is swapped.
        DateOnly? fromDay = BdTime.TryParseDay(from, out var f) ? f : null;
        DateOnly? toDay = BdTime.TryParseDay(to, out var t) ? t : null;
        if (fromDay > toDay)
        {
            (fromDay, toDay) = (toDay, fromDay);
            (from, to) = (to, from);
        }
        DateTime? fromDate = fromDay.HasValue ? BdTime.DayStartUtc(fromDay.Value) : null;
        DateTime? toDate = toDay.HasValue ? BdTime.DayEndUtc(toDay.Value) : null;

        var history = await _reviewService.GetHistoryPageAsync(
            profile.LawyerProfileId, decision, fromDate, toDate, sort, page, HistoryPageSize);

        var vm = new LawyerHistoryViewModel
        {
            LawyerName = await MyNameAsync(profile),
            BarRegistrationNumber = profile.BarRegistrationNumber,
            ActiveFilter = decision ?? "All",
            FromDate = from,
            ToDate = to,
            Sort = sort ?? "date_desc",
            TotalCount = history.TotalCount,
            Page = history.Page,
            Items = history.Items.Select(h => new LawyerHistoryItemViewModel
            {
                ReviewId = h.ReviewId,
                DocumentId = h.DocumentId,
                CaseId = h.CaseId,
                CaseTitle = h.CaseTitle,
                CategoryName = h.CategoryName,
                DistrictName = h.DistrictName,
                Decision = h.Decision.ToString(),
                Comments = h.Comments,
                ReviewedAt = h.ReviewedAt,
                VersionNo = h.VersionNo,
                DocumentText = h.DocumentText
            }).ToList()
        };

        return View(vm);
    }

    // FR-24 (lawyer variant): balance + honorarium history, moved off the
    // shared Account/Profile page into its own lawyer-scoped route.
    [HttpGet]
    public async Task<IActionResult> Payments()
    {
        var profile = await MyProfileAsync();
        if (profile == null) return NotFound();
        if (profile.VerificationStatus != VerificationStatus.Approved) return RedirectToAction(nameof(Status));

        var earnings = await _paymentService.GetLawyerEarningsAsync(profile.LawyerProfileId);
        var vm = new LawyerPaymentsViewModel
        {
            LawyerName = await MyNameAsync(profile),
            BarRegistrationNumber = profile.BarRegistrationNumber,
            Balance = earnings.Balance,
            PendingPayout = earnings.PendingPayout,
            History = earnings.History.Select(h => new EarningRowViewModel
            {
                PaymentOrderId = h.PaymentOrderId,
                CaseId = h.CaseId,
                Gross = h.Gross,
                Commission = h.Commission,
                Net = h.Net,
                PaidAt = h.PaidAt
            }).ToList()
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestPayout()
    {
        var profile = await MyProfileAsync();
        if (profile == null) return NotFound();
        if (profile.VerificationStatus != VerificationStatus.Approved) return RedirectToAction(nameof(Status));

        switch (await _paymentService.RequestPayoutAsync(profile.LawyerProfileId))
        {
            case PayoutRequestResult.AlreadyPending:
                TempData["Error"] = "একটি পরিশোধের অনুরোধ ইতিমধ্যে অপেক্ষমাণ — অ্যাডমিন পরিশোধ করলে নতুন অনুরোধ করতে পারবেন।";
                TempData["ErrorEn"] = "A payout request is already pending — you can request again once the admin has paid it.";
                break;
            case PayoutRequestResult.NothingToPay:
                TempData["Error"] = "পরিশোধযোগ্য ব্যালেন্স নেই।";
                TempData["ErrorEn"] = "No payable balance.";
                break;
            default:
                TempData["Success"] = "পরিশোধের অনুরোধ জমা হয়েছে (স্যান্ডবক্স)।";
                TempData["SuccessEn"] = "Payout request submitted (sandbox).";
                break;
        }
        return RedirectToAction(nameof(Payments));
    }
}
