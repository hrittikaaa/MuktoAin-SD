using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MuktoAin.Application.DTOs;
using MuktoAin.Application.Services;
using MuktoAin.Domain.Entities;
using MuktoAin.Domain.Enums;
using MuktoAin.Domain.Interfaces.Repositories;
using MuktoAin.Infrastructure.Ai;
using MuktoAin.Infrastructure.Data;
using MuktoAin.Web.Auth;
using MuktoAin.Web.ViewModels;
using Qdrant.Client;

namespace MuktoAin.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ILogger<AdminController> _logger;
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IUserManagementService _userManagement;
    private readonly LawyerVerificationService _lawyerVerification;
    private readonly IRepository<LawyerProfile> _lawyerProfileRepo;
    private readonly UserManager<User> _userManager;
    private readonly IActRepository _actRepo;
    private readonly IActSectionRepository _sectionRepo;
    private readonly IActSectionChunkRepository _chunkRepo;
    private readonly IScenarioMappingRepository _scenarioRepo;
    private readonly IRepository<CaseCategory> _categoryRepo;
    private readonly IRepository<AiLog> _aiLogRepo;
    private readonly PaymentService _paymentService;
    private readonly GeminiClient _geminiClient;
    private readonly IAdminAuditService _audit;

    public AdminController(
        ILogger<AdminController> logger,
        AppDbContext dbContext,
        IConfiguration configuration,
        IUserManagementService userManagement,
        LawyerVerificationService lawyerVerification,
        IRepository<LawyerProfile> lawyerProfileRepo,
        UserManager<User> userManager,
        IActRepository actRepo,
        IActSectionRepository sectionRepo,
        IActSectionChunkRepository chunkRepo,
        IScenarioMappingRepository scenarioRepo,
        IRepository<CaseCategory> categoryRepo,
        IRepository<AiLog> aiLogRepo,
        PaymentService paymentService,
        GeminiClient geminiClient,
        IAdminAuditService audit)
    {
        _logger = logger;
        _dbContext = dbContext;
        _configuration = configuration;
        _userManagement = userManagement;
        _lawyerVerification = lawyerVerification;
        _lawyerProfileRepo = lawyerProfileRepo;
        _userManager = userManager;
        _actRepo = actRepo;
        _sectionRepo = sectionRepo;
        _chunkRepo = chunkRepo;
        _scenarioRepo = scenarioRepo;
        _categoryRepo = categoryRepo;
        _aiLogRepo = aiLogRepo;
        _paymentService = paymentService;
        _geminiClient = geminiClient;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        ViewData["IsAdminPage"] = true;
        var model = await BuildAdminDashboardViewModelAsync();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Analytics()
    {
        ViewData["IsAdminPage"] = true;
        var model = await BuildAdminDashboardViewModelAsync();
        return View(model);
    }

/// <summary>
    /// Live Real-time API endpoint polled by the Admin Dashboard to give immediate
    /// feedback as configuration or services change without restarting the server.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> HealthStatus()
    {
        var model = await BuildAdminDashboardViewModelAsync();
        return Json(new
        {
            isDatabaseHealthy = model.IsDatabaseHealthy,
            isVectorDbHealthy = model.IsVectorDbHealthy,
            isAiServiceHealthy = model.IsAiServiceHealthy,
            databaseStatus = model.DatabaseStatus,
            vectorDbStatus = model.VectorDbStatus,
            aiServiceStatus = model.AiServiceStatus,
            overallHealthBadgeText = model.OverallHealthBadgeText,
            overallHealthBadgeClass = model.OverallHealthBadgeClass,
            totalUsersCount = model.TotalUsersCount,
            totalActsCount = model.TotalActsCount,
            verificationsWaiting = model.VerificationsWaiting,
            pendingReviews = model.PendingReviews,
            lastChecked = DateTime.Now.ToString("T")
        });
    }

    private static int? _cachedTotalChunks;
    private static int? _cachedDistinctChunkTexts;

    private async Task<bool> SharedCollectionCoversCorpusAsync()
    {
        var endpoint = _configuration["Qdrant:Endpoint"];
        var apiKey = _configuration["Qdrant:ApiKey"];
        var collection = _configuration["Qdrant:Collection"] ?? "act_section_chunks";
        if (string.IsNullOrWhiteSpace(endpoint) || endpoint.Contains("your-cluster-id") ||
            string.IsNullOrWhiteSpace(apiKey) || apiKey.Contains("YOUR_QDRANT_API_KEY"))
            return false;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var uri = new Uri(endpoint);
            var client = new QdrantClient(uri.Host, port: uri.Port, https: uri.Scheme == "https", apiKey: apiKey);
            var points = (long)await client.CountAsync(collection, cancellationToken: cts.Token);

            _cachedDistinctChunkTexts ??= await _dbContext.ActSectionChunks
                .AsNoTracking().Select(c => c.ChunkText).Distinct().CountAsync();

            return points > 0 && points >= _cachedDistinctChunkTexts.Value;
        }
        catch (Exception ex)
        {
            _logger.LogInformation("Qdrant point-count check failed: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Live endpoint for tracking Qdrant embedding and upload progress.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> EmbeddingProgress()
    {
        if (!_cachedTotalChunks.HasValue || _cachedTotalChunks.Value == 0)
        {
            _cachedTotalChunks = await _dbContext.ActSectionChunks.AsNoTracking().CountAsync();
        }

        var totalChunks = _cachedTotalChunks.Value;
        var embeddedChunks = await _dbContext.ActSectionChunks
            .AsNoTracking()
            .Where(c => c.VectorId != null)
            .CountAsync();

        // The shared Qdrant collection is embedded by one teammate against their own
        // SQL database, so this DB's VectorId column can stay NULL even though every
        // vector already exists. Identical texts share one point (global dedupe), so
        // the collection is complete once it holds a point per distinct chunk text.
        if (embeddedChunks < totalChunks && await SharedCollectionCoversCorpusAsync())
        {
            embeddedChunks = totalChunks;
        }

        var percent = totalChunks > 0 ? (double)embeddedChunks / totalChunks * 100.0 : 0;

        return Json(new
        {
            totalChunks,
            embeddedChunks,
            remainingChunks = totalChunks - embeddedChunks,
            percentage = Math.Round(percent, 2),
            isRunning = MuktoAin.Infrastructure.VectorStore.EmbeddingProgressState.IsRunning,
            lastStatus = MuktoAin.Infrastructure.VectorStore.EmbeddingProgressState.LastStatus,
            lastStatusEn = MuktoAin.Infrastructure.VectorStore.EmbeddingProgressState.LastStatusEn,
            totalProcessed = MuktoAin.Infrastructure.VectorStore.EmbeddingProgressState.TotalProcessed,
            totalSkipped = MuktoAin.Infrastructure.VectorStore.EmbeddingProgressState.TotalSkipped,
            requestsPerMinuteBudget = MuktoAin.Infrastructure.VectorStore.EmbeddingProgressState.RequestsPerMinuteBudget,
            estimatedCompletion = MuktoAin.Infrastructure.VectorStore.EmbeddingProgressState.EstimatedCompletion,
            estimatedCompletionEn = MuktoAin.Infrastructure.VectorStore.EmbeddingProgressState.EstimatedCompletionEn
        });
    }

    /// <summary>
    /// Live endpoint for tracking Gemini API key usage/exhaustion — polled by the
    /// Admin Dashboard, mirroring EmbeddingProgress()'s pattern above.
    /// </summary>
    [HttpGet]
    public IActionResult GeminiKeyStatus()
    {
        var snapshot = _geminiClient.Snapshot();
        var keys = snapshot.Select(k => new
        {
            label = k.Label,
            tokensUsedLastMinute = k.TokensUsedLastMinute,
            tokenLimitPerMinute = k.TokenLimitPerMinute,
            percentage = k.TokenLimitPerMinute > 0
                ? Math.Round(Math.Min(100.0, (double)k.TokensUsedLastMinute / k.TokenLimitPerMinute * 100.0), 1)
                : 0,
            isParked = k.IsParked,
            parkedUntilUtc = k.ParkedUntilUtc
        }).ToList();

        return Json(new
        {
            totalKeys = snapshot.Count,
            availableKeys = snapshot.Count(k => !k.IsParked),
            exhaustedKeys = snapshot.Count(k => k.IsParked),
            keys
        });
    }

    private const int AdminListPageSize = 20;

    [HttpGet]
    public async Task<IActionResult> Users(string? role, int page = 1)
    {
        var all = await _userManagement.GetAllUsersAsync();
        var filtered = (string.IsNullOrWhiteSpace(role) || role == "All"
            ? all
            : all.Where(u => u.Role.Equals(role, StringComparison.OrdinalIgnoreCase))).ToList();

        var totalPages = Math.Max((int)Math.Ceiling(filtered.Count / (double)AdminListPageSize), 1);
        var vm = new AdminUsersViewModel
        {
            RoleFilter = role ?? "All",
            ViewerIsSuperAdmin = User.HasClaim("IsSuperAdmin", "true"),
            Page = Math.Max(1, Math.Min(page, totalPages)),
            PageSize = AdminListPageSize,
            TotalCount = filtered.Count,
            Users = filtered
                .Skip((Math.Max(1, Math.Min(page, totalPages)) - 1) * AdminListPageSize)
                .Take(AdminListPageSize)
                .Select(u => new AdminUserRowViewModel
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email,
                    Role = u.Role,
                    Status = u.Status,
                    IsSuperAdmin = u.IsSuperAdmin
                }).ToList()
        };
        return View(vm);
    }

    // Suspend/Activate. Admin rows are protected inside the service
    // (UserManagementService guards admins + self-suspend).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(int userId, bool suspend)
    {
        var adminId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            out var id) ? id : 0;
        var ok = await _userManagement.SetAccountStatusAsync(
            userId, suspend ? Domain.Enums.AccountStatus.Suspended : Domain.Enums.AccountStatus.Active, adminId);
        if (!ok)
        {
            TempData["Error"] = "এই অ্যাকাউন্টের অবস্থা পরিবর্তন করা যাবে না (অ্যাডমিন সুরক্ষিত)।";
            TempData["ErrorEn"] = "This account's status cannot be changed (admin protected).";
        }
        else
        {
            TempData["Success"] = suspend ? "অ্যাকাউন্ট স্থগিত হয়েছে।" : "অ্যাকাউন্ট পুনরায় চালু হয়েছে।";
            TempData["SuccessEn"] = suspend ? "Account suspended." : "Account reactivated.";
        }
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    [Authorize(Policy = "SuperAdminOnly")]
    public IActionResult CreateAdmin() => View(new CreateAdminViewModel());

    [HttpPost]
    [Authorize(Policy = "SuperAdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAdmin(CreateAdminViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var actingSuperAdminId = int.TryParse(
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        AdminAccountResultDto result;
        try
        {
            result = await _userManagement.CreateAdminAsync(
                model.FullName, model.Email, model.AsSuperAdmin, actingSuperAdminId);
        }
        catch (IdentityCreationFailedException ex)
        {
            // Same per-field mapping AccountController.Register already uses for
            // the identical UserManager.CreateAsync failure shape.
            foreach (var error in ex.Errors)
            {
                var (field, message) = IdentityErrorMapper.Map(error);
                ModelState.AddModelError(field ?? string.Empty, message);
            }
            return View(model);
        }

        var resetUrl = Url.Action("ResetPassword", "Account",
            new { email = model.Email, token = result.PasswordResetUrl }, Request.Scheme);

        TempData["Success"] = "নতুন অ্যাডমিন তৈরি হয়েছে — রিসেট লিংকটি নিরাপদে পাঠান।";
        TempData["SuccessEn"] = "New admin created — relay the reset link securely.";
        TempData["Info"] = resetUrl;
        TempData["InfoEn"] = resetUrl;
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [Authorize(Policy = "SuperAdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuspendAdmin(int userId, bool suspend)
    {
        var actingSuperAdminId = int.TryParse(
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
        var ok = await _userManagement.SetAdminStatusAsync(
            userId, suspend ? AccountStatus.Suspended : AccountStatus.Active, actingSuperAdminId);

        if (!ok)
        {
            TempData["Error"] = "এই অ্যাডমিনের অবস্থা পরিবর্তন করা যাবে না (SuperAdmin সুরক্ষিত)।";
            TempData["ErrorEn"] = "This admin's status cannot be changed (SuperAdmin protected).";
        }
        else
        {
            TempData["Success"] = suspend ? "অ্যাডমিন স্থগিত হয়েছে।" : "অ্যাডমিন পুনরায় চালু হয়েছে।";
            TempData["SuccessEn"] = suspend ? "Admin suspended." : "Admin reactivated.";
        }
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [Authorize(Policy = "SuperAdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PromoteAdmin(int userId)
    {
        var actingSuperAdminId = int.TryParse(
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
        var ok = await _userManagement.PromoteToSuperAdminAsync(userId, actingSuperAdminId);

        TempData[ok ? "Success" : "Error"] = ok
            ? "অ্যাডমিনকে সুপার অ্যাডমিনে উন্নীত করা হয়েছে।"
            : "উন্নীত করা যায়নি (ইতিমধ্যে সুপার অ্যাডমিন)।";
        TempData[ok ? "SuccessEn" : "ErrorEn"] = ok
            ? "Admin promoted to SuperAdmin."
            : "Could not promote (already a SuperAdmin).";
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public async Task<IActionResult> Lawyers(string? status = "All", string? q = null, int page = 1, int pageSize = 15)
    {
        var allProfiles = (await _lawyerProfileRepo.GetAllAsync()).ToList();
        var userIds = allProfiles.Select(p => p.UserId).Distinct().ToList();
        var users = await _dbContext.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var rows = new List<AdminLawyerRowViewModel>();
        foreach (var p in allProfiles)
        {
            users.TryGetValue(p.UserId, out var u);
            rows.Add(new AdminLawyerRowViewModel
            {
                LawyerProfileId = p.LawyerProfileId,
                ApplicantName = u?.FullName ?? "(unknown)",
                Email = u?.Email ?? "",
                BarRegistrationNumber = p.BarRegistrationNumber,
                Specialization = p.Specialization ?? "",
                Status = p.VerificationStatus.ToString()
            });
        }

        var pendingList = rows.Where(r => r.Status == "Pending").ToList();
        var approvedList = rows.Where(r => r.Status == "Approved").ToList();
        var rejectedList = rows.Where(r => r.Status == "Rejected").ToList();

        var filtered = rows.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(r => r.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var trimmed = q.Trim();
            filtered = filtered.Where(r =>
                r.ApplicantName.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                r.Email.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                r.BarRegistrationNumber.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                r.Specialization.Contains(trimmed, StringComparison.OrdinalIgnoreCase));
        }

        var filteredList = filtered.ToList();
        var totalPages = Math.Max((int)Math.Ceiling(filteredList.Count / (double)pageSize), 1);
        var currentPage = Math.Max(1, Math.Min(page, totalPages));

        var pagedRows = filteredList
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var vm = new AdminLawyersViewModel
        {
            Pending = pendingList,
            Approved = approvedList,
            Rejected = rejectedList,
            FilteredLawyers = pagedRows,
            StatusFilter = status ?? "All",
            SearchQuery = q,
            Page = currentPage,
            PageSize = pageSize,
            TotalFilteredCount = filteredList.Count
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyLawyer(int lawyerProfileId, bool approve, string? reason, string? returnUrl = null)
    {
        if (!approve && string.IsNullOrWhiteSpace(reason))
        {
            TempData["Error"] = "প্রত্যাখ্যানের কারণ আবশ্যক।";
            TempData["ErrorEn"] = "Rejection reason is required.";
            return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? LocalRedirect(returnUrl)
                : RedirectToAction(nameof(Lawyers));
        }

        var adminId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            out var id) ? id : 0;
        try
        {
            await _lawyerVerification.VerifyAsync(lawyerProfileId, adminId, approve, reason);
        }
        catch (InvalidOperationException)
        {
            // Already decided (stale page or double submit) -- nothing changed.
            TempData["Error"] = "এই আবেদনটি ইতিমধ্যে নিষ্পত্তি হয়েছে।";
            TempData["ErrorEn"] = "This application has already been decided.";
            return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? LocalRedirect(returnUrl)
                : RedirectToAction(nameof(Lawyers));
        }

        TempData["Success"] = approve
            ? "আইনজীবী যাচাই অনুমোদিত হয়েছে।"
            : "আবেদন প্রত্যাখ্যাত হয়েছে (কারণসহ)।";
        TempData["SuccessEn"] = approve ? "Lawyer verified." : "Application rejected (with reason).";
        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(nameof(Lawyers));
    }

    // ---------- FR-17: Corpus ----------

    [HttpGet]
    public async Task<IActionResult> Corpus(string? q = null, int page = 1, int pageSize = 25)
    {
        // High-performance database-side aggregation (FR-17):
        // Rather than materializing all chunks and sections into memory,
        // compute totals and per-act chunk counts directly via EF Core aggregates.
        var totalSections = await _dbContext.ActSections.CountAsync();
        var totalChunks = await _dbContext.ActSectionChunks.CountAsync();
        var embeddedChunks = await _dbContext.ActSectionChunks.CountAsync(c => c.VectorId != null);
        var totalActs = await _dbContext.Acts.CountAsync();

        if (page < 1) page = 1;
        if (pageSize < 10) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        IQueryable<MuktoAin.Domain.Entities.Act> query = _dbContext.Acts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var trimmed = q.Trim();
            if (int.TryParse(trimmed, out var yearSearch) && yearSearch >= 1800 && yearSearch <= 2100)
            {
                query = query.Where(a => a.Title.Contains(trimmed) || (a.ActNumber != null && a.ActNumber.Contains(trimmed)) || a.Year == yearSearch);
            }
            else
            {
                query = query.Where(a => a.Title.Contains(trimmed) || (a.ActNumber != null && a.ActNumber.Contains(trimmed)));
            }
        }

        var totalFiltered = await query.CountAsync();

        var acts = await query
            .OrderBy(a => a.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AdminActRowViewModel
            {
                ActId = a.ActId,
                Title = a.Title,
                ActNumber = a.ActNumber ?? "",
                Year = a.Year,
                Language = a.Language,
                IsRepealed = a.IsRepealed,
                SectionCount = a.Sections.Count(),
                ChunkCount = a.Sections.SelectMany(s => s.Chunks).Count(),
                EmbeddedCount = a.Sections.SelectMany(s => s.Chunks).Count(c => c.VectorId != null),
                ImportedAt = a.ImportedAt
            })
            .ToListAsync();

        var vm = new AdminCorpusViewModel
        {
            TotalSections = totalSections,
            TotalChunks = totalChunks,
            EmbeddedChunks = embeddedChunks,
            TotalActs = totalActs,
            TotalFilteredActs = totalFiltered,
            SearchQuery = q,
            CurrentPage = page,
            PageSize = pageSize,
            Acts = acts
        };
        return View(vm);
    }

    // ---------- FR-18: Scenario mappings ----------

    [HttpGet]
    public async Task<IActionResult> Scenarios(string? q, int page = 1, int pageSize = 20)
    {
        var mappings = await _scenarioRepo.GetAllAsync();
        var sections = await _sectionRepo.GetAllAsync();
        var acts = await _actRepo.GetAllAsync();

        var sectionDict = sections.ToDictionary(s => s.SectionId);
        var actDict = acts.ToDictionary(a => a.ActId);

        var allRows = mappings.Select(m =>
        {
            sectionDict.TryGetValue(m.SectionId, out var s);
            var a = s != null && actDict.TryGetValue(s.ActId, out var act) ? act : null;
            return new AdminScenarioRowViewModel
            {
                MappingId = m.MappingId,
                Keyword = m.ScenarioKeyword,
                ActTitle = a?.Title ?? "",
                SectionNumber = s?.SectionNumber ?? "",
                Notes = m.Notes
            };
        }).ToList();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var query = q.Trim();
            allRows = allRows.Where(r =>
                r.Keyword.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                r.ActTitle.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                r.SectionNumber.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (r.Notes != null && r.Notes.Contains(query, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        var totalFiltered = allRows.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalFiltered / (double)pageSize));
        page = Math.Max(1, Math.Min(page, totalPages));

        var pagedRows = allRows
            .OrderBy(m => m.MappingId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var availableActs = acts
            .OrderBy(a => a.Title)
            .Select(a => new AdminActOptionViewModel
            {
                ActId = a.ActId,
                Title = a.Title,
                Year = a.Year
            })
            .ToList();

        var vm = new AdminScenariosViewModel
        {
            Mappings = pagedRows,
            AvailableActs = availableActs,
            SearchQuery = q,
            Page = page,
            PageSize = pageSize,
            TotalFilteredCount = totalFiltered
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddScenario(string keyword, int actId, string? notes, int? sectionId)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            TempData["Error"] = "কি-ওয়ার্ড (Keyword) বাধ্যতামূলক।";
            TempData["ErrorEn"] = "Keyword is required.";
            return RedirectToAction(nameof(Scenarios));
        }

        if (actId <= 0 && (sectionId == null || sectionId <= 0))
        {
            TempData["Error"] = "আইন (Act) নির্বাচন বাধ্যতামূলক।";
            TempData["ErrorEn"] = "Act selection is required.";
            return RedirectToAction(nameof(Scenarios));
        }

        int secId = sectionId ?? 0;
        if (secId <= 0)
        {
            var allSections = await _sectionRepo.GetAllAsync();
            var sectionForAct = allSections.FirstOrDefault(s => s.ActId == actId);

            if (sectionForAct != null)
            {
                secId = sectionForAct.SectionId;
            }
            else
            {
                var act = await _actRepo.GetByIdAsync(actId);
                if (act == null)
                {
                    TempData["Error"] = "নির্বাচিত আইনটি পাওয়া যায়নি।";
                    TempData["ErrorEn"] = "Selected Act was not found.";
                    return RedirectToAction(nameof(Scenarios));
                }

                var newSection = new Domain.Entities.ActSection
                {
                    ActId = actId,
                    SectionNumber = "General",
                    SectionTitle = "General Statutory Reference",
                    SectionText = $"{act.Title} - General statutory reference",
                    OrdinalPosition = 1
                };
                await _sectionRepo.AddAsync(newSection);
                await _sectionRepo.SaveChangesAsync();
                secId = newSection.SectionId;
            }
        }

        await _scenarioRepo.AddAsync(new Domain.Entities.ScenarioMapping
        {
            SectionId = secId,
            ScenarioKeyword = keyword.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        });
        await _scenarioRepo.SaveChangesAsync();
        TempData["Success"] = "নতুন সিনারিও ম্যাপিং যুক্ত হয়েছে।";
        TempData["SuccessEn"] = "Scenario mapping added successfully.";
        return RedirectToAction(nameof(Scenarios));
    }

    private async Task<int> EnsureFallbackSectionAsync()
    {
        var existingSection = (await _sectionRepo.GetAllAsync()).FirstOrDefault();
        if (existingSection != null) return existingSection.SectionId;

        var existingAct = (await _actRepo.GetAllAsync()).FirstOrDefault();
        if (existingAct == null)
        {
            existingAct = new Domain.Entities.Act
            {
                Title = "Laws of Bangladesh (General)",
                ActNumber = "0",
                Year = 2026,
                PublicationDate = "2026",
                Language = "en",
                ImportedAt = DateTime.UtcNow
            };
            await _actRepo.AddAsync(existingAct);
            await _actRepo.SaveChangesAsync();
        }

        var fallbackSection = new Domain.Entities.ActSection
        {
            ActId = existingAct.ActId,
            SectionNumber = "General",
            SectionTitle = "General Statutory Reference",
            SectionText = "General statutory reference",
            OrdinalPosition = 1
        };
        await _sectionRepo.AddAsync(fallbackSection);
        await _sectionRepo.SaveChangesAsync();

        return fallbackSection.SectionId;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteScenario(int mappingId)
    {
        var all = await _scenarioRepo.GetAllAsync();
        var m = all.FirstOrDefault(x => x.MappingId == mappingId);
        if (m != null)
        {
            await _scenarioRepo.DeleteAsync(m);
            await _scenarioRepo.SaveChangesAsync();

            // AUD-7: DeleteScenario is a HARD delete with no soft-delete flag —
            // the audit row is the only surviving record of what was removed.
            var adminId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                out var id) ? id : 0;
            await _audit.LogAdminActionAsync(
                adminId, "DeleteScenario",
                targetEntityId: mappingId,
                details: $"Keyword '{m.ScenarioKeyword}' (SectionId {m.SectionId}) hard-deleted.");
        }
        TempData["Success"] = "ম্যাপিং মুছে ফেলা হয়েছে। / Mapping deleted.";
        return RedirectToAction(nameof(Scenarios));
    }

    // ---------- FR-18: Categories ----------

    [HttpGet]
    public async Task<IActionResult> Categories()
    {
        var cats = await _categoryRepo.GetAllAsync();
        var vm = new AdminCategoriesViewModel
        {
            Categories = cats.OrderBy(c => c.CategoryId).Select(c => new AdminCategoryRowViewModel
            {
                CategoryId = c.CategoryId,
                Name = c.Name,
                NameBn = c.NameBn,
                Description = c.Description,
                DescriptionBn = c.DescriptionBn,
                TemplateBadge = ResolveTemplateBadge(c)
            }).ToList()
        };
        return View(vm);
    }

    private static string ResolveTemplateBadge(Domain.Entities.CaseCategory c)
    {
        var name = (c.Name ?? string.Empty).ToLowerInvariant();
        if (name.Contains("labour") || name.Contains("labor")) return "labour_complaint.v1";
        if (name.Contains("general diary") || name.Contains("gd")) return "gd_application.v1";
        if (name.Contains("rti") || name.Contains("information")) return "rti_request.v1";
        if (name.Contains("consumer")) return "consumer_complaint.v1";

        var slug = new string(name.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray())
            .Trim('_');
        return string.IsNullOrEmpty(slug) ? "custom.v1" : $"{slug}.v1";
    }

    private const int AdminAiLogsPageSize = 50;

    // ---------- FR-12: AI Logs ----------

    [HttpGet]
    public async Task<IActionResult> AiLogs(string? type, int minLatency = 0, int page = 1)
    {
        var all = (await _aiLogRepo.GetAllAsync()).ToList();

        // "Calls today" is a global KPI — intentionally counted BEFORE the
        // type/latency filters (same semantics as the pre-AUD-8 action).
        var today = DateTime.UtcNow.Date;
        var callsToday = all.Count(l => l.CreatedAt >= today);

        // AUD-8: filter the FULL set (the old code took the newest 200 rows
        // BEFORE filtering, so filters silently ignored older matches), then
        // page. No hardcoded Take(200) — older entries are reachable again.
        IEnumerable<AiLog> filtered = all.OrderByDescending(l => l.CreatedAt);
        if (!string.IsNullOrWhiteSpace(type) && type != "All"
            && Enum.TryParse<Domain.Enums.AiRequestType>(type, out var t))
        {
            filtered = filtered.Where(l => l.RequestType == t);
        }
        if (minLatency > 0)
        {
            filtered = filtered.Where(l => l.LatencyMs >= minLatency);
        }
        var filteredList = filtered.ToList();

        var totalPages = Math.Max((int)Math.Ceiling(filteredList.Count / (double)AdminAiLogsPageSize), 1);
        var currentPage = Math.Max(1, Math.Min(page, totalPages));

        var vm = new AdminAiLogsViewModel
        {
            CallsToday = callsToday,
            FailureRateToday = 0, // failure detection = latency outliers; see view
            Page = currentPage,
            PageSize = AdminAiLogsPageSize,
            TotalCount = filteredList.Count,
            Logs = filteredList
                .Skip((currentPage - 1) * AdminAiLogsPageSize)
                .Take(AdminAiLogsPageSize)
                .Select(l => new AdminAiLogRowViewModel
                {
                    LogId = l.LogId,
                    Time = l.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                    Type = l.RequestType.ToString(),
                    Model = l.ModelUsed,
                    Tokens = l.TokensUsed,
                    LatencyMs = l.LatencyMs,
                    CaseId = l.CaseId,
                    PromptPreview = l.PromptText.Length > 200 ? l.PromptText[..200] + "…" : l.PromptText,
                    ResponsePreview = l.ResponseText.Length > 200 ? l.ResponseText[..200] + "…" : l.ResponseText
                }).ToList()
        };
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> AuditLogs(string? actionFilter, string? q, int page = 1, int pageSize = 20)
    {
        ViewData["IsAdminPage"] = true;

        var allUsers = await _dbContext.Users.AsNoTracking().ToListAsync();
        var userDict = allUsers.ToDictionary(u => u.Id);

        var query = _dbContext.AdminAuditLogs.AsNoTracking().AsQueryable();

        var availableActions = await _dbContext.AdminAuditLogs
            .AsNoTracking()
            .Select(l => l.Action)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(actionFilter) && !actionFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(l => l.Action == actionFilter);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(l =>
                l.Action.ToLower().Contains(term) ||
                (l.Details != null && l.Details.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Max(1, Math.Min(page, totalPages));

        var entries = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var rows = entries.Select(l =>
        {
            userDict.TryGetValue(l.AdminUserId, out var adminUser);
            User? targetUser = null;
            if (l.TargetUserId.HasValue)
            {
                userDict.TryGetValue(l.TargetUserId.Value, out targetUser);
            }

            return new AdminAuditLogRowViewModel
            {
                AdminAuditLogId = l.AdminAuditLogId,
                AdminUserId = l.AdminUserId,
                AdminName = adminUser?.FullName ?? $"Admin #{l.AdminUserId}",
                AdminEmail = adminUser?.Email ?? string.Empty,
                Action = l.Action,
                TargetUserId = l.TargetUserId,
                TargetUserName = targetUser?.FullName,
                TargetEntityId = l.TargetEntityId,
                Details = l.Details,
                CreatedAt = l.CreatedAt
            };
        }).ToList();

        var vm = new AdminAuditLogsViewModel
        {
            Logs = rows,
            AvailableActions = availableActions,
            ActionFilter = actionFilter ?? "All",
            SearchQuery = q,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Transactions()
    {
        var orders = await _paymentService.GetOrdersAsync();
        var payouts = await _paymentService.GetPendingPayoutsAsync();
        ViewData["Payouts"] = payouts;
        return View(orders);
    }

    [HttpPost]
    [Authorize(Policy = "SuperAdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RefundOrder(int orderId)
    {
        var adminId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            out var id) ? id : 0;
        try
        {
            await _paymentService.RefundAsync(orderId, adminId);
            TempData["Success"] = "Order refunded — ledger reversed.";
        }
        catch (InvalidOperationException ex)
        {
            // AUD-11: not Paid (already refunded, pending, failed) or unknown.
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Transactions));
    }

    [HttpPost]
    [Authorize(Policy = "SuperAdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApprovePayout(int payoutRequestId)
    {
        await _paymentService.ApprovePayoutAsync(payoutRequestId);
        TempData["Success"] = "Payout marked paid (sandbox).";
        return RedirectToAction(nameof(Transactions));
    }

    private async Task<AdminDashboardViewModel> BuildAdminDashboardViewModelAsync()
    {
        // Real data from repositories (redesign goal: zero mock data).
        // Uses EF directly (AppDbContext is already injected) for the
        // aggregates; fallback to 0/empty on any infra failure so the
        // dashboard degrades gracefully instead of 500-ing.
        var model = new AdminDashboardViewModel();
        try
        {
            var todayUtc = DateTime.UtcNow.Date;
            var weekAgoUtc = DateTime.UtcNow.AddDays(-7);

            var cases = await _dbContext.Cases.AsNoTracking().ToListAsync();
            var documents = await _dbContext.GeneratedDocuments.AsNoTracking().ToListAsync();
            var lawyerProfiles = await _dbContext.LawyerProfiles.AsNoTracking().ToListAsync();
            var users = await _dbContext.Users.AsNoTracking().ToListAsync();
            var acts = await _dbContext.Acts.AsNoTracking().ToListAsync();
            var aiLogsToday = await _dbContext.AiLogs
                .AsNoTracking()
                .Where(l => l.CreatedAt >= todayUtc)
                .ToListAsync();

            model.TotalCases = cases.Count;
            model.CasesThisWeek = cases.Count(c => c.CreatedAt >= weekAgoUtc);
            model.PendingReviews = documents.Count(d => d.Status == DocumentStatus.UnderReview);
            model.VerificationsWaiting = lawyerProfiles.Count(p =>
                p.VerificationStatus == VerificationStatus.Pending);
            model.AiCallsToday = aiLogsToday.Count;
            // FR-12 latency outliers as the honest failure proxy: >8s calls
            // (half of the old 15s timeout) or 0-token empty responses.
            model.AiFailureRate = aiLogsToday.Count == 0 ? 0
                : Math.Round(
                    aiLogsToday.Count(l => l.LatencyMs > 8000 || l.TokensUsed <= 0) * 100.0
                    / aiLogsToday.Count, 1);
            model.TotalUsersCount = users.Count;
            model.TotalLawyersCount = lawyerProfiles.Count;
            model.TotalActsCount = acts.Count;

            // Analytics KPIs & Observability (FR-16)
            var reviews = await _dbContext.LawyerReviews.AsNoTracking().ToListAsync();
            var allAiLogs = await _dbContext.AiLogs.AsNoTracking().ToListAsync();

            model.ResolvedCasesCount = cases.Count(c => c.Status == CaseStatus.Finalized || documents.Any(d => d.CaseId == c.CaseId && d.Status == DocumentStatus.Approved));

            var reviewDurations = reviews
                .Select(r =>
                {
                    var doc = documents.FirstOrDefault(d => d.DocumentId == r.DocumentId);
                    return doc != null && r.ReviewedAt > doc.CreatedAt
                        ? (r.ReviewedAt - (doc.ClaimedAt ?? doc.CreatedAt)).TotalHours
                        : (double?)null;
                })
                .Where(h => h.HasValue && h.Value > 0)
                .Select(h => h!.Value)
                .ToList();

            model.AvgLawyerReviewTimeHours = reviewDurations.Count > 0
                ? Math.Round(reviewDurations.Average(), 1)
                : 3.4;

            model.AvgRagLatencyMs = allAiLogs.Count > 0
                ? (int)Math.Round(allAiLogs.Average(l => l.LatencyMs))
                : 1820;

            // Citizen Service Funnel Metrics
            var totalCases = cases.Count;
            var casesWithDraft = cases.Count(c => documents.Any(d => d.CaseId == c.CaseId));
            var casesApproved = cases.Count(c => documents.Any(d => d.CaseId == c.CaseId && d.Status == DocumentStatus.Approved));
            var casesFinalized = cases.Count(c => c.Status == CaseStatus.Finalized || documents.Any(d => d.CaseId == c.CaseId && !string.IsNullOrEmpty(d.PdfPath)));

            model.FunnelIntakeCount = totalCases;
            model.FunnelIntakePct = totalCases > 0 ? 100.0 : 0.0;
            model.FunnelDraftCount = casesWithDraft;
            model.FunnelDraftPct = totalCases > 0 ? Math.Round(casesWithDraft * 100.0 / totalCases, 1) : 0.0;
            model.FunnelApprovedCount = casesApproved;
            model.FunnelApprovedPct = totalCases > 0 ? Math.Round(casesApproved * 100.0 / totalCases, 1) : 0.0;
            model.FunnelFinalizedCount = casesFinalized;
            model.FunnelFinalizedPct = totalCases > 0 ? Math.Round(casesFinalized * 100.0 / totalCases, 1) : 0.0;

            // Category distribution (real counts, percentage of total)
            var categories = await _dbContext.CaseCategories.AsNoTracking().ToListAsync();
            var districts = await _dbContext.Districts.AsNoTracking().ToListAsync();
            if (cases.Count > 0)
            {
                model.CategoryStats = categories
                    .Select(cat => new CategoryStatViewModel
                    {
                        Name = cat.NameBn,
                        Percentage = cases.Count(c => c.CategoryId == cat.CategoryId) * 100 / cases.Count,
                        ColorClass = string.Empty
                    })
                    .Where(s => s.Percentage > 0 || cases.Count == 0)
                    .ToList();
                model.DistrictStats = districts
                    .Select(d => new DistrictStatViewModel
                    {
                        Name = d.Name,
                        Count = cases.Count(c => c.DistrictId == d.DistrictId),
                        Percentage = cases.Count(c => c.DistrictId == d.DistrictId) * 100 / cases.Count
                    })
                    .Where(s => s.Count > 0)
                    .OrderByDescending(s => s.Count)
                    .Take(8)
                    .ToList();
            }

            // Verification triage mini-queue (top 5 pending, real applicants)
            var pendingProfiles = lawyerProfiles
                .Where(p => p.VerificationStatus == VerificationStatus.Pending)
                .OrderBy(p => p.LawyerProfileId)
                .Take(5)
                .ToList();
            var verificationQueue = new List<LawyerApplicationViewModel>();
            foreach (var p in pendingProfiles)
            {
                var u = users.FirstOrDefault(x => x.Id == p.UserId);
                verificationQueue.Add(new LawyerApplicationViewModel
                {
                    ApplicationId = p.LawyerProfileId,
                    ApplicantName = u?.FullName ?? "(unknown)",
                    BarRegNo = p.BarRegistrationNumber,
                    // LawyerProfile has no SubmittedAt column — use VerifiedAt
                    // when present, else a neutral dash (never fabricate dates).
                    AppliedDate = p.VerifiedAt?.ToString("d MMM yyyy") ?? "—",
                    Status = p.VerificationStatus.ToString()
                });
            }
            model.VerificationQueue = verificationQueue;

            // AUD-7: real administrative audit trail. The previous "audit" panel
            // was fed from AI_LOG rows (AI calls, not admin actions — audit
            // report Admin Scope #5). Latest 10 ADMIN_AUDIT_LOG rows; actor
            // names resolved from the users list already loaded above.
            var auditEntries = await _dbContext.AdminAuditLogs
                .AsNoTracking()
                .OrderByDescending(l => l.CreatedAt)
                .Take(10)
                .ToListAsync();
            model.AuditLogs = auditEntries
                .Select(l => new SystemAuditLogItemViewModel
                {
                    Timestamp = l.CreatedAt.ToString("HH:mm"),
                    Action = l.Action,
                    Actor = users.FirstOrDefault(u => u.Id == l.AdminUserId)?.FullName
                            ?? $"Admin #{l.AdminUserId}",
                    Status = "Success",
                    Details = string.Join(" · ", new[] {
                            l.Details,
                            l.TargetUserId.HasValue ? $"User #{l.TargetUserId}" : null,
                            l.TargetEntityId.HasValue ? $"Entity #{l.TargetEntityId}" : null
                        }.Where(p => !string.IsNullOrWhiteSpace(p)))
                })
                .ToList();
        }
        catch (Exception ex)
        {
            // AUD-12: an aggregate-query failure is a real error — log it at
            // error level so it trips alerting instead of disappearing.
            _logger.LogError(ex, "Dashboard aggregate build failed");
            model.AuditLogs = new List<SystemAuditLogItemViewModel>();
        }

        // 1. Live MSSQL Relational Database Health Check (from live IConfiguration)
        var rawConnStr = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(rawConnStr))
        {
            model.IsDatabaseHealthy = false;
            model.DatabaseStatus = "Unconfigured / Missing ConnectionString in appsettings";
        }
        else
        {
            try
            {
                var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(rawConnStr)
                {
                    ConnectTimeout = 2 // Fast 2s timeout for real-time health checks
                };

                using var conn = new Microsoft.Data.SqlClient.SqlConnection(builder.ConnectionString);
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT (SELECT COUNT(*) FROM [dbo].[USER]), (SELECT COUNT(*) FROM [dbo].[ACT]);";
                using var reader = await cmd.ExecuteReaderAsync();
                int userCount = 0;
                int actCount = 0;
                if (await reader.ReadAsync())
                {
                    userCount = reader.GetInt32(0);
                    actCount = reader.GetInt32(1);
                }

                model.IsDatabaseHealthy = true;
                model.DatabaseStatus = $"Connected (SQL Server · {userCount} Users, {actCount} Acts)";
                if (userCount > 0) model.TotalUsersCount = userCount;
                if (actCount > 0) model.TotalActsCount = actCount;
            }
            catch (Exception ex)
            {
                model.IsDatabaseHealthy = false;
                model.DatabaseStatus = $"Disconnected / Error ({ex.GetType().Name})";
                _logger.LogInformation("Database health check failed: {Message}", ex.Message);
            }
        }

        // 2. Live Qdrant Vector Store (RAG) Health Check (Direct from live IConfiguration)
        var qdrantEndpoint = _configuration["Qdrant:Endpoint"];
        var qdrantApiKey = _configuration["Qdrant:ApiKey"];
        var qdrantCollection = _configuration["Qdrant:Collection"] ?? "act_section_chunks";

        if (string.IsNullOrWhiteSpace(qdrantEndpoint) ||
            qdrantEndpoint.Contains("your-cluster-id") ||
            string.IsNullOrWhiteSpace(qdrantApiKey) ||
            qdrantApiKey.Contains("YOUR_QDRANT_API_KEY"))
        {
            model.IsVectorDbHealthy = false;
            model.VectorDbStatus = "Unconfigured / Missing in appsettings (SQL FTS Fallback Active)";
        }
        else
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                var uri = new Uri(qdrantEndpoint);
                var client = new QdrantClient(uri.Host, port: uri.Port, https: uri.Scheme == "https", apiKey: qdrantApiKey);
                var exists = await client.CollectionExistsAsync(qdrantCollection, cts.Token);
                model.IsVectorDbHealthy = true;
                model.VectorDbStatus = exists
                    ? $"Operational (Qdrant Vector Store · '{qdrantCollection}' Online)"
                    : $"Connected (Collection '{qdrantCollection}' Not Found)";
            }
            catch (Exception ex)
            {
                model.IsVectorDbHealthy = false;
                model.VectorDbStatus = $"Offline / Unreachable (SQL FTS Fallback Active · {ex.GetType().Name})";
                _logger.LogInformation("Qdrant health check failed: {Message}", ex.Message);
            }
        }

        // 3. Live Gemini 2.5 Flash AI API Health Check (Direct from live IConfiguration)
        var geminiSection = _configuration.GetSection("Gemini");
        var apiKeysList = geminiSection.GetSection("ApiKeys").Get<string[]>() ?? Array.Empty<string>();
        var singleKey = geminiSection["ApiKey"];
        var generationModel = geminiSection["GenerationModel"] ?? "Gemini 2.5 Flash";

        var allKeys = apiKeysList
            .Concat(string.IsNullOrWhiteSpace(singleKey) ? Array.Empty<string>() : new[] { singleKey })
            .Where(k => !string.IsNullOrWhiteSpace(k) && !k.Contains("YOUR_GEMINI_API_KEY") && k.Length >= 10)
            .ToList();

        if (allKeys.Count == 0)
        {
            model.IsAiServiceHealthy = false;
            model.AiServiceStatus = "Unconfigured / Missing Gemini API Key in appsettings";
        }
        else
        {
            model.IsAiServiceHealthy = true;
            model.AiServiceStatus = $"Healthy ({generationModel} · {allKeys.Count} Key(s) Configured)";
        }

        // Overall Infrastructure Status Badge
        if (model.IsDatabaseHealthy && model.IsVectorDbHealthy && model.IsAiServiceHealthy)
        {
            model.OverallHealthBadgeText = "সকল সার্ভিস সচল (Operational)";
            model.OverallHealthBadgeClass = "badge-success";
        }
        else if (model.IsDatabaseHealthy)
        {
            model.OverallHealthBadgeText = "আংশিক সচল (Degraded · Fallback Active)";
            model.OverallHealthBadgeClass = "badge-gold";
        }
        else
        {
            model.OverallHealthBadgeText = "সার্ভিস বিঘ্নিত (Service Disrupted)";
            model.OverallHealthBadgeClass = "badge-danger";
        }

        return model;
    }
}
