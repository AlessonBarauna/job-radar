using JobRadar.API.Models;

namespace JobRadar.API.Repositories.Interfaces;

/// <summary>
/// Repositório para persistência do histórico de buscas.
/// </summary>
public interface ISearchHistoryRepository
{
    Task SaveAsync(SearchHistory history, CancellationToken ct = default);
    Task<List<SearchHistory>> GetRecentAsync(int limit = 20, CancellationToken ct = default);
    Task<int> ClearOlderThanAsync(TimeSpan age, CancellationToken ct = default);
}
