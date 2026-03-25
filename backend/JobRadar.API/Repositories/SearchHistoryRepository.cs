using JobRadar.API.Data;
using JobRadar.API.Models;
using JobRadar.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.API.Repositories;

/// <summary>
/// Implementação EF Core do repositório de histórico.
/// </summary>
public class SearchHistoryRepository(AppDbContext db) : ISearchHistoryRepository
{
    public async Task SaveAsync(SearchHistory history, CancellationToken ct = default)
    {
        db.SearchHistories.Add(history);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<SearchHistory>> GetRecentAsync(int limit = 20, CancellationToken ct = default) =>
        await db.SearchHistories
            .OrderByDescending(h => h.SearchedAt)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<int> ClearOlderThanAsync(TimeSpan age, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - age;
        return await db.SearchHistories
            .Where(h => h.SearchedAt < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}
