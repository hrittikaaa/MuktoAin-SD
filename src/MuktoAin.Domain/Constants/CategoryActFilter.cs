namespace MuktoAin.Domain.Constants;

/// <summary>
/// Maps each case category to the Act titles most relevant to it.
/// Used by RagContextBuilder to filter retrieval results so only
/// category-relevant statutes appear in the prompt context.
/// </summary>
public static class CategoryActFilter
{
    private static readonly Dictionary<string, IReadOnlyList<string>> Registry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LabourComplaint"] = new[]
        {
            "Bangladesh Labour Act, 2006",
            "Bangladesh Labour Rules, 2015",
            "Payment of Wages Act, 1936",
        },
        ["GeneralDiary"] = new[]
        {
            "Code of Criminal Procedure, 1898",
            "Penal Code, 1860",
            "Police Act, 1861",
            "Nari O Shishu Nirjatan Daman Ain, 2000",
        },
        ["RtiRequest"] = new[]
        {
            "Right to Information Act, 2009",
        },
        ["ConsumerComplaint"] = new[]
        {
            "Consumer Rights Protection Act, 2009",
            "The Sale of Goods Act, 1930",
            "The Contract Act, 1872",
            "Bangladesh Standards and Testing Institution Act, 2018",
        },
        ["LandPropertyDispute"] = new[]
        {
            "Transfer of Property Act, 1882",
            "State Acquisition and Tenancy Act, 1950",
            "Land Crime Prevention and Redress Act, 2023",
            "Specific Relief Act, 1877",
            "Registration Act, 1908",
            "Non-Agricultural Tenancy Act, 1947",
        },
        ["FamilyDispute"] = new[]
        {
            "Muslim Family Laws Ordinance, 1961",
            "Family Courts Ordinance, 1985",
            "Dowry Prohibition Act, 2018",
            "Domestic Violence (Prevention and Protection) Act, 2010",
            "Guardians and Wards Act, 1890",
            "Muslim Marriages and Divorces (Registration) Act, 1974",
            "The Child Marriage Restraint Act, 2017",
        },
        ["CyberCrime"] = new[]
        {
            "Digital Security Act, 2018",
            "Cyber Security Act, 2023",
            "Information and Communication Technology Act, 2006",
            "Penal Code, 1860",
            "Pornography Control Act, 2012",
        },
        ["EnvironmentalComplaint"] = new[]
        {
            "Bangladesh Environment Conservation Act, 1995",
            "Environment Conservation Rules, 1997",
            "Environment Court Act, 2010",
            "Bangladesh Water Act, 2013",
            "Brick Kiln Control Act, 2013",
        },
    };

    /// <summary>
    /// Returns Act titles relevant to the given category for retrieval filtering.
    /// If no category is matched, returns an empty list (no filtering applied).
    /// </summary>
    public static IReadOnlyList<string> GetRelevantActTitles(string? categoryKey)
        => categoryKey != null && Registry.TryGetValue(categoryKey, out var titles)
            ? titles
            : Array.Empty<string>();
}
