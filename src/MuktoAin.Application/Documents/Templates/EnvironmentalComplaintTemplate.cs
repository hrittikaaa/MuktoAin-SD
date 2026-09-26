using System.Text;
using MuktoAin.Application.DTOs;
using MuktoAin.Domain.Constants;
using MuktoAin.Domain.Entities;
using MuktoAin.Domain.Enums;

namespace MuktoAin.Application.Documents.Templates;

/// <summary>
/// Environmental complaint / public grievance template under the Bangladesh Environment Conservation Act 1995 /
/// Environment Court Act 2010 / Forest Act 1927 / Bangladesh Water Act 2013.
/// </summary>
public class EnvironmentalComplaintTemplate : IDocumentTemplate, IBanglaDocumentVariant
{
    public DocumentType DocumentType => DocumentType.EnvironmentalComplaint;

    public Task<string> RenderAsync(Case caseEntity, RightsExplanationDto explanation)
    {
        var districtName = caseEntity.District?.Name ?? "________";
        var sb = new StringBuilder();

        // ── Header ──────────────────────────────────────────────
        sb.AppendLine("TO");
        sb.AppendLine("The Director General / Director (Enforcement)");
        sb.AppendLine("Department of Environment (DoE), Bangladesh");
        sb.AppendLine($"District Office: {districtName}, Bangladesh");
        sb.AppendLine();

        // ── Subject ─────────────────────────────────────────────
        var primarySection = explanation.CitedSections.FirstOrDefault();
        var sectionRef = primarySection != null
            ? $" Under Section {primarySection.SectionNumber} of {primarySection.ActTitle}"
            : " Under the Bangladesh Environment Conservation Act, 1995";
        sb.AppendLine($"Subject: Formal Complaint / Report on Environmental Violation and Pollution Hazards{sectionRef}");
        sb.AppendLine();

        // ── Salutation ──────────────────────────────────────────
        sb.AppendLine("Respected Authority,");
        sb.AppendLine();

        // ── Complainant Introduction ────────────────────────────
        sb.AppendLine($"I/We, the undersigned citizen(s)/affected resident(s) of {districtName}, do hereby draw your urgent attention " +
                       "and lodge this formal complaint regarding severe environmental destruction, industrial pollution, unauthorized hill cutting, " +
                       "wetland/river encroachment, or ecological damage occurring in our locality:");
        sb.AppendLine();

        // ── Facts of the Violation ──────────────────────────────
        sb.AppendLine("FACTS OF THE ENVIRONMENTAL VIOLATION:");
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
            sb.AppendLine("• Bangladesh Environment Conservation Act, 1995 / Environment Court Act, 2010 / Bangladesh Water Act, 2013");
            sb.AppendLine();
        }

        // ── Rights Explanation ──────────────────────────────────
        if (!string.IsNullOrWhiteSpace(explanation.Explanation))
        {
            sb.AppendLine("YOUR RIGHTS UNDER ENVIRONMENTAL LAW:");
            sb.AppendLine(new string('─', 40));
            sb.AppendLine(explanation.Explanation);
            sb.AppendLine();
        }

        // ── Relief / Remedy Sought ──────────────────────────────
        sb.AppendLine("RELIEF / ENFORCEMENT SOUGHT:");
        sb.AppendLine(new string('─', 40));
        sb.AppendLine("In consideration of public health, ecological safety, and constitutional rights to life and clean environment, the complainant prays:");
        sb.AppendLine("1. That an immediate on-spot mobile court / enforcement drive and environmental impact assessment be executed;");
        sb.AppendLine("2. That closure orders, environmental clearance cancellation, or stop-work notices be issued against the offending entity;");
        sb.AppendLine("3. That ecological restoration, decontamination, and statutory environmental compensation be mandated.");
        sb.AppendLine();

        // ── Supporting Evidence / Attachments ───────────────────
        sb.AppendLine("SUPPORTING EVIDENCE / ATTACHMENTS:");
        sb.AppendLine(new string('─', 40));
        sb.AppendLine("• Photographic / Video evidence showing environmental violation, smoke, chemical discharge, or encroachment");
        sb.AppendLine("• Location coordinates / Google Maps pin / Mouza details of the site");
        sb.AppendLine("• Local collective petitions / Newspaper or media investigative reports (if available)");
        sb.AppendLine();

        // ── Declaration ─────────────────────────────────────────
        sb.AppendLine("DECLARATION:");
        sb.AppendLine(new string('─', 40));
        sb.AppendLine("I declare that this complaint is submitted for public interest and environmental conservation in good faith.");
        sb.AppendLine();

        // ── Signature Block ─────────────────────────────────────
        sb.AppendLine($"Date: {DateTime.UtcNow:dd MMMM, yyyy}");
        sb.AppendLine("Complainant / Public Representative: ________________________");
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
        sb.AppendLine("মহাপরিচালক / পরিচালক (এনফোর্সমেন্ট)");
        sb.AppendLine("পরিবেশ অধিদপ্তর, বাংলাদেশ");
        sb.AppendLine($"জেলা/বিভাগীয় কার্যালয়: {districtName ?? BanglaOnlyRender.Placeholder}, বাংলাদেশ");
        sb.AppendLine();

        var primarySection = explanation.CitedSections.FirstOrDefault();
        var sectionRef = primarySection != null
            ? $" {primarySection.ActTitle}-এর ধারা {primarySection.SectionNumber}-এর অধীনে"
            : " বাংলাদেশ পরিবেশ সংরক্ষণ আইন, ১৯৯৫-এর অধীনে";
        sb.AppendLine($"বিষয়: পরিবেশ দূষণ, বেআইনি দখল বা পরিবেশ বিধ্বংসী কার্যকলাপ প্রতিকারের অভিযোগ{sectionRef}");
        sb.AppendLine();

        sb.AppendLine("মহোদয়,");
        sb.AppendLine();

        sb.AppendLine($"আমি/আমরা, {districtName ?? BanglaOnlyRender.Placeholder}-এর সচেতন নাগরিক ও ভুক্তভোগী অধিবাসী, " +
                       "আমাদের এলাকায় পরিবেশ আইন লঙ্ঘন, ক্ষতিকর বর্জ্য নির্গমন, অবৈধ নদী/জলাশয় দখল বা বৃক্ষ নিধনের বিরুদ্ধে " +
                       "জরুরি প্রশাসনিক পদক্ষেপ গ্রহণের জন্য এই অভিযোগ দাখিল করছি:");
        sb.AppendLine();

        sb.AppendLine("পরিবেশ লঙ্ঘনের বিবরণ:");
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
            sb.AppendLine("• বাংলাদেশ পরিবেশ সংরক্ষণ আইন, ১৯৯৫ / পরিবেশ আদালত আইন, ২০১০ / বাংলাদেশ পানি আইন, ২০১৩");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(explanation.Explanation))
        {
            sb.AppendLine("পরিবেশ আইনে আপনার অধিকার:");
            sb.AppendLine(BanglaOnlyRender.Rule);
            sb.AppendLine(explanation.Explanation);
            sb.AppendLine();
        }

        sb.AppendLine("প্রার্থিত প্রতিকার ও আইনি পদক্ষেপ:");
        sb.AppendLine(BanglaOnlyRender.Rule);
        sb.AppendLine("জনস্বাস্থ্য ও প্রতিবেশ সুরক্ষায় পরিবেশ অধিদপ্তরের প্রতি বিনীত প্রার্থনা:");
        sb.AppendLine("১. তাৎক্ষণিকভাবে সরজমিন পরিদর্শন এবং ভ্রাম্যমাণ আদালত (মোবাইল কোর্ট) পরিচালনা করা হোক;");
        sb.AppendLine("২. দূষণকারী/লঙ্ঘনকারী প্রতিষ্ঠানের বিরুদ্ধে পরিবেশ ছাড়পত্র বাতিল বা কার্যক্রম স্থগিতাদেশ জারি করা হোক;");
        sb.AppendLine("৩. পরিবেশগত ক্ষতিপূরণ নির্ধারণ এবং পূর্বাবস্থায় ফিরিয়ে আনার প্রয়োজনীয় নির্দেশ প্রদান করা হোক।");
        sb.AppendLine();

        sb.AppendLine("সংযুক্ত প্রমাণক:");
        sb.AppendLine(BanglaOnlyRender.Rule);
        sb.AppendLine("• দূষণ, বর্জ্য নির্গমন বা দখলের আলোকচিত্র / ভিডিও ফুটেজ");
        sb.AppendLine("• ঘটনাস্থলের সঠিক ভৌগোলিক অবস্থান / গুগল ম্যাপ লোকেশন");
        sb.AppendLine("• স্থানীয় বাসিন্দাদের গণস্বাক্ষর বা সংবাদপত্রের প্রতিবেদন (যদি থাকে)");
        sb.AppendLine();

        sb.AppendLine("ঘোষণা:");
        sb.AppendLine(BanglaOnlyRender.Rule);
        sb.AppendLine("আমি/আমরা এই মর্মে ঘোষণা করছি যে, এই অভিযোগ জনস্বার্থে ও প্রাকৃতিক পরিবেশ সংরক্ষণের উদ্দেশ্যে পেশ করা হয়েছে।");
        sb.AppendLine();

        return BanglaOnlyRender.AppendClosing(sb, "অভিযোগকারী / স্থানীয় অধিবাসী প্রতিনিধি", districtName);
    }
}
