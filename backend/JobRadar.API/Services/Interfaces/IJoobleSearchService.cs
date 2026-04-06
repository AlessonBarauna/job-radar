using JobRadar.API.Models;

namespace JobRadar.API.Services.Interfaces;

/// <summary>
/// Serviço de busca via Jooble API.
/// Foco no mercado brasileiro. API key gratuita em: https://jooble.org/api/about
/// Configure em appsettings: Search:JoobleApiKey
/// </summary>
public interface IJoobleSearchService
{
    bool IsConfigured { get; }
    Task<List<JobResult>> SearchAsync(List<string> keywords, CancellationToken ct = default);
}
