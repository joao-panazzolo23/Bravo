namespace Bravo.Shared.Pagination;

public record PaginatedList<T>
{
    public int Count { get; private init; }
    public int Pages { get; private init; }
    public bool Next { get; private init; }
    public int Items { get; private init; }
    public IEnumerable<T>? Results { get; init; }
    public PaginatedList(IEnumerable<T> results, int count, int page, int length)
    {
        Items = count;
        Pages = (int)Math.Ceiling((double)count / (double)length);
        Results = results;
        Count = results.Count();
        Next = (page * length) < count;
    }
}
