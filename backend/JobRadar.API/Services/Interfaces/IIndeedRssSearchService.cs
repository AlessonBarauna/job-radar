using JobRadar.API.Models;

namespace JobRadar.API.Services.Interfaces;

/// <summary>
/// Serviço de busca via Indeed Brasil RSS Feed.
/// Gratuito, sem necessidade de API key.
/// Retorna vagas do mercado brasileiro.
/// </summary>
public interface IIndeedRssSearchService
{
    bool IsConfigured { get; }
    Task<List<JobResult>> SearchAsync(List<string> keywords, CancellationToken ct = default);
}
