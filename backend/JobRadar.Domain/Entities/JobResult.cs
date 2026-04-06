namespace JobRadar.Domain.Entities;

/// <summary>
/// Entidade de domínio que representa um resultado de vaga coletado.
/// Sem dependências de infraestrutura (EF Core, Http, etc.).
/// </summary>
public class JobResult
{
    public int Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Snippet { get; private set; } = string.Empty;
    public string? Author { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public DateTime PublishedAt { get; private set; }
    public string Keywords { get; private set; } = string.Empty;
    public int RelevanceScore { get; private set; }
    public string MatchedKeywords { get; private set; } = string.Empty;
    public string ResultType { get; private set; } = "job";
    public DateTime CollectedAt { get; private set; } = DateTime.UtcNow;

    // Construtor para EF Core
    private JobResult() { }

    public static JobResult Create(
        string title,
        string snippet,
        string url,
        DateTime publishedAt,
        string? author = null,
        string resultType = "job")
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Título é obrigatório.", nameof(title));
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL é obrigatória.", nameof(url));

        return new JobResult
        {
            Title = title.Trim(),
            Snippet = snippet.Trim(),
            Url = url.Trim(),
            PublishedAt = publishedAt,
            Author = author?.Trim(),
            ResultType = resultType
        };
    }

    public void ApplyRelevance(int score, IEnumerable<string> matchedKeywords, string keywords)
    {
        RelevanceScore = Math.Clamp(score, 0, 100);
        MatchedKeywords = string.Join(",", matchedKeywords);
        Keywords = keywords;
    }
}
