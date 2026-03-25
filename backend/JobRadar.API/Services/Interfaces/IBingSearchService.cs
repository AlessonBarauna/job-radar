using JobRadar.API.Models;

namespace JobRadar.API.Services.Interfaces;

/// <summary>
/// Serviço de busca via Bing Web Search API v7.
/// Requer chave em appsettings: Search:BingApiKey.
/// </summary>
public interface IBingSearchService
{
    /// <summary>
    /// Indica se o serviço está configurado (API key presente).
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Executa busca no Bing filtrando site:linkedin.com nas últimas 24h.
    /// </summary>
    Task<List<JobResult>> SearchAsync(List<string> keywords, CancellationToken ct = default);
}
