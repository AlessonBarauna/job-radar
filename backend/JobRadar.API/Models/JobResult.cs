namespace JobRadar.API.Models;

/// <summary>
/// Representa um resultado de vaga ou post encontrado.
/// </summary>
public class JobResult
{
    public int Id { get; set; }

    /// <summary>Título da vaga ou post.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Resumo/snippet do resultado.</summary>
    public string Snippet { get; set; } = string.Empty;

    /// <summary>Autor ou empresa (extraído do título/snippet quando disponível).</summary>
    public string? Author { get; set; }

    /// <summary>URL original no LinkedIn.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Data de publicação (estimada via data do resultado de busca).</summary>
    public DateTime PublishedAt { get; set; }

    /// <summary>Palavras-chave que gerou este resultado.</summary>
    public string Keywords { get; set; } = string.Empty;

    /// <summary>Score de relevância calculado (0-100).</summary>
    public int RelevanceScore { get; set; }

    /// <summary>Palavras-chave encontradas no título/snippet (separadas por vírgula).</summary>
    public string MatchedKeywords { get; set; } = string.Empty;

    /// <summary>Tipo: "job" (vaga) ou "post" (post).</summary>
    public string ResultType { get; set; } = "job";

    /// <summary>Data em que foi coletado.</summary>
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
}
