using System.Text;
using MuktoAin.Application.DTOs;
using MuktoAin.Domain.Constants;
using MuktoAin.Domain.Entities;
using MuktoAin.Domain.Enums;

namespace MuktoAin.Application.Documents.Templates;

/// <summary>
/// Family and domestic dispute legal application/petition template under the Family Courts Act 2023 /
/// Muslim Family Laws Ordinance 1961, Dowry Prohibition Act 2018, and Domestic Violence (Prevention and Protection) Act 2010.
/// </summary>
public class FamilyDisputeTemplate : IDocumentTemplate, IBanglaDocumentVariant
{
    public DocumentType DocumentType => DocumentType.FamilyDispute;

    public Task<string> RenderAsync(Case caseEntity, RightsExplanationDto explanation)
    {
        var districtName = caseEntity.District?.Name ?? "________";
        var disputeType = CaseFileReader.Read(caseEntity.Description, "disputeType");
        var relation = CaseFileReader.Read(caseEntity.Description, "relationToOpponent");
        var marriageDate = CaseFileReader.Read(caseEntity.Description, "marriageDate");
        var children = CaseFileReader.ReadOrNull(caseEntity.Description, "childrenInfo");
        var demandOrViolence = CaseFileReader.Read(caseEntity.Description, "demandOrViolenceDetails");

        var sb = new StringBuilder();

        // ── Header ──────────────────────────────────────────────
        sb.AppendLine("IN THE COURT OF THE ASSISTANT JUDGE / FAMILY COURT");
        sb.AppendLine($"District: {districtName}, Bangladesh");
        sb.AppendLine();

        // ── Subject ─────────────────────────────────────────────
        var primarySection = explanation.CitedSections.FirstOrDefault();
        var sectionRef = primarySection != null
            ? $" Under Section {primarySection.SectionNumber} of {primarySection.ActTitle}"
            : " Under the Family Courts Act / Muslim Family Laws";
        sb.AppendLine($"Subject: Application / Plaint regarding {disputeType} (Against {relation}){sectionRef}");
        sb.AppendLine();

        // ── Salutation ──────────────────────────────────────────
        sb.AppendLine("Respected Court,");
        sb.AppendLine();

        // ── Petitioner Introduction ─────────────────────────────
        sb.AppendLine($"The humble petition of the petitioner, resident of {districtName}, most respectfully states:");
        sb.AppendLine();
        sb.AppendLine("FAMILY & MARITAL PARTICULARS:");
        sb.AppendLine(new string('─', 40));
        sb.AppendLine($"  Dispute Type: {disputeType}");
        sb.AppendLine($"  Opponent Relationship: {relation}");
        sb.AppendLine($"  Date of Marriage: {marriageDate}");
        if (children != null) sb.AppendLine($"  Children: {children}");
        sb.AppendLine($"  Specific Harm / Demand / Dispute: {demandOrViolence}");
        sb.AppendLine();

        // ── Facts of the Case ───────────────────────────────────
        sb.AppendLine("FACTS OF THE CASE:");
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
            sb.AppendLine("• Family Courts Act, 2023 / Muslim Family Laws Ordinance, 1961 / Domestic Violence (Prevention and Protection) Act, 2010 / Dowry Prohibition Act, 2018");
            sb.AppendLine();
        }

        // ── Rights Explanation ──────────────────────────────────
        if (!string.IsNullOrWhiteSpace(explanation.Explanation))
        {
            sb.AppendLine("YOUR RIGHTS UNDER FAMILY LAW:");
            sb.AppendLine(new string('─', 40));
            sb.AppendLine(explanation.Explanation);
            sb.AppendLine();
        }

        // ── Relief / Remedy Sought ──────────────────────────────
        sb.AppendLine("RELIEF / PRAYER SOUGHT:");
        sb.AppendLine(new string('─', 40));
        sb.AppendLine("In the premises aforesaid, the petitioner respectfully prays that the Court may be pleased to:");
        sb.AppendLine("1. Pass a decree/order directing the respondent to pay regular monthly maintenance (khorposh) and dower (mahr) arrears;");
        sb.AppendLine("2. Grant lawful guardianship, visitation rights, or interim custody of the minor child(ren);");
        sb.AppendLine("3. Issue appropriate protection and restraint orders under the Domestic Violence Act to safeguard the petitioner's security.");
        sb.AppendLine();

        // ── Supporting Evidence / Attachments ───────────────────
        sb.AppendLine("SUPPORTING EVIDENCE / ATTACHMENTS:");
        sb.AppendLine(new string('─', 40));
        sb.AppendLine($"• Nikahnama / Marriage Certificate dated {marriageDate} (if available)");
        if (children != null) sb.AppendLine("• Birth certificate(s) of minor child(ren)");
        sb.AppendLine("• Evidence of respondent's income / employment details");
        sb.AppendLine("• Medical reports / General Diary records (in cases of domestic violence or harassment)");
        sb.AppendLine();

        // ── Verification ────────────────────────────────────────
        sb.AppendLine("VERIFICATION:");
        sb.AppendLine(new string('─', 40));
        sb.AppendLine("I, the petitioner above named, do hereby verify and declare that the statements made above are true");
        sb.AppendLine("to my personal knowledge and belief.");
        sb.AppendLine();

        // ── Signature Block ─────────────────────────────────────
        sb.AppendLine($"Date: {DateTime.UtcNow:dd MMMM, yyyy}");
        sb.AppendLine("Petitioner: ________________________");
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
        var disputeType = CaseFileReader.Read(caseEntity.Description, "disputeType");
        var relation = CaseFileReader.Read(caseEntity.Description, "relationToOpponent");
        var marriageDate = CaseFileReader.Read(caseEntity.Description, "marriageDate");
        var children = CaseFileReader.ReadOrNull(caseEntity.Description, "childrenInfo");
        var demandOrViolence = CaseFileReader.Read(caseEntity.Description, "demandOrViolenceDetails");

        var sb = new StringBuilder();

        sb.AppendLine("বিজ্ঞ সহকারী জজ / পারিবারিক আদালত সমীপে");
        sb.AppendLine($"জেলা: {districtName ?? BanglaOnlyRender.Placeholder}, বাংলাদেশ");
        sb.AppendLine();

        var primarySection = explanation.CitedSections.FirstOrDefault();
        var sectionRef = primarySection != null
            ? $" {primarySection.ActTitle}-এর ধারা {primarySection.SectionNumber}-এর অধীনে"
            : " পারিবারিক আদালত আইনের অধীনে";
        sb.AppendLine($"বিষয়: {relation}-এর বিরুদ্ধে {disputeType} সংক্রান্ত আরজি/আবেদন{sectionRef}");
        sb.AppendLine();

        sb.AppendLine("বিনীত নিবেদন এই যে,");
        sb.AppendLine();

        sb.AppendLine($"আমি, নিম্নস্বাক্ষরকারী আবেদনকারী/বাদী, {districtName ?? BanglaOnlyRender.Placeholder}-এর বাসিন্দা, " +
                       $"বিবাদীর ({relation}) বিরুদ্ধে নিম্নবর্ণিত পারিবারিক অধিকার ক্ষুণ্ণ ও প্রতিকার প্রার্থনায় এই আবেদন পেশ করছি:");
        sb.AppendLine();

        sb.AppendLine("পারিবারিক ও বৈবাহিক বিবরণ:");
        sb.AppendLine(BanglaOnlyRender.Rule);
        sb.AppendLine($"  বিরোধের ধরন: {disputeType}");
        sb.AppendLine($"  বিবাদীর সাথে সম্পর্ক: {relation}");
        sb.AppendLine($"  বিবাহের তারিখ: {marriageDate}");
        if (children != null) sb.AppendLine($"  সন্তান: {children}");
        sb.AppendLine($"  দাবি বা নির্যাতনের বিবরণ: {demandOrViolence}");
        sb.AppendLine();

        sb.AppendLine("মামলার ঘটনাবলি:");
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
            sb.AppendLine("• পারিবারিক আদালত আইন, ২০২৩ / মুসলিম পারিবারিক আইন অধ্যাদেশ, ১৯৬১ / পারিবারিক সহিংসতা (প্রতিরোধ ও সুরক্ষা) আইন, ২০১০ / যৌতুক নিরোধ আইন, ২০১৮");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(explanation.Explanation))
        {
            sb.AppendLine("পারিবারিক আইনে আপনার অধিকার:");
            sb.AppendLine(BanglaOnlyRender.Rule);
            sb.AppendLine(explanation.Explanation);
            sb.AppendLine();
        }

        sb.AppendLine("প্রার্থিত প্রতিকার:");
        sb.AppendLine(BanglaOnlyRender.Rule);
        sb.AppendLine("অতএব বিনীত প্রার্থনা, আদালত সন্তুষ্ট হয়ে ন্যায়বিচারের স্বার্থে:");
        sb.AppendLine("১. বাদী ও সন্তানদের জন্য নিয়মিত মাসিক ভরণপোষণ (খোরপোষ) ও অপরিশোধিত দেনমোহর আদায়ের ডিক্রি প্রদান করবেন;");
        sb.AppendLine("২. নাবালক সন্তানের আইনগত হেফাজত বা তত্ত্বাবধানের আদেশ প্রদান করবেন;");
        sb.AppendLine("৩. বাদীর নিরাপত্তা বিধানে পারিবারিক সহিংসতা প্রতিরোধ আইনের আওতায় সুরক্ষা আদেশ প্রদান করবেন।");
        sb.AppendLine();

        sb.AppendLine("সংযুক্ত প্রমাণপত্র:");
        sb.AppendLine(BanglaOnlyRender.Rule);
        sb.AppendLine($"• {marriageDate} তারিখের নিকাহনামা / বিবাহ নিবন্ধন সনদ (যদি থাকে)");
        if (children != null) sb.AppendLine("• সন্তানের জন্ম নিবন্ধন সনদ");
        sb.AppendLine("• বিবাদীর পেশা ও আয়ের প্রমাণক");
        sb.AppendLine("• চিকিৎসা সনদ / পূর্ববর্তী জিডির কপি (সহিংসতা বা নির্যাতনের ক্ষেত্রে)");
        sb.AppendLine();

        sb.AppendLine("ঘোষণা ও সত্যায়ন:");
        sb.AppendLine(BanglaOnlyRender.Rule);
        sb.AppendLine("আমি এই মর্মে সত্যায়ন করছি যে, উপরোক্ত সমস্ত বিবরণ আমার জ্ঞান ও বিশ্বাসমতে সম্পূর্ণ সত্য ও সঠিক।");
        sb.AppendLine();

        return BanglaOnlyRender.AppendClosing(sb, "বাদী / আবেদনকারী", districtName);
    }
}
