using JobRadar.API.Models;

namespace JobRadar.API.Services.Interfaces;

/// <summary>
/// Serviço de busca via Remotive.com API.
/// Gratuito, sem necessidade de API key.
/// Documentação: https://remotive.com/api/remote-jobs
/// </summary>
public interface IRemotiveSearchService
{
    bool IsConfigured { get; }
    Task<List<JobResult>> SearchAsync(List<string> keywords, CancellationToken ct = default);
}
