using System.Text;
using MuktoAin.Application.DTOs;
using MuktoAin.Domain.Constants;
using MuktoAin.Domain.Entities;
using MuktoAin.Domain.Enums;

namespace MuktoAin.Application.Documents.Templates;

/// <summary>
/// Bangladesh District Labour Court complaint template.
/// Follows the format specified in Arpita_plan.md Step 2.3 —
/// structured complaint under the Bangladesh Labour Act, 2006.
/// </summary>
public class LabourComplaintTemplate : IDocumentTemplate, IBanglaDocumentVariant
{
    public DocumentType DocumentType => DocumentType.LabourComplaint;

    public Task<string> RenderAsync(Case caseEntity, RightsExplanationDto explanation)
    {
        var districtName = caseEntity.District?.Name ?? "________";
        var employer = CaseFileReader.Read(caseEntity.Description, "employerName");
        var wage = CaseFileReader.Read(caseEntity.Description, "monthlyWage");
        var complaintType = CaseFileReader.ReadOrNull(caseEntity.Description, "natureOfComplaint");
        var unpaidPeriod = CaseFileReader.ReadOrNull(caseEntity.Description, "unpaidPeriod");

        var sb = new StringBuilder();

        // ── Header ──────────────────────────────────────────────
        sb.AppendLine("TO");
        sb.AppendLine("The Inspector General / District Labour Court");
        sb.AppendLine($"{districtName}, Bangladesh");
        sb.AppendLine();

        // ── Subject ─────────────────────────────────────────────
        var primarySection = explanation.CitedSections.FirstOrDefault();
        var sectionRef = primarySection != null
            ? $"Section {primarySection.SectionNumber} of"
            : string.Empty;
        var complaintLabel = complaintType ?? "Labour Act Violation";
        sb.AppendLine($"Subject: Complaint regarding {complaintLabel} against {employer}, Under {sectionRef} the Bangladesh Labour Act, 2006");
        sb.AppendLine();

        // ── Salutation ──────────────────────────────────────────
        sb.AppendLine("Respected Sir/Madam,");
        sb.AppendLine();

        // ── Complainant Introduction ────────────────────────────
        sb.AppendLine($"I, the undersigned, resident of {districtName}, employed by {employer} " +
                       $"at a monthly wage of {wage}, do hereby submit this complaint " +
                       "for the following violation(s) of the Bangladesh Labour Act, 2006:");
        if (unpaidPeriod != null)
        {
            sb.AppendLine();
            sb.AppendLine($"Period of violation: {unpaidPeriod}");
        }
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
            sb.AppendLine("  [No specific sections retrieved — consult a qualified advocate]");
            sb.AppendLine();
        }

        // ── Rights Explanation ──────────────────────────────────
        if (!string.IsNullOrWhiteSpace(explanation.Explanation))
        {
            sb.AppendLine("YOUR RIGHTS UNDER APPLICABLE LAW:");
            sb.AppendLine(new string('─', 40));
            sb.AppendLine(explanation.Explanation);
            sb.AppendLine();
        }

        // ── Relief Sought ───────────────────────────────────────
        sb.AppendLine("RELIEF SOUGHT:");
        sb.AppendLine(new string('─', 40));
        sb.AppendLine("Based on the above facts and the applicable legal provisions cited herein,");
        sb.AppendLine("the complainant respectfully prays for appropriate relief including but not");
        sb.AppendLine("limited to compensation, recovery of unpaid dues, and/or any other remedy the");
        sb.AppendLine("Honourable Court deems fit and proper.");
        sb.AppendLine();

        // ── Declaration ─────────────────────────────────────────
        sb.AppendLine("DECLARATION:");
        sb.AppendLine(new string('─', 40));
        sb.AppendLine("I hereby declare that the information provided above is true and correct to");
        sb.AppendLine("the best of my knowledge and belief. I understand that any false statement");
        sb.AppendLine("may result in legal consequences.");
        sb.AppendLine();

        // ── Signature Block ─────────────────────────────────────
        sb.AppendLine($"Date: {DateTime.UtcNow:dd MMMM, yyyy}");
        sb.AppendLine("Complainant: ________________________");
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
        var employer = CaseFileReader.Read(caseEntity.Description, "employerName");
        var wage = CaseFileReader.Read(caseEntity.Description, "monthlyWage");
        var complaintType = CaseFileReader.ReadOrNull(caseEntity.Description, "natureOfComplaint");
        var unpaidPeriod = CaseFileReader.ReadOrNull(caseEntity.Description, "unpaidPeriod");

        var sb = new StringBuilder();

        // ── Header ──────────────────────────────────────────────
        sb.AppendLine("বরাবর");
        sb.AppendLine("শ্রম পরিদপ্তর / জেলা শ্রম আদালত");
        sb.AppendLine($"{districtName ?? BanglaOnlyRender.Placeholder}, বাংলাদেশ");
        sb.AppendLine();

        // ── Subject ─────────────────────────────────────────────
        var primarySection = explanation.CitedSections.FirstOrDefault();
        var sectionRef = primarySection != null
            ? $"ধারা {primarySection.SectionNumber} (বাংলাদেশ শ্রম আইন, ২০০৬)-এর অধীনে"
            : "বাংলাদেশ শ্রম আইন, ২০০৬-এর অধীনে";
        var complaintLabel = complaintType ?? "শ্রম আইন লঙ্ঘন সংক্রান্ত";
        sb.AppendLine($"বিষয়: {employer}-এর বিরুদ্ধে {complaintLabel} বিষয়ে {sectionRef} অভিযোগ");
        sb.AppendLine();

        // ── Salutation ──────────────────────────────────────────
        sb.AppendLine("মহোদয়,");
        sb.AppendLine();

        // ── Complainant Introduction ────────────────────────────
        sb.AppendLine($"আমি, স্বাক্ষরকারী, {districtName ?? BanglaOnlyRender.Placeholder}-এর বাসিন্দা, {employer}-এ " +
                       $"মাসিক {wage} বেতনে কর্মরত, বাংলাদেশ শ্রম আইন, ২০০৬-এর নিম্নলিখিত লঙ্ঘনের বিষয়ে এই অভিযোগ জমা দিচ্ছি:");
        if (unpaidPeriod != null)
        {
            sb.AppendLine();
            sb.AppendLine($"লঙ্ঘন/বকেয়ার সময়কাল: {unpaidPeriod}");
        }
        sb.AppendLine();

        // ── Facts ───────────────────────────────────────────────
        sb.AppendLine("মামলার ঘটনাবলি:");
        sb.AppendLine(BanglaOnlyRender.Rule);
        sb.AppendLine(caseEntity.Description);
        sb.AppendLine();

        // ── Legal provisions ────────────────────────────────────
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
            sb.AppendLine("  [নির্দিষ্ট কোনো ধারা পাওয়া যায়নি — একজন যোগ্য আইনজীবীর পরামর্শ নিন]");
            sb.AppendLine();
        }

        // ── Rights explanation ──────────────────────────────────
        if (!string.IsNullOrWhiteSpace(explanation.Explanation))
        {
            sb.AppendLine("প্রযোজ্য আইনে আপনার অধিকার:");
            sb.AppendLine(BanglaOnlyRender.Rule);
            sb.AppendLine(explanation.Explanation);
            sb.AppendLine();
        }

        // ── Relief ──────────────────────────────────────────────
        sb.AppendLine("প্রার্থিত প্রতিকার:");
        sb.AppendLine(BanglaOnlyRender.Rule);
        sb.AppendLine("উপরোক্ত ঘটনা ও উদ্ধৃত আইনি বিধানের ভিত্তিতে, অভিযোগকারী বকেয়া পাওনা আদায়, ক্ষতিপূরণ " +
                       "এবং/অথবা মাননীয় আদালত যথাযথ মনে করেন এমন অন্য যেকোনো প্রতিকার প্রার্থনা করছেন।");
        sb.AppendLine();

        // ── Declaration ─────────────────────────────────────────
        sb.AppendLine("ঘোষণা:");
        sb.AppendLine(BanglaOnlyRender.Rule);
        sb.AppendLine("আমি এই মর্মে ঘোষণা করছি যে, উপরোক্ত তথ্য আমার জানামতে ও বিশ্বাসমতে সম্পূর্ণ সত্য ও সঠিক। " +
                       "মিথ্যা বিবৃতি প্রদানের ফলে আইনগত পরিণতি হতে পারে তা আমি অবগত।");
        sb.AppendLine();

        return BanglaOnlyRender.AppendClosing(sb, "অভিযোগকারী", districtName);
    }
}
