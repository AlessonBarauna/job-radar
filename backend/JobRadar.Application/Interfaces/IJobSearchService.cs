using JobRadar.Application.DTOs;

namespace JobRadar.Application.Interfaces;

/// <summary>
/// Contrato do serviço principal de busca de vagas.
/// </summary>
public interface IJobSearchService
{
    Task<SearchResponseDto> SearchAsync(string rawKeywords, CancellationToken ct = default);
}
