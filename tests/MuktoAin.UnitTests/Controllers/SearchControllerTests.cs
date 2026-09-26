using Microsoft.AspNetCore.Mvc;
using Moq;
using MuktoAin.Application.Services;
using MuktoAin.Domain.Entities;
using MuktoAin.Domain.Enums;
using MuktoAin.Domain.Interfaces.Repositories;
using MuktoAin.Domain.Interfaces.Services;
using MuktoAin.Domain.Models;
using MuktoAin.Web.Controllers;
using MuktoAin.Web.ViewModels;
using Xunit;

namespace MuktoAin.UnitTests.Controllers;

// FR-7 Acts search page (/Search). SearchService is concrete, so it is built
// for real over a mocked keyword search and Act repository. Covers the three
// entry modes (empty prompt, keyword search, Act browse), snippet truncation
// and the sub-clause splitting used by the full-section modal.
public class SearchControllerTests
{
    private readonly Mock<IKeywordSectionSearch> _keywordSearch = new();
    private readonly Mock<IActRepository> _actRepo = new();
    private readonly SearchController _controller;

    public SearchControllerTests()
    {
        _actRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Act>());
        _controller = new SearchController(new SearchService(_keywordSearch.Object, _actRepo.Object), _actRepo.Object);
    }

    private static RetrievedSection Hit(int sectionId, string text = "Some section text.", string actTitle = "Labour Act") =>
        new(sectionId, actTitle, sectionId.ToString(), text, 0.9f, RetrievalMethod.Keyword, "42", 2006);

    private void KeywordReturns(params RetrievedSection[] hits) =>
        _keywordSearch.Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(hits);

    private static SearchViewModel ModelOf(IActionResult result) =>
        Assert.IsType<SearchViewModel>(Assert.IsType<ViewResult>(result).Model);

    // ---- empty state -------------------------------------------------------

    [Fact]
    public async Task Index_NoQueryAndNoAct_ShowsPromptWithoutSearching()
    {
        var result = await _controller.Index(q: null);

        var model = ModelOf(result);
        Assert.False(model.HasSearched);
        Assert.Empty(model.Results);
        _keywordSearch.Verify(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Index_BlankQueryAndNoAct_IsTreatedAsEmpty(string q)
    {
        var result = await _controller.Index(q);

        Assert.False(ModelOf(result).HasSearched);
        _keywordSearch.Verify(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Index_AlwaysLoadsActDropdownSortedByTitle()
    {
        _actRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Act>
        {
            new() { ActId = 1, Title = "Penal Code" },
            new() { ActId = 2, Title = "Consumer Rights Protection Act" },
            new() { ActId = 3, Title = "Labour Act" }
        });

        await _controller.Index(q: null);

        var acts = Assert.IsType<List<Act>>((object)_controller.ViewBag.Acts);
        Assert.Equal(new[] { "Consumer Rights Protection Act", "Labour Act", "Penal Code" }, acts.Select(a => a.Title));
    }

    // ---- keyword search ----------------------------------------------------

    [Fact]
    public async Task Index_WithQuery_RunsKeywordSearchAndMapsResults()
    {
        KeywordReturns(Hit(7, "Every worker shall be paid wages.", "Bangladesh Labour Act"));

        var result = await _controller.Index("wages");

        var model = ModelOf(result);
        Assert.True(model.HasSearched);
        Assert.Equal("wages", model.Query);
        Assert.Equal(1, model.TotalResults);
        Assert.Equal(10, model.PageSize);
        var item = Assert.Single(model.Results);
        Assert.Equal(7, item.SectionId);
        Assert.Equal("Bangladesh Labour Act", item.ActTitle);
        Assert.Equal("7", item.SectionNumber);
        Assert.Equal("42", item.ActNumber);
        Assert.Equal(2006, item.ActYear);
        Assert.Equal(string.Empty, item.SectionTitle);
        _keywordSearch.Verify(s => s.SearchAsync("wages", 100), Times.Once);
    }

    [Fact]
    public async Task Index_WithQuery_NoHits_ReturnsEmptySearchedState()
    {
        KeywordReturns();

        var model = ModelOf(await _controller.Index("nothing-matches"));

        Assert.True(model.HasSearched);
        Assert.Equal(0, model.TotalResults);
        Assert.Empty(model.Results);
    }

    [Fact]
    public async Task Index_SecondPage_ReturnsNextTenResults()
    {
        KeywordReturns(Enumerable.Range(1, 25).Select(i => Hit(i)).ToArray());

        var model = ModelOf(await _controller.Index("law", page: 2));

        Assert.Equal(2, model.Page);
        Assert.Equal(25, model.TotalResults);
        Assert.Equal(Enumerable.Range(11, 10), model.Results.Select(r => r.SectionId));
    }

    [Fact]
    public async Task Index_LastPartialPage_ReturnsRemainder()
    {
        KeywordReturns(Enumerable.Range(1, 25).Select(i => Hit(i)).ToArray());

        var model = ModelOf(await _controller.Index("law", page: 3));

        Assert.Equal(new[] { 21, 22, 23, 24, 25 }, model.Results.Select(r => r.SectionId));
    }

    [Fact]
    public async Task Index_QueryWithActFilter_KeepsOnlyThatActsSections()
    {
        KeywordReturns(Hit(1), Hit(2), Hit(3), Hit(4));
        _actRepo.Setup(r => r.GetWithSectionsAsync(9)).ReturnsAsync(new Act
        {
            ActId = 9,
            Sections = new List<ActSection> { new() { SectionId = 2 }, new() { SectionId = 4 } }
        });

        var model = ModelOf(await _controller.Index("wages", actId: 9));

        Assert.Equal(9, model.ActId);
        Assert.Equal(2, model.TotalResults);
        Assert.Equal(new[] { 2, 4 }, model.Results.Select(r => r.SectionId));
    }

    [Fact]
    public async Task Index_QueryWithUnknownActFilter_ReturnsNoResults()
    {
        KeywordReturns(Hit(1), Hit(2));
        _actRepo.Setup(r => r.GetWithSectionsAsync(It.IsAny<int>())).ReturnsAsync((Act?)null);

        var model = ModelOf(await _controller.Index("wages", actId: 404));

        Assert.True(model.HasSearched);
        Assert.Empty(model.Results);
    }

    // ---- Act browse (no keyword) --------------------------------------------

    [Fact]
    public async Task Index_ActOnly_BrowsesSectionsInStatutoryOrder()
    {
        _actRepo.Setup(r => r.GetWithSectionsAsync(5)).ReturnsAsync(new Act
        {
            ActId = 5,
            Title = "Right to Information Act",
            ActNumber = "20",
            Year = 2009,
            Sections = new List<ActSection>
            {
                new() { SectionId = 30, OrdinalPosition = 3, SectionText = "3. Third section." },
                new() { SectionId = 10, OrdinalPosition = 1, SectionText = "1. First section." },
                new() { SectionId = 20, OrdinalPosition = 2, SectionText = "2. Second section." }
            }
        });

        var model = ModelOf(await _controller.Index(q: null, actId: 5));

        Assert.True(model.HasSearched);
        Assert.Equal(5, model.ActId);
        Assert.Equal(string.Empty, model.Query);
        Assert.Equal(new[] { 10, 20, 30 }, model.Results.Select(r => r.SectionId));
        Assert.All(model.Results, r => Assert.Equal("Right to Information Act", r.ActTitle));
        Assert.Equal(2009, model.Results[0].ActYear);
        _keywordSearch.Verify(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Index_ActOnly_ResolvesSectionNumberFromLeadingText()
    {
        _actRepo.Setup(r => r.GetWithSectionsAsync(5)).ReturnsAsync(new Act
        {
            ActId = 5,
            Title = "Act",
            Sections = new List<ActSection>
            {
                new() { SectionId = 1, OrdinalPosition = 1, SectionNumber = null, SectionText = "12. Short title." },
                new() { SectionId = 2, OrdinalPosition = 2, SectionNumber = "13A", SectionText = "Stored number wins." },
                new() { SectionId = 3, OrdinalPosition = 3, SectionNumber = null, SectionText = "CHAPTER II" }
            }
        });

        var model = ModelOf(await _controller.Index(q: null, actId: 5));

        Assert.Equal(new[] { "12", "13A", "" }, model.Results.Select(r => r.SectionNumber));
    }

    // ---- snippet truncation -------------------------------------------------

    [Fact]
    public async Task Index_ShortSection_IsNotTruncated()
    {
        const string text = "A short section.";
        KeywordReturns(Hit(1, text));

        var item = Assert.Single(ModelOf(await _controller.Index("short")).Results);

        Assert.False(item.IsTruncated);
        Assert.Equal(text, item.SectionTextSnippet);
        Assert.Equal(text, item.SectionTextFull);
    }

    [Fact]
    public async Task Index_LongSection_SnippetIsCutWithEllipsisButFullTextKept()
    {
        var text = string.Join(" ", Enumerable.Repeat("wages", 100));
        KeywordReturns(Hit(1, text));

        var item = Assert.Single(ModelOf(await _controller.Index("wages")).Results);

        Assert.True(item.IsTruncated);
        Assert.EndsWith("…", item.SectionTextSnippet);
        Assert.True(item.SectionTextSnippet.Length <= 221);
        Assert.Equal(text, item.SectionTextFull);
    }

    [Fact]
    public async Task Index_SectionOfExactlySnippetLength_IsNotTruncated()
    {
        var text = new string('x', 220);
        KeywordReturns(Hit(1, text));

        var item = Assert.Single(ModelOf(await _controller.Index("x")).Results);

        Assert.False(item.IsTruncated);
        Assert.Equal(text, item.SectionTextSnippet);
    }

    // ---- clause splitting ---------------------------------------------------

    [Fact]
    public async Task Index_EnumeratedLatinClauses_AreSplitIntoIntroAndList()
    {
        const string text = "9. The Commission shall have its own Fund which shall comprise-(a)\tgrants from the Government;(b) \tloans from the Government;(c) other receipts.";
        KeywordReturns(Hit(9, text));

        var item = Assert.Single(ModelOf(await _controller.Index("fund")).Results);

        Assert.Equal("9. The Commission shall have its own Fund which shall comprise-", item.SectionIntro);
        Assert.Equal(new[]
        {
            "(a) grants from the Government;",
            "(b) loans from the Government;",
            "(c) other receipts."
        }, item.SectionClauses);
    }

    [Fact]
    public async Task Index_NumberedClauses_AreSplit()
    {
        const string text = "Duties include: (1) keep records; (2) file returns; (3) pay dues.";
        KeywordReturns(Hit(1, text));

        var item = Assert.Single(ModelOf(await _controller.Index("duties")).Results);

        Assert.Equal("Duties include:", item.SectionIntro);
        Assert.Equal(3, item.SectionClauses.Count);
        Assert.StartsWith("(1)", item.SectionClauses[0]);
        Assert.StartsWith("(3)", item.SectionClauses[2]);
    }

    [Fact]
    public async Task Index_BengaliConsonantClauses_AreSplit()
    {
        const string text = "কমিশনের দায়িত্ব- (ক) তথ্য সংরক্ষণ; (খ) অভিযোগ গ্রহণ;";
        KeywordReturns(Hit(1, text));

        var item = Assert.Single(ModelOf(await _controller.Index("কমিশন")).Results);

        Assert.Equal("কমিশনের দায়িত্ব-", item.SectionIntro);
        Assert.Equal(new[] { "(ক) তথ্য সংরক্ষণ;", "(খ) অভিযোগ গ্রহণ;" }, item.SectionClauses);
    }

    [Fact]
    public async Task Index_BengaliDigitClauses_AreSplit()
    {
        const string text = "শর্তাবলী: (১) প্রথম শর্ত; (২) দ্বিতীয় শর্ত।";
        KeywordReturns(Hit(1, text));

        var item = Assert.Single(ModelOf(await _controller.Index("শর্ত")).Results);

        Assert.Equal(2, item.SectionClauses.Count);
        Assert.StartsWith("(১)", item.SectionClauses[0]);
    }

    [Fact]
    public async Task Index_SingleCrossReference_IsNotTreatedAsList()
    {
        const string text = "Subject to section 3(2) and (a) nothing else applies.";
        KeywordReturns(Hit(1, text));

        var item = Assert.Single(ModelOf(await _controller.Index("section")).Results);

        Assert.Equal(text, item.SectionIntro);
        Assert.Empty(item.SectionClauses);
    }

    [Fact]
    public async Task Index_ListNotStartingAtOpener_IsLeftAsProse()
    {
        const string text = "As amended by (2) the first order and (3) the second order.";
        KeywordReturns(Hit(1, text));

        var item = Assert.Single(ModelOf(await _controller.Index("order")).Results);

        Assert.Equal(text, item.SectionIntro);
        Assert.Empty(item.SectionClauses);
    }

    [Fact]
    public async Task Index_PlainProse_HasNoClauses()
    {
        const string text = "This Act may be called the Consumer Rights Protection Act.";
        KeywordReturns(Hit(1, text));

        var item = Assert.Single(ModelOf(await _controller.Index("consumer")).Results);

        Assert.Equal(text, item.SectionIntro);
        Assert.Empty(item.SectionClauses);
    }

    [Fact]
    public async Task Index_ClauseWhitespaceRuns_AreCollapsed()
    {
        const string text = "Includes-(a)\t\t first   item;\n(b)\n second\titem.";
        KeywordReturns(Hit(1, text));

        var item = Assert.Single(ModelOf(await _controller.Index("items")).Results);

        Assert.Equal(new[] { "(a) first item;", "(b) second item." }, item.SectionClauses);
    }
}
