using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;

namespace MuktoAin.Web.Controllers;

/// <summary>
/// Renders a generated legal document (plain text, line-structured) for
/// display. Unlike <see cref="MarkdownText"/> it keeps every line break, and a
/// line made only of rule characters (the templates' ────/════ dividers) becomes
/// an &lt;hr&gt; that always fits the width: as text it is one unbreakable
/// word that either overflows the paper sheet or wraps into a ragged second line.
/// Everything else is HTML-encoded.
/// </summary>
public static class DocumentText
{
    private const int MinRuleLength = 8;

    public static IHtmlContent ToHtml(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return HtmlString.Empty;

        var sb = new StringBuilder("<div class=\"doc-text\">");
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var rule = RuleKind(lines[i]);
            if (rule != null)
            {
                sb.Append("<hr class=\"doc-rule").Append(rule).Append("\">");
                continue; // the <hr> is a block, so no line break after it
            }
            sb.Append(HtmlEncoder.Default.Encode(lines[i]));
            if (i < lines.Length - 1) sb.Append('\n');
        }
        return new HtmlString(sb.Append("</div>").ToString());
    }

    // "" for a single rule, " double" for ════ / ====, null for ordinary text.
    private static string? RuleKind(string line)
    {
        var t = line.Trim();
        if (t.Length < MinRuleLength) return null;
        if (t.All(c => c is '═' or '=')) return " double";
        return t.All(c => c is '─' or '━' or '-' or '_' or '—') ? "" : null;
    }
}
