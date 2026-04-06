namespace JobRadar.API.DTOs;

/// <summary>
/// DTO de resposta com dados de uma vaga/post.
/// </summary>
public class JobResultDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string Url { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public int RelevanceScore { get; set; }

    /// <summary>Lista de palavras-chave encontradas no conteúdo.</summary>
    public List<string> MatchedKeywords { get; set; } = [];

    /// <summary>Tipo: "job" ou "post".</summary>
    public string ResultType { get; set; } = "job";

    /// <summary>Tempo relativo de publicação (ex: "há 3 horas").</summary>
    public string RelativeTime { get; set; } = string.Empty;

    /// <summary>Provedor de origem inferido pela URL (Remotive, Indeed, LinkedIn, etc.).</summary>
    public string Source { get; set; } = string.Empty;
}
