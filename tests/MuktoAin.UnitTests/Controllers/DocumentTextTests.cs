using MuktoAin.Web.Controllers;
using Xunit;

namespace MuktoAin.UnitTests.Controllers;

public class DocumentTextTests
{
    private static string ToHtmlString(string? text)
    {
        var html = DocumentText.ToHtml(text);
        using var writer = new System.IO.StringWriter();
        html.WriteTo(writer, System.Text.Encodings.Web.HtmlEncoder.Default);
        return writer.ToString();
    }

    [Fact]
    public void ToHtml_DividerLines_BecomeRules_NotText()
    {
        var html = ToHtmlString("RELIEF SOUGHT:\n" + new string('─', 40) + "\nBased on the facts\n" + new string('═', 44));

        Assert.Contains("<hr class=\"doc-rule\">", html);
        Assert.Contains("<hr class=\"doc-rule double\">", html);
        Assert.DoesNotContain("───", html);
        Assert.DoesNotContain("═══", html);
    }

    [Fact]
    public void ToHtml_KeepsLineBreaks_AndDoesNotMergeTheLineAfterADivider()
    {
        var html = ToHtmlString("DECLARATION:\n" + new string('─', 40) + "\nI hereby declare");

        Assert.Contains("DECLARATION:\n<hr class=\"doc-rule\">I hereby declare", html);
    }

    [Fact]
    public void ToHtml_FillInBlanksWithText_StayAsText()
    {
        var html = ToHtmlString("Complainant: ________________________");

        Assert.Contains("Complainant: ________________________", html);
        Assert.DoesNotContain("<hr", html);
    }

    [Fact]
    public void ToHtml_EncodesHtml()
    {
        var html = ToHtmlString("<script>alert(1)</script>");

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void ToHtml_Blank_RendersNothing(string? text) => Assert.Equal("", ToHtmlString(text));
}
