namespace JobRadar.API.DTOs;

/// <summary>
/// DTO de resposta da busca.
/// </summary>
public class SearchResponseDto
{
    /// <summary>Resultados encontrados, ordenados por relevância.</summary>
    public List<JobResultDto> Results { get; set; } = [];

    /// <summary>Total de resultados.</summary>
    public int Total { get; set; }

    /// <summary>Palavras-chave buscadas.</summary>
    public List<string> Keywords { get; set; } = [];

    /// <summary>Provedor de busca utilizado.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Tempo de execução em ms.</summary>
    public long ElapsedMs { get; set; }

    /// <summary>Data/hora da busca.</summary>
    public DateTime SearchedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Se os dados vieram do cache.</summary>
    public bool FromCache { get; set; }
}
