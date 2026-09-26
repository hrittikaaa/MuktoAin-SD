namespace MuktoAin.Web.Controllers;

/// <summary>
/// Which page links a pager shows: the first and last page, the current page
/// and <paramref name="radius"/> neighbours each side. A null entry is an
/// ellipsis gap; a gap of exactly one page shows that page instead, since an
/// ellipsis would take the same space. Presentation mapping only.
/// </summary>
public static class Pager
{
    public static IReadOnlyList<int?> Window(int page, int totalPages, int radius = 2)
    {
        var shown = new SortedSet<int> { 1, totalPages };
        for (var p = page - radius; p <= page + radius; p++)
            if (p >= 1 && p <= totalPages) shown.Add(p);

        var result = new List<int?>();
        var previous = 0;
        foreach (var p in shown)
        {
            if (p - previous == 2) result.Add(p - 1);
            else if (p - previous > 2) result.Add(null);
            result.Add(p);
            previous = p;
        }
        return result;
    }
}
