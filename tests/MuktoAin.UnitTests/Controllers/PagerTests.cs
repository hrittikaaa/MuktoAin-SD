using MuktoAin.Web.Controllers;

namespace MuktoAin.UnitTests.Controllers;

public class PagerTests
{
    [Fact]
    public void FewPages_ShowsEveryPage()
    {
        Assert.Equal(new int?[] { 1, 2, 3, 4, 5 }, Pager.Window(page: 3, totalPages: 5));
    }

    [Fact]
    public void ManyPages_ShowsFirstLastAndNeighboursWithGaps()
    {
        // null marks an ellipsis gap
        Assert.Equal(new int?[] { 1, null, 8, 9, 10, 11, 12, null, 40 }, Pager.Window(page: 10, totalPages: 40));
    }

    [Fact]
    public void NearTheStart_HasNoLeadingGap()
    {
        Assert.Equal(new int?[] { 1, 2, 3, 4, null, 40 }, Pager.Window(page: 2, totalPages: 40));
    }

    [Fact]
    public void NearTheEnd_HasNoTrailingGap()
    {
        Assert.Equal(new int?[] { 1, null, 37, 38, 39, 40 }, Pager.Window(page: 39, totalPages: 40));
    }

    [Fact]
    public void GapOfOnePage_ShowsThePageInsteadOfEllipsis()
    {
        Assert.Equal(new int?[] { 1, 2, 3, 4, 5, 6, null, 20 }, Pager.Window(page: 4, totalPages: 20));
    }
}
