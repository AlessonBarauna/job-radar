using JobRadar.API.Models;

namespace JobRadar.API.Services.Interfaces;

/// <summary>
/// Serviço de busca via Google Custom Search API.
/// Requer chaves em appsettings: Search:GoogleApiKey e Search:GoogleCseId.
/// Free tier: 100 queries/dia.
/// </summary>
public interface IGoogleCustomSearchService
{
    /// <summary>
    /// Indica se o serviço está configurado.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Executa busca no Google CSE filtrando site:linkedin.com nas últimas 24h.
    /// </summary>
    Task<List<JobResult>> SearchAsync(List<string> keywords, CancellationToken ct = default);
}
