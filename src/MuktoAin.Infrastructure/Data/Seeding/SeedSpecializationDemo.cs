using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MuktoAin.Domain.Entities;
using MuktoAin.Domain.Enums;
using MuktoAin.Domain.Interfaces;

namespace MuktoAin.Infrastructure.Data.Seeding;

// Dev-only fixtures for the "My field" queue filter (LAW-QUEUE-SPEC): one
// APPROVED lawyer per specialization plus one unclaimed UnderReview document
// per case category, so each login sees a different slice of the same queue.
//
// The sixth lawyer ("Maritime law") matches no category on purpose -- that
// login exercises the fallback banner (queue shows everything).
//
// Idempotent: short-circuits once the first lawyer login exists. Called from
// Program.cs only when app.Environment.IsDevelopment().
//
// Depends on SeedCategories / SeedDistricts (categories are looked up by name,
// not hardcoded, since CASE_CATEGORY ids are IDENTITY-assigned).
public static class SeedSpecializationDemo
{
    public const string Password = "Field!2026";

    // Specialization text -> the category its keywords match in
    // LawyerQueueNotifier.CategoryKeywords. Keep both in sync.
    private static readonly (string Email, string FullName, string BarNumber, string Specialization)[] Lawyers =
    {
        ("labour@demo.muktoain.bd",      "Advocate Rehana Karim",    "SPEC-BAR-2026-0001", "শ্রম আইন / Labour and employment law"),
        ("criminal@demo.muktoain.bd",    "Advocate Tanvir Ahmed",    "SPEC-BAR-2026-0002", "ফৌজদারি আইন / Criminal law"),
        ("rti@demo.muktoain.bd",         "Advocate Sadia Noor",      "SPEC-BAR-2026-0003", "তথ্য অধিকার / Right to Information"),
        ("consumer@demo.muktoain.bd",    "Advocate Imran Hossain",   "SPEC-BAR-2026-0004", "ভোক্তা অধিকার / Consumer law"),
        ("land@demo.muktoain.bd",        "Advocate Tariqul Islam",   "SPEC-BAR-2026-0007", "ভূমি ও সম্পত্তি আইন / Land and property law"),
        ("family@demo.muktoain.bd",      "Advocate Shireen Akhter",  "SPEC-BAR-2026-0008", "পারিবারিক আইন / Family and domestic law"),
        ("cyber@demo.muktoain.bd",       "Advocate Faisal Mahmud",   "SPEC-BAR-2026-0009", "সাইবার নিরাপত্তা ও ডিজিটাল আইন / Cyber law and digital security"),
        ("environment@demo.muktoain.bd", "Advocate Nazmul Haque",   "SPEC-BAR-2026-0010", "পরিবেশ আইন / Environmental law"),
        ("general@demo.muktoain.bd",     "Advocate Nafisa Rahman",   "SPEC-BAR-2026-0005", "সাধারণ আইন / General practice"),
        ("nomatch@demo.muktoain.bd",     "Advocate Zahid Chowdhury", "SPEC-BAR-2026-0006", "Maritime law"),
    };

    // Category name (as seeded from data/categories.json) -> the queued case.
    private static readonly (string CategoryName, DocumentType DocumentType, string Title, string Description, string Draft)[] QueuedCases =
    {
        ("Labour Complaint", DocumentType.LabourComplaint,
            "তিন মাসের বকেয়া বেতন",
            "কারখানা কর্তৃপক্ষ তিন মাস ধরে বেতন পরিশোধ করছে না।",
            "খসড়া: শ্রম আইন ২০০৬ অনুযায়ী বকেয়া মজুরি আদায়ের অভিযোগপত্র।"),
        ("General Diary (GD)", DocumentType.GeneralDiary,
            "মোটরসাইকেল চুরির জিডি",
            "বাসার সামনে থেকে মোটরসাইকেলটি চুরি হয়ে গেছে।",
            "খসড়া: থানায় সাধারণ ডায়েরি দায়েরের আবেদন।"),
        ("RTI Request", DocumentType.RtiRequest,
            "ইউনিয়ন পরিষদের বরাদ্দের তথ্য",
            "গত অর্থবছরের উন্নয়ন বরাদ্দের হিসাব জানতে চাই।",
            "খসড়া: তথ্য অধিকার আইন ২০০৯ অনুযায়ী তথ্য প্রাপ্তির আবেদন।"),
        ("Consumer Complaint", DocumentType.ConsumerComplaint,
            "মেয়াদোত্তীর্ণ পণ্য বিক্রয়",
            "সুপারশপ থেকে কেনা পণ্যের মেয়াদ আগেই শেষ হয়ে গিয়েছিল।",
            "খসড়া: ভোক্তা অধিকার সংরক্ষণ আইন ২০০৯ অনুযায়ী অভিযোগপত্র।"),
        ("Land & Property Dispute", DocumentType.LandPropertyDispute,
            "জমির অবৈধ দখল ও নামজারি বিরোধ",
            "পৈতৃক সম্পত্তি অন্য পক্ষ জোরপূর্বক দখল করে রেখেছে।",
            "খসড়া: ভূমি বিরোধ ও উচ্ছেদ প্রতিকারের আবেদন।"),
        ("Family & Domestic Dispute", DocumentType.FamilyDispute,
            "পারিবারিক ভরণপোষণ ও দেনমোহর দাবি",
            "স্বামী বিগত ছয় মাস যাবত ভরণপোষণ প্রদান করছেন না।",
            "খসড়া: পারিবারিক আদালতে ভরণপোষণ ও খোরপোষ দাবির আরজি।"),
        ("Cyber Crime & Digital Complaint", DocumentType.CyberCrime,
            "অনলাইন হয়রানি ও ভুয়া ফেসবুক আইডি",
            "আমার নাম ও ছবি ব্যবহার করে অপপ্রচার চালানো হচ্ছে।",
            "খসড়া: সাইবার অপরাধ তদন্তে সাধারণ ডায়েরি ও অভিযোগ।"),
        ("Environmental Complaint", DocumentType.EnvironmentalComplaint,
            "কারখানার বিষাক্ত বর্জ্য নির্গমন",
            "স্থানীয় নদী ও কৃষিজমিতে অপরিশোধিত বর্জ্য ফেলা হচ্ছে।",
            "খসড়া: পরিবেশ অধিদপ্তরে দূষণ প্রতিকার ও মোবাইল কোর্টের আবেদন।"),
    };

    public static async Task SeedAsync(
        AppDbContext context,
        UserManager<User> userManager,
        IEncryptionService encryptionService,
        ILogger logger)
    {
        var firstLawyer = await userManager.FindByEmailAsync(Lawyers[0].Email);
        if (firstLawyer is not null)
        {
            // Self-heal demo specialization cases if key rotated in dev
            var specCitizen = await userManager.FindByEmailAsync("fieldcitizen@demo.muktoain.bd");
            if (specCitizen is not null)
            {
                var specCases = await context.Cases.Where(c => c.UserId == specCitizen.Id).ToListAsync();
                var healed = false;
                for (var i = 0; i < specCases.Count; i++)
                {
                    var sc = specCases[i];
                    try
                    {
                        encryptionService.Decrypt(sc.Title);
                    }
                    catch
                    {
                        var tmpl = QueuedCases[i % QueuedCases.Length];
                        sc.Title = encryptionService.Encrypt(tmpl.Title);
                        sc.Description = encryptionService.Encrypt(tmpl.Description);
                        healed = true;
                    }
                }
                if (healed)
                {
                    await context.SaveChangesAsync();
                    logger.LogInformation("Self-healed undecryptable specialization demo cases.");
                }
            }
            return; // already seeded
        }

        var categories = await context.CaseCategories.ToListAsync();
        var district = await context.Districts.OrderBy(d => d.DistrictId).FirstOrDefaultAsync();
        if (categories.Count == 0 || district is null)
        {
            logger.LogWarning(
                "SeedSpecializationDemo: no categories/districts found -- run SeedCategories/SeedDistricts first. Skipping.");
            return;
        }

        foreach (var (email, fullName, barNumber, specialization) in Lawyers)
        {
            var user = await CreateUserAsync(userManager, logger, fullName, email, UserRole.Lawyer);
            context.LawyerProfiles.Add(new LawyerProfile
            {
                UserId = user.Id,
                BarRegistrationNumber = barNumber,
                Specialization = specialization,
                VerificationStatus = VerificationStatus.Approved,
                VerifiedAt = DateTime.UtcNow,
            });
        }

        // One citizen owns all four queued cases, so Track shows them together.
        var citizen = await CreateUserAsync(
            userManager, logger, "Shamima Begum", "fieldcitizen@demo.muktoain.bd", UserRole.Citizen);
        await context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var queued = 0;
        var age = 0;
        foreach (var (categoryName, documentType, title, description, draft) in QueuedCases)
        {
            var category = categories.FirstOrDefault(c => c.Name == categoryName);
            if (category is null)
            {
                logger.LogWarning("SeedSpecializationDemo: category '{Category}' not found. Skipping its case.", categoryName);
                continue;
            }

            age++;
            var createdAt = now.AddHours(-6 * age); // staggered so the SLA column varies
            var demoCase = new Case
            {
                UserId = citizen.Id,
                CategoryId = category.CategoryId,
                DistrictId = district.DistrictId,
                Title = encryptionService.Encrypt(title),
                Description = encryptionService.Encrypt(description),
                Language = "bn",
                Status = CaseStatus.UnderReview,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
            };
            context.Cases.Add(demoCase);
            await context.SaveChangesAsync();

            // Unclaimed: every lawyer whose field matches can open it.
            context.GeneratedDocuments.Add(new GeneratedDocument
            {
                CaseId = demoCase.CaseId,
                DocumentType = documentType,
                ContentDraft = draft,
                Status = DocumentStatus.UnderReview,
                AssignedLawyerProfileId = null,
                CreatedAt = createdAt,
            });
            queued++;
        }

        await context.SaveChangesAsync();

        logger.LogInformation(
            "Seeded specialization demo: {Lawyers} approved lawyers, {Cases} queued cases. Password for all: {Password}",
            Lawyers.Length, queued, Password);
    }

    private static async Task<User> CreateUserAsync(
        UserManager<User> userManager,
        ILogger logger,
        string fullName,
        string email,
        UserRole role)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null) return existing;

        var user = new User
        {
            FullName = fullName,
            UserName = email,
            Email = email,
            Role = role,
            AccountStatus = AccountStatus.Active,
            PreferredLanguage = "bn",
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
        };

        var result = await userManager.CreateAsync(user, Password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            logger.LogError("SeedSpecializationDemo: failed to create {Email}: {Errors}", email, errors);
            throw new InvalidOperationException($"Specialization demo seeding failed for '{email}'.");
        }

        return user;
    }
}
