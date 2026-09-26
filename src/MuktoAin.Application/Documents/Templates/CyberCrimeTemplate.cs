using System.Text;
using MuktoAin.Application.DTOs;
using MuktoAin.Domain.Constants;
using MuktoAin.Domain.Entities;
using MuktoAin.Domain.Enums;

namespace MuktoAin.Application.Documents.Templates;

/// <summary>
/// Cyber crime & digital complaint/GD template under the Cyber Security Act 2023 /
/// Information and Communication Technology (ICT) Act 2006.
/// </summary>
public class CyberCrimeTemplate : IDocumentTemplate, IBanglaDocumentVariant
{
    public DocumentType DocumentType => DocumentType.CyberCrime;

    public Task<string> RenderAsync(Case caseEntity, RightsExplanationDto explanation)
    {
        var districtName = caseEntity.District?.Name ?? "________";
        var sb = new StringBuilder();

        // ── Header ──────────────────────────────────────────────
        sb.AppendLine("TO");
        sb.AppendLine("The Officer-in-Charge / Cyber Crime Investigation Division");
        sb.AppendLine($"Police Station / Cyber Centre: {districtName}, Bangladesh");
        sb.AppendLine();

        // ── Subject ─────────────────────────────────────────────
        var primarySection = explanation.CitedSections.FirstOrDefault();
        var sectionRef = primarySection != null
            ? $" Under Section {primarySection.SectionNumber} of {primarySection.ActTitle}"
            : " Under the Cyber Security Act / ICT Act";
        sb.AppendLine($"Subject: Complaint / GD regarding Cyber Offence, Online Fraud, or Digital Harassment{sectionRef}");
        sb.AppendLine();

        // ── Salutation ──────────────────────────────────────────
        sb.AppendLine("Respected Officer,");
        sb.AppendLine();

        // ── Complainant Introduction ────────────────────────────
        sb.AppendLine($"I, the undersigned complainant, resident of {districtName}, do hereby lodge this formal complaint " +
                       "regarding digital offences including online impersonation, cyber extortion, unauthorized system access, " +
                       "financial fraud, or harassment perpetrated via electronic and internet platforms:");
        sb.AppendLine();

        // ── Facts of the Incident ───────────────────────────────
        sb.AppendLine("FACTS OF THE INCIDENT:");
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
            sb.AppendLine("• Cyber Security Act, 2023 / Information and Communication Technology Act, 2006 (Relevant digital offence provisions)");
            sb.AppendLine();
        }

        // ── Rights Explanation ──────────────────────────────────
        if (!string.IsNullOrWhiteSpace(explanation.Explanation))
        {
            sb.AppendLine("YOUR RIGHTS UNDER CYBER LAW:");
            sb.AppendLine(new string('─', 40));
            sb.AppendLine(explanation.Explanation);
            sb.AppendLine();
        }

        // ── Relief / Remedy Sought ──────────────────────────────
        sb.AppendLine("RELIEF SOUGHT:");
        sb.AppendLine(new string('─', 40));
        sb.AppendLine("In light of the aforesaid facts, the complainant respectfully requests that the law enforcement authorities:");
        sb.AppendLine("1. Register this formal complaint / General Diary (GD) and initiate cyber forensic tracing;");
        sb.AppendLine("2. Request BTRC / relevant platform authorities for removal, takedown, or blocking of malicious content/accounts;");
        sb.AppendLine("3. Apprehend the perpetrators and take necessary legal actions under the applicable cyber laws.");
        sb.AppendLine();

        // ── Supporting Digital Evidence ─────────────────────────
        sb.AppendLine("SUPPORTING DIGITAL EVIDENCE / ATTACHMENTS:");
        sb.AppendLine(new string('─', 40));
        sb.AppendLine("• Screenshots of defamatory posts, messages, or fraudulent transactions");
        sb.AppendLine("• Relevant Profile URLs, post links, email headers, or phone/MFS numbers");
        sb.AppendLine("• Mobile Financial Service (MFS) / Bank transaction IDs and statements");
        sb.AppendLine("• Communication logs / call recordings / timestamps");
        sb.AppendLine();

        // ── Declaration ─────────────────────────────────────────
        sb.AppendLine("DECLARATION:");
        sb.AppendLine(new string('─', 40));
        sb.AppendLine("I declare that the digital evidence and information provided above are genuine and submitted in good faith.");
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
        var sb = new StringBuilder();

        sb.AppendLine("বরাবর");
        sb.AppendLine("ভারপ্রাপ্ত কর্মকর্তা / সাইবার ক্রাইম তদন্ত বিভাগ");
        sb.AppendLine($"থানা / সাইবার তদন্ত ইউনিট: {districtName ?? BanglaOnlyRender.Placeholder}, বাংলাদেশ");
        sb.AppendLine();

        var primarySection = explanation.CitedSections.FirstOrDefault();
        var sectionRef = primarySection != null
            ? $" {primarySection.ActTitle}-এর ধারা {primarySection.SectionNumber}-এর অধীনে"
            : " সাইবার নিরাপত্তা আইনের অধীনে";
        sb.AppendLine($"বিষয়: সাইবার অপরাধ, অনলাইন প্রতারণা বা ডিজিটাল হয়রানি সংক্রান্ত অভিযোগ/জিডি{sectionRef}");
        sb.AppendLine();

        sb.AppendLine("জনাব,");
        sb.AppendLine();

        sb.AppendLine($"আমি, নিম্নস্বাক্ষরকারী অভিযোগকারী, {districtName ?? BanglaOnlyRender.Placeholder}-এর বাসিন্দা, " +
                       "অনলাইনে সংঘটিত প্রতারণা, সম্মানহানি, হ্যাকিং বা সাইবার হয়রানির বিষয়ে আইনানুগ ব্যবস্থা গ্রহণের জন্য " +
                       "এই অভিযোগপত্র দাখিল করছি:");
        sb.AppendLine();

        sb.AppendLine("ঘটনাবলির বিবরণ:");
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
            sb.AppendLine("• সাইবার নিরাপত্তা আইন, ২০২৩ / তথ্য ও যোগাযোগ প্রযুক্তি আইন, ২০০৬ (প্রাসঙ্গিক ডিজিটাল অপরাধ বিধান)");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(explanation.Explanation))
        {
            sb.AppendLine("সাইবার আইনে আপনার অধিকার:");
            sb.AppendLine(BanglaOnlyRender.Rule);
            sb.AppendLine(explanation.Explanation);
            sb.AppendLine();
        }

        sb.AppendLine("প্রার্থিত প্রতিকার:");
        sb.AppendLine(BanglaOnlyRender.Rule);
        sb.AppendLine("অতএব বিনীত অনুরোধ:");
        sb.AppendLine("১. এই অভিযোগের ভিত্তিতে সাধারণ ডায়েরি (জিডি) / নিয়মিত মামলা রুজু করে ডিজিটাল তদন্ত শুরু করা হোক;");
        sb.AppendLine("২. বিটিআরসি বা সংশ্লিষ্ট সামাজিক যোগাযোগ মাধ্যম কর্তৃপক্ষের মাধ্যমে ক্ষতিকর কনটেন্ট অপসারণ বা আইডি ব্লক করার উদ্যোগ গ্রহণ করা হোক;");
        sb.AppendLine("৩. অপরাধীদের চিহ্নিত করে আইনগত শাস্তিমূলক ব্যবস্থা গ্রহণ করা হোক।");
        sb.AppendLine();

        sb.AppendLine("সংযুক্ত ডিজিটাল প্রমাণক:");
        sb.AppendLine(BanglaOnlyRender.Rule);
        sb.AppendLine("• আপত্তিকর পোস্ট, বার্তা বা আর্থিক প্রতারণার স্ক্রিনশট");
        sb.AppendLine("• সংশ্লিষ্ট প্রোফাইল লিংক (URL), মোবাইল নম্বর বা এমএফএস অ্যাকাউন্ট নম্বর");
        sb.AppendLine("• ব্যাংক/এমএফএস লেনদেনের ট্রানজেকশন আইডি ও স্টেটমেন্ট");
        sb.AppendLine("• অন্যান্য প্রাসঙ্গিক ডিজিটাল লগ বা তথ্য");
        sb.AppendLine();

        sb.AppendLine("ঘোষণা:");
        sb.AppendLine(BanglaOnlyRender.Rule);
        sb.AppendLine("আমি এই মর্মে নিশ্চিত করছি যে, প্রদত্ত তথ্য ও প্রমাণকসমূহ প্রকৃত ও সঠিক।");
        sb.AppendLine();

        return BanglaOnlyRender.AppendClosing(sb, "অভিযোগকারী", districtName);
    }
}
