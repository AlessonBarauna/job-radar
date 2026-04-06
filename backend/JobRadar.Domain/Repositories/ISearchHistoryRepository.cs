using JobRadar.Domain.Entities;

namespace JobRadar.Domain.Repositories;

/// <summary>
/// Interface de repositório de histórico de buscas.
/// Implementada na camada Infrastructure (EF Core).
/// </summary>
public interface ISearchHistoryRepository
{
    Task SaveAsync(SearchHistory history, CancellationToken ct = default);
    Task<IReadOnlyList<SearchHistory>> GetRecentAsync(int limit, CancellationToken ct = default);
}
