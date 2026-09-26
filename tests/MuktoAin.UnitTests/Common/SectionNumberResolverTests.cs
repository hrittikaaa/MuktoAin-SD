using MuktoAin.Domain.Common;
using Xunit;

namespace MuktoAin.UnitTests.Common;

public class SectionNumberResolverTests
{
    [Fact]
    public void Resolve_ReturnsStoredNumber_WhenPresent()
    {
        var result = SectionNumberResolver.Resolve("42", "12. Sample text");
        Assert.Equal("42", result);
    }

    [Theory]
    [InlineData("12. Simple numbered section", "12")]
    [InlineData("১২। বাংলা সংখ্যাক্রম ধারা", "১২")]
    [InlineData("Section 34. English prefix with dot", "34")]
    [InlineData("Sec. 5A. Alphanumeric section", "5A")]
    [InlineData("ধারা ১২৩: কোলনযুক্ত ধারা নম্বর", "১২৩")]
    [InlineData("ধারা ১২-ক। হাইফেনযুক্ত বাংলা ধারা", "১২-ক")]
    [InlineData("ধারা ১২ক। যুক্তবর্ণ ধারা", "১২ক")]
    [InlineData("(১) বন্ধনীযুক্ত উপধারা বা ধারা", "১")]
    [InlineData("[42] ব্র্যাকেটযুক্ত ধারা", "42")]
    [InlineData("420. Penal Code cheating", "420")]
    public void Resolve_ExtractsLeadingNumber_FromSectionText(string sectionText, string expected)
    {
        var result = SectionNumberResolver.Resolve(null, sectionText);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Resolve_ReturnsEmpty_WhenNoLeadingNumber()
    {
        var result = SectionNumberResolver.Resolve(null, "This is plain prose without numbers.");
        Assert.Equal(string.Empty, result);
    }
}
