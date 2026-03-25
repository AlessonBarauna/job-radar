namespace JobRadar.API.Models;

/// <summary>
/// Histórico de buscas realizadas pelo usuário.
/// </summary>
public class SearchHistory
{
    public int Id { get; set; }

    /// <summary>Palavras-chave usadas na busca.</summary>
    public string Keywords { get; set; } = string.Empty;

    /// <summary>Quantidade de resultados retornados.</summary>
    public int ResultCount { get; set; }

    /// <summary>Data/hora da busca.</summary>
    public DateTime SearchedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Tempo de resposta em milissegundos.</summary>
    public long ElapsedMs { get; set; }

    /// <summary>Provedor utilizado (Bing, Google, Mock).</summary>
    public string Provider { get; set; } = string.Empty;
}
