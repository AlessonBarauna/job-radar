using JobRadar.Domain.Entities;
using JobRadar.Domain.Repositories;
using JobRadar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Repositories;

public class SearchHistoryRepository(AppDbContext db) : ISearchHistoryRepository
{
    public async Task SaveAsync(SearchHistory history, CancellationToken ct = default)
    {
        db.SearchHistories.Add(history);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SearchHistory>> GetRecentAsync(int limit, CancellationToken ct = default)
    {
        return await db.SearchHistories
            .OrderByDescending(h => h.SearchedAt)
            .Take(limit)
            .ToListAsync(ct);
    }
}
