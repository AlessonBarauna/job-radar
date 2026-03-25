using JobRadar.API.DTOs;

namespace JobRadar.API.Services.Interfaces;

/// <summary>
/// Serviço principal de busca de vagas.
/// </summary>
public interface IJobSearchService
{
    /// <summary>
    /// Busca vagas e posts do LinkedIn com base nas palavras-chave.
    /// Filtra apenas resultados das últimas 24 horas.
    /// </summary>
    Task<SearchResponseDto> SearchAsync(string keywords, CancellationToken ct = default);
}
