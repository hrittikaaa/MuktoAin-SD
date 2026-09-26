using Microsoft.AspNetCore.Mvc;
using Moq;
using MuktoAin.Application.Services;
using MuktoAin.Domain.Entities;
using MuktoAin.Domain.Interfaces.Repositories;
using MuktoAin.Web.Controllers;
using MuktoAin.Web.ViewModels;
using Xunit;

namespace MuktoAin.UnitTests.Controllers;

// FR-6 category browsing (/Category). CategoryService is a thin passthrough,
// so it is built for real over a mocked repository; these tests pin the
// presentation mapping the controller owns (icons, accents, pipe-split lists).
public class CategoryControllerTests
{
    private readonly Mock<IRepository<CaseCategory>> _categoryRepo = new();
    private readonly CategoryController _controller;

    public CategoryControllerTests()
    {
        _controller = new CategoryController(new CategoryService(_categoryRepo.Object));
    }

    private static CaseCategory Category(int id, string name = "Name", string actions = "", string actionsEn = "") => new()
    {
        CategoryId = id,
        Name = name,
        NameBn = name + " (bn)",
        Description = name + " description",
        DescriptionBn = name + " বিবরণ",
        CommonActions = actions,
        CommonActionsEn = actionsEn
    };

    [Fact]
    public async Task Index_OrdersCategoriesById()
    {
        _categoryRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<CaseCategory>
        {
            Category(3, "RTI"), Category(1, "Labour"), Category(4, "Consumer"), Category(2, "GD")
        });

        var result = await _controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<List<CategoryViewModel>>(view.Model);
        Assert.Equal(new[] { 1, 2, 3, 4 }, model.Select(m => m.CategoryId));
    }

    [Fact]
    public async Task Index_NoCategories_ReturnsEmptyList()
    {
        _categoryRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<CaseCategory>());

        var result = await _controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Empty(Assert.IsType<List<CategoryViewModel>>(view.Model));
    }

    [Fact]
    public async Task Index_MapsBilingualNamesAndDescriptions()
    {
        _categoryRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<CaseCategory>
        {
            new()
            {
                CategoryId = 1,
                Name = "Labour Complaint",
                NameBn = "শ্রম অভিযোগ",
                Description = "Workplace disputes",
                DescriptionBn = "কর্মক্ষেত্রের বিরোধ"
            }
        });

        var result = await _controller.Index();

        var item = Assert.Single(Assert.IsType<List<CategoryViewModel>>(Assert.IsType<ViewResult>(result).Model));
        Assert.Equal("Labour Complaint", item.NameEn);
        Assert.Equal("শ্রম অভিযোগ", item.NameBn);
        Assert.Equal("Workplace disputes", item.DescriptionEn);
        Assert.Equal("কর্মক্ষেত্রের বিরোধ", item.DescriptionBn);
    }

    [Theory]
    [InlineData(1, "briefcase", "primary")]
    [InlineData(2, "shield", "info")]
    [InlineData(3, "file-text", "success")]
    [InlineData(4, "shopping-bag", "gold")]
    public async Task Details_SeededCategory_UsesItsIconAndAccent(int id, string icon, string accent)
    {
        _categoryRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(Category(id));

        var result = await _controller.Details(id);

        var model = Assert.IsType<CategoryViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(icon, model.Icon);
        Assert.Equal(accent, model.Accent);
    }

    [Fact]
    public async Task Details_CategoryOutsideSeededFour_FallsBackToFolderAndGold()
    {
        _categoryRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync(Category(99));

        var result = await _controller.Details(99);

        var model = Assert.IsType<CategoryViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("folder", model.Icon);
        Assert.Equal("gold", model.Accent);
    }

    [Fact]
    public async Task Details_UnknownId_ReturnsNotFound()
    {
        _categoryRepo.Setup(r => r.GetByIdAsync(It.IsAny<object>())).ReturnsAsync((CaseCategory?)null);

        var result = await _controller.Details(123);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_SplitsPipeDelimitedCommonActionsInOrder()
    {
        _categoryRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(
            Category(1, actions: "বকেয়া বেতন|অন্যায় ছাঁটাই|ওভারটাইম", actionsEn: "Unpaid wages|Unfair dismissal|Overtime"));

        var result = await _controller.Details(1);

        var model = Assert.IsType<CategoryViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(new[] { "বকেয়া বেতন", "অন্যায় ছাঁটাই", "ওভারটাইম" }, model.CommonActions);
        Assert.Equal(new[] { "Unpaid wages", "Unfair dismissal", "Overtime" }, model.CommonActionsEn);
    }

    [Fact]
    public async Task Details_DropsEmptyEntriesFromCommonActions()
    {
        _categoryRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(
            Category(2, actions: "|Theft||Lost phone|", actionsEn: "||"));

        var result = await _controller.Details(2);

        var model = Assert.IsType<CategoryViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(new[] { "Theft", "Lost phone" }, model.CommonActions);
        Assert.Empty(model.CommonActionsEn);
    }

    [Fact]
    public async Task Details_EmptyCommonActions_GivesEmptyLists()
    {
        _categoryRepo.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(Category(3));

        var result = await _controller.Details(3);

        var model = Assert.IsType<CategoryViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Empty(model.CommonActions);
        Assert.Empty(model.CommonActionsEn);
    }

    [Fact]
    public void Actions_AreHttpGet()
    {
        foreach (var name in new[] { nameof(CategoryController.Index), nameof(CategoryController.Details) })
        {
            var method = typeof(CategoryController).GetMethod(name)!;
            Assert.NotNull(method.GetCustomAttributes(typeof(HttpGetAttribute), inherit: true).SingleOrDefault());
        }
    }
}
