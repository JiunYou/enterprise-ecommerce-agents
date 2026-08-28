namespace EnterpriseCommerce.Application.Common.Models;

public sealed record PagedList<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public static PagedList<T> Create(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        return new PagedList<T>(items, page, pageSize, totalCount);
    }
}
