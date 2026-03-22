using System.Text.Json.Serialization;

namespace DeployFlow.Application.DTOs;

public record PaginatedResponse<T>(
    [property: JsonPropertyName("data")] List<T> Items,
    [property: JsonPropertyName("total")] int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
)
{
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;

    public static PaginatedResponse<T> Create(IEnumerable<T> items, int totalCount, int page, int pageSize)
    {
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PaginatedResponse<T>(items.ToList(), totalCount, page, pageSize, totalPages);
    }
}
