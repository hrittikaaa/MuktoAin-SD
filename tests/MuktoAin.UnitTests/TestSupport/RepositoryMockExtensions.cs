using System.Linq.Expressions;
using Moq;
using MuktoAin.Domain.Interfaces.Repositories;

namespace MuktoAin.UnitTests.TestSupport;

public static class RepositoryMockExtensions
{
    // Backs both GetAllAsync and FindAsync with the same in-memory rows, so a
    // test states its data once and FindAsync applies the real predicate.
    public static void SetupRows<TRepo, T>(this Mock<TRepo> repo, IEnumerable<T> rows)
        where TRepo : class, IRepository<T>
        where T : class
    {
        var list = rows.ToList();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(list);
        repo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<T, bool>>>()))
            .ReturnsAsync((Expression<Func<T, bool>> predicate) => list.Where(predicate.Compile()).ToList());
    }
}
