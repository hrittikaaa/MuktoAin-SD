using System.Text;
using MuktoAin.Application.DTOs;
using MuktoAin.Domain.Constants;
using MuktoAin.Domain.Entities;
using MuktoAin.Domain.Enums;

namespace MuktoAin.Application.Documents.Templates;

/// <summary>
/// Land and property dispute complaint/application template under Transfer of Property Act 1882,
/// State Acquisition & Tenancy Act 1950, and Land Crime Prevention & Redress Act 2023.
/// </summary>
public class LandPropertyTemplate : IDocumentTemplate, IBanglaDocumentVariant
{
    public DocumentType DocumentType => DocumentType.LandPropertyDispute;

    public Task<string> RenderAsync(Case caseEntity, RightsExplanationDto explanation)
    {
        var districtName = caseEntity.District?.Name ?? "________";
        var landLoc = CaseFileReader.Read(caseEntity.Description, "landLocation");
        var deedInfo = CaseFileReader.Read(caseEntity.Description, "deedOrDocumentInfo");
        var area = CaseFileReader.Read(caseEntity.Description, "landArea");
        var disputeType = CaseFileReader.ReadOrNull(caseEntity.Description, "natureOfDispute");
        var opponent = CaseFileReader.ReadOrNull(caseEntity.Description, "opponentRelation");

        var sb = new StringBuilder();

        // ── Header ──────────────────────────────────────────────
        sb.AppendLine("TO");
        sb.AppendLine("The Assistant Commissioner (Land) / Upazila Nirbahi Officer (UNO)");
        sb.AppendLine($"District Office / Upazila Office: {districtName}, Bangladesh");
        sb.AppendLine();

        // ── Subject ─────────────────────────────────────────────
        var primarySection = explanation.CitedSections.FirstOrDefault();
        var sectionRef = primarySection != null
            ? $" Under Section {primarySection.SectionNumber} of {primarySection.ActTitle}"
            : string.Empty;
        var disputeLabel = disputeType ?? "Land & Property Dispute";
        sb.AppendLine($"Subject: Application / Complaint regarding {disputeLabel}{sectionRef}");
        sb.AppendLine();

        // ── Salutation ──────────────────────────────────────────
        sb.AppendLine("Respected Sir/Madam,");
        sb.AppendLine();

        // ── Applicant Introduction ──────────────────────────────
        sb.AppendLine($"I, the undersigned applicant/landholder, resident of {districtName}, do hereby submit this formal " +
                       "application/complaint regarding the following land dispute:");
        sb.AppendLine();
        sb.AppendLine("PROPERTY DETAILS:");
        sb.AppendLine(new string('─', 40));
        sb.AppendLine($"  Location (Mouza/Plot/Upazila): {landLoc}");
        sb.AppendLine($"  Deed / Ownership Document: {deedInfo}");
        sb.AppendLine($"  Land Area: {area}");
        if (opponent != null) sb.AppendLine($"  Opponent: {opponent}");
        sb.AppendLine();

        // ── Facts of the Dispute ────────────────────────────────
        sb.AppendLine("FACTS OF THE DISPUTE:");
        sb.AppendLine(new string('─', 40));
        sb.AppendLine(caseEntity.Description);
        sb.AppendLine();

        // ── Applicable Legal Provisions ─────────────────────────
        sb.AppendLine("APPLICABLE LEGAL PROVISIONS:");
        sb.AppendLine(new string('─', 40));
        if (explanation.CitedSections.Count > 0)
        {
            foreach (var section in explanation.CitedSections)
            {
                sb.AppendLine($"• {section.ActTitle}, Section {section.SectionNumber}:");
                sb.AppendLine($"  {section.SectionText}");
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("• State Acquisition and Tenancy Act, 1950 / Transfer of Property Act, 1882 / Land Crime Prevention and Redress Act, 2023");
            sb.AppendLine();
        }

        // ── Rights Explanation ──────────────────────────────────
        if (!string.IsNullOrWhiteSpace(explanation.Explanation))
        {
            sb.AppendLine("YOUR RIGHTS UNDER LAND LAW:");
            sb.AppendLine(new string('─', 40));
            sb.AppendLine(explanation.Explanation);
            sb.AppendLine();
        }

        // ── Relief / Remedy Sought ──────────────────────────────
        sb.AppendLine("RELIEF / REMEDY SOUGHT:");
        sb.AppendLine(new string('─', 40));
        sb.AppendLine("Based on the aforementioned facts and applicable statutory provisions, the applicant respectfully prays:");
        sb.AppendLine("1. That an on-site inquiry, land survey verification, and necessary administrative hearing be conducted;");
        sb.AppendLine("2. That lawful possession, proper mutation (namjari), and correct revenue/khatian records be upheld;");
        sb.AppendLine("3. That appropriate restraint orders or remedial action be taken against unauthorized encroachment and illegal dispossession.");
        sb.AppendLine();

        // ── Supporting Evidence / Attachments ───────────────────
        sb.AppendLine("SUPPORTING EVIDENCE / ATTACHMENTS:");
        sb.AppendLine(new string('─', 40));
        sb.AppendLine($"• Title deed / Document: {deedInfo}");
        sb.AppendLine("• Latest Khatian (CS/SA/RS/City/BS), DCR, and land development tax (Khajna) receipts");
        sb.AppendLine("• Land sketch / site map / demarcation survey report (if available)");
        sb.AppendLine("• Mutation (Namjari) proposal or rejection copy (if applicable)");
        sb.AppendLine();

        // ── Declaration ─────────────────────────────────────────
        sb.AppendLine("DECLARATION:");
        sb.AppendLine(new string('─', 40));
        sb.AppendLine("I hereby declare that the particulars furnished above are true and correct to the best of my knowledge,");
        sb.AppendLine("information, and belief, and that I hold legitimate lawful interest in the subject property.");
        sb.AppendLine();

        // ── Signature Block ─────────────────────────────────────
        sb.AppendLine($"Date: {DateTime.UtcNow:dd MMMM, yyyy}");
        sb.AppendLine("Applicant: ________________________");
        sb.AppendLine($"District: {districtName}");
        sb.AppendLine();

        // ── Disclaimer Stamp (Surface 3 of 3) ───────────────────
        sb.AppendLine(new string('═', 60));
        sb.AppendLine(Disclaimers.Legal);
        sb.AppendLine(Disclaimers.LegalBangla);
        sb.AppendLine(new string('═', 60));

        return Task.FromResult(sb.ToString());
    }

    public async Task<string> RenderBanglaOnlyAsync(Case caseEntity, RightsExplanationDto explanation)
    {
        var districtName = caseEntity.District?.Name;
        var landLoc = CaseFileReader.Read(caseEntity.Description, "landLocation");
        var deedInfo = CaseFileReader.Read(caseEntity.Description, "deedOrDocumentInfo");
        var area = CaseFileReader.Read(caseEntity.Description, "landArea");
        var disputeType = CaseFileReader.ReadOrNull(caseEntity.Description, "natureOfDispute");
        var opponent = CaseFileReader.ReadOrNull(caseEntity.Description, "opponentRelation");

        var sb = new StringBuilder();

        sb.AppendLine("বরাবর");
        sb.AppendLine("সহকারী কমিশনার (ভূমি) / উপজেলা নির্বাহী অফিসার (ইউএনও)");
        sb.AppendLine($"উপজেলা/জেলা কার্যালয়: {districtName ?? BanglaOnlyRender.Placeholder}, বাংলাদেশ");
        sb.AppendLine();

        var primarySection = explanation.CitedSections.FirstOrDefault();
        var sectionRef = primarySection != null
            ? $" {primarySection.ActTitle}-এর ধারা {primarySection.SectionNumber}-এর অধীনে"
            : string.Empty;
        var disputeLabel = disputeType != null ? $" ({disputeType})" : "";
        sb.AppendLine($"বিষয়: ভূমি ও সম্পত্তি বিরোধ সংক্রান্ত প্রতিকারের আবেদন{disputeLabel}{sectionRef}");
        sb.AppendLine();

        sb.AppendLine("মহোদয়,");
        sb.AppendLine();

        sb.AppendLine($"আমি, নিম্নস্বাক্ষরকারী আবেদনকারী/জমির বৈধ স্বত্বাধিকারী, {districtName ?? BanglaOnlyRender.Placeholder}-এর বাসিন্দা, " +
                       "আমার বৈধ স্বত্বাধীন স্থাবর সম্পত্তির বিরোধের বিষয়ে নিম্নোক্ত তথ্য উপস্থাপনপূর্বক আইনগত প্রতিকার চেয়ে এই আবেদন পেশ করছি:");
        sb.AppendLine();

        sb.AppendLine("সম্পত্তির বিবরণ:");
        sb.AppendLine(BanglaOnlyRender.Rule);
        sb.AppendLine($"  অবস্থান (মৌজা/দাগ/উপজেলা): {landLoc}");
        sb.AppendLine($"  দলিল / মালিকানার কাগজ: {deedInfo}");
        sb.AppendLine($"  জমির পরিমাণ: {area}");
        if (opponent != null) sb.AppendLine($"  প্রতিপক্ষ: {opponent}");
        sb.AppendLine();

        sb.AppendLine("বিরোধের ঘটনাবলি:");
        sb.AppendLine(BanglaOnlyRender.Rule);
        sb.AppendLine(caseEntity.Description);
        sb.AppendLine();

        sb.AppendLine("প্রযোজ্য আইনি বিধান:");
        sb.AppendLine(BanglaOnlyRender.Rule);
        if (explanation.CitedSections.Count > 0)
        {
            foreach (var section in explanation.CitedSections)
            {
                sb.AppendLine($"• {section.ActTitle}, ধারা {section.SectionNumber}:");
                sb.AppendLine($"  {section.SectionText}");
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("• রাষ্ট্রীয় অধিগ্রহণ ও প্রজাস্বত্ব আইন, ১৯৫০ / সম্পত্তি হস্তান্তর আইন, ১৮৮২ / ভূমি অপরাধ প্রতিরোধ ও প্রতিকার আইন, ২০২৩");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(explanation.Explanation))
        {
            sb.AppendLine("ভূমি আইনে আপনার অধিকার:");
            sb.AppendLine(BanglaOnlyRender.Rule);
            sb.AppendLine(explanation.Explanation);
            sb.AppendLine();
        }

        sb.AppendLine("প্রার্থিত প্রতিকার:");
        sb.AppendLine(BanglaOnlyRender.Rule);
        sb.AppendLine("উপরোক্ত ঘটনা ও প্রযোজ্য আইনি বিধানের ভিত্তিতে আবেদনকারী বিনীতভাবে প্রার্থনা করছেন:");
        sb.AppendLine("১. বিরোধীয় ভূমির সঠিক দখল ও রেকর্ডীয় মালিকানা নির্ধারণে সরজমিনে তদন্ত পরিচালনা করা হোক;");
        sb.AppendLine("২. যথাযথ নামজারি (মিউটেশন), জমাখারিজ ও খাজনা দাখিলা প্রদানের সুরক্ষা নিশ্চিত করা হোক;");
        sb.AppendLine("৩. অবৈধ উচ্ছেদ, সীমানা লঙ্ঘন বা বেআইনি দখল প্রতিরোধে প্রয়োজনীয় প্রশাসনিক ও আইনগত ব্যবস্থা গ্রহণ করা হোক।");
        sb.AppendLine();

        sb.AppendLine("সংযুক্ত প্রমাণপত্র:");
        sb.AppendLine(BanglaOnlyRender.Rule);
        sb.AppendLine($"• মূল বা সার্টিফাইড রেজিস্ট্রি দলিল / কাগজপত্র: {deedInfo}");
        sb.AppendLine("• হালনাগাদ খতিয়ান (সিএস/এসএ/আরএস/সিটি/বিএস), ডিসিআর ও ভূমি উন্নয়ন কর (খাজনা) দাখিলা");
        sb.AppendLine("• সীমানা নকশা / মৌজা ম্যাপের চিহ্নিত অংশ (যদি থাকে)");
        sb.AppendLine("• নামজারি আবেদন বা পূর্ববর্তী আদেশের অনুলিপি");
        sb.AppendLine();

        sb.AppendLine("ঘোষণা:");
        sb.AppendLine(BanglaOnlyRender.Rule);
        sb.AppendLine("আমি এই মর্মে ঘোষণা করছি যে, উপরোক্ত বিবরণ আমার জ্ঞান, তথ্য ও বিশ্বাসমতে সত্য ও সঠিক, " +
                       "এবং বর্ণিত সম্পত্তিতে আমার বৈধ স্বত্ব ও স্বার্থ বিদ্যমান রয়েছে।");
        sb.AppendLine();

        return BanglaOnlyRender.AppendClosing(sb, "আবেদনকারী", districtName);
    }
}
