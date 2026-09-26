namespace MuktoAin.Web.Controllers;

/// <summary>
/// Display-only Bengali numerals for the data-bn half of a bilingual value
/// (the English half keeps Latin digits). Presentation mapping only.
/// </summary>
public static class Numerals
{
    public static string Bn(string text) =>
        string.Create(text.Length, text, (span, s) =>
        {
            for (var i = 0; i < s.Length; i++)
                span[i] = char.IsAsciiDigit(s[i]) ? (char)(s[i] - '0' + '০') : s[i];
        });
}
