namespace JobRadar.Domain.Entities;

/// <summary>
/// Entidade que representa o histórico de uma busca realizada.
/// </summary>
public class SearchHistory
{
    public int Id { get; private set; }
    public string Keywords { get; private set; } = string.Empty;
    public int ResultCount { get; private set; }
    public long ElapsedMs { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public DateTime SearchedAt { get; private set; }

    private SearchHistory() { }

    public static SearchHistory Create(string keywords, int resultCount, long elapsedMs, string provider)
    {
        return new SearchHistory
        {
            Keywords = keywords,
            ResultCount = resultCount,
            ElapsedMs = elapsedMs,
            Provider = provider,
            SearchedAt = DateTime.UtcNow
        };
    }
}
