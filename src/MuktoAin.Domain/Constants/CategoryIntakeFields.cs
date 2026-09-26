namespace MuktoAin.Domain.Constants;

/// <summary>
/// Per-category required fields for structured legal intake.
/// C# owns this checklist — the AI cannot skip fields.
/// </summary>
public static class CategoryIntakeFields
{
    public record IntakeField(
        string Key,
        string NameEn,
        string NameBn,
        string ExampleEn,
        string ExampleBn,
        bool IsCritical = true // Critical = blocks readyToExplain; non-critical = nice-to-have
    );

    private static readonly Dictionary<string, IReadOnlyList<IntakeField>> Registry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LabourComplaint"] = new IntakeField[]
        {
            new("employerName", "Employer / company name", "নিয়োগকর্তা / কোম্পানির নাম", "e.g., ABC Garments Ltd.", "যেমন: এবিসি গার্মেন্টস লিমিটেড", true),
            new("natureOfComplaint", "Type of complaint", "অভিযোগের ধরন", "e.g., unpaid wages / wrongful termination / workplace injury", "যেমন: বকেয়া বেতন / অন্যায় বরখাস্ত / কর্মক্ষেত্রে দুর্ঘটনা", true),
            new("monthlyWage", "Monthly wage amount", "মাসিক বেতনের পরিমাণ", "e.g., 12,000 BDT", "যেমন: ১২,০০০ টাকা", true),
            new("employmentDuration", "How long employed", "কতদিন কাজ করেছেন", "e.g., 3 years", "যেমন: ৩ বছর", false),
            new("unpaidPeriod", "Period of non-payment or violation", "বকেয়া/লঙ্ঘনের সময়কাল", "e.g., last 3 months", "যেমন: গত ৩ মাস", true),
        },
        ["GeneralDiary"] = new IntakeField[]
        {
            new("incidentType", "Type of incident", "ঘটনার ধরন", "e.g., theft / threat / lost document / missing person", "যেমন: চুরি / হুমকি / কাগজপত্র হারানো / নিখোঁজ ব্যক্তি", true),
            new("incidentDateTime", "Exact date and time of incident", "ঘটনার সঠিক তারিখ ও সময়", "e.g., 15 July 2025, around 3 PM", "যেমন: ১৫ জুলাই ২০২৫, বিকাল ৩টার দিকে", true),
            new("incidentLocation", "Exact location of incident", "ঘটনার সঠিক স্থান", "e.g., Mirpur-10 bus stop, near the mosque", "যেমন: মিরপুর-১০ বাস স্টপ, মসজিদের পাশে", true),
            new("itemOrPersonDescription", "Description of lost item / suspect / missing person", "হারানো জিনিস / সন্দেহভাজন / নিখোঁজ ব্যক্তির বিবরণ", "e.g., blue Samsung phone / man in black shirt, approx 30 years old", "যেমন: নীল স্যামসাং ফোন / কালো শার্ট পরা লোক, বয়স প্রায় ৩০", true),
        },
        ["RtiRequest"] = new IntakeField[]
        {
            new("targetAuthority", "Government office or authority", "সরকারি দপ্তর বা কর্তৃপক্ষ", "e.g., Upazila Parishad, Savar / Roads & Highways Division", "যেমন: সাভার উপজেলা পরিষদ / সড়ক ও জনপথ বিভাগ", true),
            new("informationSought", "Specific information requested", "নির্দিষ্ট কী তথ্য চান", "e.g., Budget allocation for road repair in Ward 5 for FY 2024-25", "যেমন: ৫ নং ওয়ার্ডে ২০২৪-২৫ অর্থবছরে সড়ক মেরামতের বরাদ্দ", true),
            new("reasonForRequest", "Why you need this information", "কেন এই তথ্য প্রয়োজন", "e.g., Public accountability / research / personal interest", "যেমন: জনগণের জবাবদিহিতা / গবেষণা / ব্যক্তিগত প্রয়োজন", false),
            new("previousAttempt", "Did you already ask and get refused?", "আগে চেয়ে প্রত্যাখ্যাত হয়েছেন কি?", "e.g., Yes, verbal request on 10 June 2025, no response", "যেমন: হ্যাঁ, ১০ জুন ২০২৫-এ মৌখিকভাবে চেয়েছিলাম, কোনো উত্তর পাইনি", false),
        },
        ["ConsumerComplaint"] = new IntakeField[]
        {
            new("productOrService", "Product or service name", "পণ্য বা সেবার নাম", "e.g., Samsung Galaxy A15 / Grameenphone broadband", "যেমন: স্যামসাং গ্যালাক্সি A15 / গ্রামীণফোন ব্রডব্যান্ড", true),
            new("sellerOrProvider", "Seller or service provider name and location", "বিক্রেতা বা সেবাদাতার নাম ও ঠিকানা", "e.g., Star Gadgets, Bashundhara City, Dhaka", "যেমন: স্টার গ্যাজেটস, বসুন্ধরা সিটি, ঢাকা", true),
            new("purchaseDate", "Date of purchase", "ক্রয়ের তারিখ", "e.g., 5 March 2025", "যেমন: ৫ মার্চ ২০২৫", true),
            new("amountPaid", "Amount paid", "কত টাকা দিয়েছেন", "e.g., 18,500 BDT", "যেমন: ১৮,৫০০ টাকা", true),
            new("defectOrIssue", "Specific defect or issue", "নির্দিষ্ট ত্রুটি বা সমস্যা", "e.g., Screen went black after 2 weeks, shop refused refund", "যেমন: ২ সপ্তাহ পর স্ক্রিন কালো হয়ে গেছে, দোকান ফেরত দিতে অস্বীকার করেছে", true),
        },
        ["LandPropertyDispute"] = new IntakeField[]
        {
            new("landLocation", "Mouza, Plot/Dag, and Upazila of the land", "জমির মৌজা, দাগ নম্বর ও উপজেলা", "e.g., Mouza Kaliganj, Dag 234, Upazila Savar", "যেমন: মৌজা কালীগঞ্জ, দাগ ২৩৪, উপজেলা সাভার", true),
            new("deedOrDocumentInfo", "Deed number or ownership document details", "দলিল নম্বর বা মালিকানার কাগজের তথ্য", "e.g., Deed No. 1234/2020, Sub-Registry Office Savar", "যেমন: দলিল নং ১২৩৪/২০২০, সাভার সাব-রেজিস্ট্রি অফিস", true),
            new("landArea", "Area of land", "জমির পরিমাণ", "e.g., 10 decimal / 1 bigha", "যেমন: ১০ শতাংশ / ১ বিঘা", true),
            new("natureOfDispute", "Type of land dispute", "ভূমি বিরোধের ধরন", "e.g., forced transfer / eviction / boundary dispute / mutation problem", "যেমন: জোরপূর্বক হস্তান্তর / উচ্ছেদ / সীমানা বিরোধ / নামজারি সমস্যা", true),
            new("opponentRelation", "Opponent and their relation to you", "প্রতিপক্ষ ও তাদের সাথে সম্পর্ক", "e.g., Cousin Karim, neighboring landowner / government acquisition", "যেমন: চাচাতো ভাই করিম, পাশের জমির মালিক / সরকারি অধিগ্রহণ", true),
        },
        ["FamilyDispute"] = new IntakeField[]
        {
            new("disputeType", "Type of family dispute", "পারিবারিক বিরোধের ধরন", "e.g., divorce / maintenance / custody / dowry / domestic violence", "যেমন: তালাক / ভরণপোষণ / সন্তানের হেফাজত / যৌতুক / পারিবারিক সহিংসতা", true),
            new("relationToOpponent", "Your relationship to the other party", "প্রতিপক্ষের সাথে আপনার সম্পর্ক", "e.g., husband / wife / in-laws", "যেমন: স্বামী / স্ত্রী / শ্বশুরবাড়ি", true),
            new("marriageDate", "Date of marriage", "বিবাহের তারিখ", "e.g., 15 January 2018", "যেমন: ১৫ জানুয়ারি ২০১৮", true),
            new("childrenInfo", "Children and their ages (if any)", "সন্তান ও তাদের বয়স (যদি থাকে)", "e.g., 1 son (age 5), 1 daughter (age 2)", "যেমন: ১ ছেলে (৫ বছর), ১ মেয়ে (২ বছর)", false),
            new("demandOrViolenceDetails", "Specific demand or violence details", "নির্দিষ্ট দাবি বা সহিংসতার বিবরণ", "e.g., demanding 5 lakh BDT / beaten on 10 June", "যেমন: ৫ লক্ষ টাকা দাবি / ১০ জুন মারধর করেছে", true),
        },
        ["CyberCrime"] = new IntakeField[]
        {
            new("crimeType", "Type of cyber crime", "সাইবার অপরাধের ধরন", "e.g., online fraud / hacking / cyberbullying / identity theft / defamation", "যেমন: অনলাইন প্রতারণা / হ্যাকিং / সাইবার বুলিং / পরিচয় চুরি / মানহানি", true),
            new("platformOrMedium", "Platform or medium used", "কোন প্ল্যাটফর্ম বা মাধ্যমে", "e.g., Facebook / bKash / WhatsApp / email", "যেমন: ফেসবুক / বিকাশ / হোয়াটসঅ্যাপ / ইমেইল", true),
            new("financialLoss", "Amount lost (if any)", "আর্থিক ক্ষতির পরিমাণ (যদি থাকে)", "e.g., 25,000 BDT via bKash", "যেমন: বিকাশে ২৫,০০০ টাকা", false),
            new("evidenceAvailable", "Evidence you have", "আপনার কাছে কী প্রমাণ আছে", "e.g., screenshots of chat / transaction ID / threatening messages", "যেমন: চ্যাটের স্ক্রিনশট / লেনদেনের আইডি / হুমকির মেসেজ", true),
        },
        ["EnvironmentalComplaint"] = new IntakeField[]
        {
            new("pollutionType", "Type of environmental violation", "পরিবেশ লঙ্ঘনের ধরন", "e.g., factory pollution / illegal tree felling / river encroachment / noise", "যেমন: কারখানার দূষণ / অবৈধ গাছ কাটা / নদী দখল / শব্দ দূষণ", true),
            new("sourceOfPollution", "Who or what is causing it", "কে বা কী কারণে", "e.g., XYZ Tannery, Hazaribagh / construction site next door", "যেমন: হাজারীবাগ এক্সওয়াইজেড ট্যানারি / পাশের নির্মাণ সাইট", true),
            new("affectedArea", "Affected area and population", "ক্ষতিগ্রস্ত এলাকা ও জনসংখ্যা", "e.g., 500 families in Ward 3, drinking water contaminated", "যেমন: ৩ নং ওয়ার্ডের ৫০০ পরিবার, পানি দূষিত", true),
            new("duration", "How long has this been happening", "কতদিন ধরে হচ্ছে", "e.g., since January 2024", "যেমন: ২০২৪ সালের জানুয়ারি থেকে", false),
        },
    };

    /// <summary>Returns the required intake fields for a detected category.</summary>
    public static IReadOnlyList<IntakeField> GetRequiredFields(string categoryKey)
        => Registry.TryGetValue(categoryKey, out var fields) ? fields : Array.Empty<IntakeField>();

    /// <summary>Returns all category keys that have field definitions.</summary>
    public static IReadOnlyCollection<string> AllCategories => Registry.Keys;

    /// <summary>
    /// Validates a case file JSON against the required fields for a category.
    /// Returns the list of CRITICAL field keys that are still missing or empty.
    /// </summary>
    public static IReadOnlyList<string> ValidateCaseFile(string categoryKey, string? caseFileJson)
    {
        var fields = GetRequiredFields(categoryKey);
        if (fields.Count == 0) return Array.Empty<string>();

        var missing = new List<string>();
        foreach (var field in fields)
        {
            if (!field.IsCritical) continue;
            var value = ReadJsonField(caseFileJson, field.Key);
            if (string.IsNullOrWhiteSpace(value))
                missing.Add(field.Key);
        }
        return missing;
    }

    /// <summary>
    /// Builds a prompt injection block for the next intake turn, telling the AI
    /// exactly which fields to ask about.
    /// </summary>
    public static string BuildMissingFieldsPromptBlock(string categoryKey, IReadOnlyList<string> missingFieldKeys, string language)
    {
        var fields = GetRequiredFields(categoryKey);
        if (fields.Count == 0 || missingFieldKeys.Count == 0) return string.Empty;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine();
        sb.AppendLine($"CATEGORY DETECTED: {categoryKey}");
        sb.AppendLine("THE FOLLOWING CRITICAL DETAILS ARE STILL MISSING FROM THE CASE FILE.");
        sb.AppendLine("YOUR NEXT RESPONSE MUST ASK ABOUT ALL OF THESE REMAINING MISSING DETAILS TOGETHER IN A CLEAR NUMBERED LIST SO THE CITIZEN CAN ANSWER IN ONE REPLY.");
        sb.AppendLine("Store the citizen's answers in the caseFile JSON under the field keys shown.");
        sb.AppendLine();

        int index = 1;
        foreach (var key in missingFieldKeys)
        {
            var field = fields.FirstOrDefault(f => f.Key == key);
            if (field == null) continue;

            var name = language == "en" ? field.NameEn : field.NameBn;
            var example = language == "en" ? field.ExampleEn : field.ExampleBn;
            sb.AppendLine($"  {index++}. Field key: \"{field.Key}\" — {name} ({example})");
        }
        sb.AppendLine();
        sb.AppendLine("DO NOT set readyToExplain=true until ALL the above fields have values in the caseFile.");
        return sb.ToString();
    }

    // Reads a top-level field from case file JSON.
    private static string? ReadJsonField(string? json, string key)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                if (string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    return p.Value.ValueKind == System.Text.Json.JsonValueKind.String
                        ? p.Value.GetString()
                        : p.Value.ValueKind == System.Text.Json.JsonValueKind.Null ? null : p.Value.GetRawText();
                }
            }
            return null;
        }
        catch { return null; }
    }
}
