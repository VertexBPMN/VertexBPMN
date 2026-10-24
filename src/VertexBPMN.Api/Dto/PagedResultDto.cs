namespace VertexBPMN.Api.Dto;

/// <summary>
/// Fixed-pagination envelope returned by the personal-inbox query.
/// </summary>
/// <typeparam name="T">The type of each item in the page.</typeparam>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type exposed on HTTP action signatures.")]
public sealed class PagedResultDto<T>
{
	public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
	public int Total { get; set; }
	public int Page { get; set; }
	public int PageSize { get; set; }
	public bool HasMore { get; set; }
}
