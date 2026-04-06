using System.Diagnostics;
using JobRadar.Application.DTOs;
using JobRadar.Application.Interfaces;
using JobRadar.Domain.Entities;
using JobRadar.Domain.Repositories;
using JobRadar.Domain.Services;
using JobRadar.Domain.ValueObjects;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace JobRadar.Application.Services;

/// <summary>
/// Serviço de aplicação — orquestra a busca de vagas.
///
/// Estratégia de provedores (descoberta automática via DI):
///   1. Sequential configurados, em ordem de Priority → para no primeiro com resultados.
///   2. Parallel configurados → rodam juntos, resultados combinados.
///   3. Sequential não configurados (Mock) → fallback final.
///
/// Para adicionar um novo provedor: implemente IJobProvider em Infrastructure e registre no DI.
/// Nenhuma alteração aqui é necessária.
/// </summary>
public class JobSearchService(
    IEnumerable<IJobProvider> providers,
    IRelevanceService relevanceService,
    ISearchHistoryRepository historyRepo,
    IMemoryCache cache,
    ILogger<JobSearchService> logger) : IJobSearchService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

    public async Task<SearchResponseDto> SearchAsync(string rawKeywords, CancellationToken ct = default)
    {
        var keywords = Keywords.Parse(rawKeywords);
        var cacheKey = $"search:{keywords.ToCacheKey()}";
        var sw = Stopwatch.StartNew();

        if (cache.TryGetValue(cacheKey, out SearchResponseDto? cached) && cached != null)
        {
            logger.LogInformation("Cache hit: {Keywords}", keywords.ToDisplay());
            cached.FromCache = true;
            return cached;
        }

        var (results, providerLabel) = await FetchFromProvidersAsync(keywords, ct);

        foreach (var result in results)
        {
            var score   = relevanceService.CalculateScore(result, keywords);
            var matched = relevanceService.FindMatchedKeywords(result, keywords);
            result.ApplyRelevance(score, matched, keywords.ToDisplay());
        }

        var ordered = results
            .OrderByDescending(r => r.RelevanceScore)
            .ThenByDescending(r => r.PublishedAt)
            .ToList();

        sw.Stop();

        var response = new SearchResponseDto
        {
            Results    = ordered.Select(r => MapToDto(r)).ToList(),
            Total      = ordered.Count,
            Keywords   = keywords.Values.ToList(),
            Provider   = providerLabel,
            ElapsedMs  = sw.ElapsedMilliseconds,
            SearchedAt = DateTime.UtcNow,
            FromCache  = false
        };

        cache.Set(cacheKey, response, CacheTtl);

        await historyRepo.SaveAsync(
            SearchHistory.Create(keywords.ToDisplay(), response.Total, sw.ElapsedMilliseconds, providerLabel),
            ct);

        return response;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Estratégia de provedores
    // ─────────────────────────────────────────────────────────────────────

    private async Task<(List<JobResult> results, string label)> FetchFromProvidersAsync(
        Keywords keywords, CancellationToken ct)
    {
        var allProviders = providers.ToList();

        // 1. Sequential configurados (ex: Bing, Google) — ordem de prioridade
        var sequentialConfigured = allProviders
            .Where(p => p.Mode == ProviderMode.Sequential && p.IsConfigured && p.Priority < int.MaxValue)
            .OrderBy(p => p.Priority);

        foreach (var provider in sequentialConfigured)
        {
            var results = await FetchSafeAsync(provider, keywords, ct);
            if (results.Count > 0)
            {
                logger.LogInformation("Provedor utilizado: {Provider}", provider.Name);
                return (results, provider.Name);
            }
        }

        // 2. Parallel (ex: Remotive, Indeed) — rodam juntos
        var parallelProviders = allProviders
            .Where(p => p.Mode == ProviderMode.Parallel && p.IsConfigured)
            .ToList();

        if (parallelProviders.Count > 0)
        {
            logger.LogInformation("Provedores paralelos: {Providers}",
                string.Join(", ", parallelProviders.Select(p => p.Name)));

            var tasks = parallelProviders
                .Select(p => FetchSafeAsync(p, keywords, ct))
                .ToList();

            var allResults = await Task.WhenAll(tasks);

            // Deduplica por URL entre providers (mesma vaga pode aparecer em Gupy + Jobicy)
            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var combined = allResults
                .SelectMany(r => r)
                .Where(r => seenUrls.Add(r.Url))
                .ToList();

            if (combined.Count > 0)
            {
                var activeNames = parallelProviders
                    .Zip(allResults, (p, r) => r.Count > 0 ? p.Name : null)
                    .Where(n => n is not null)
                    .ToList();

                return (combined, string.Join(" + ", activeNames));
            }
        }

        // 3. Mock — fallback absoluto
        var mock = allProviders.FirstOrDefault(p => p.Priority == int.MaxValue);
        if (mock is not null)
        {
            logger.LogWarning("Todos os provedores falharam ou retornaram vazio. Usando Mock.");
            return (await FetchSafeAsync(mock, keywords, ct), mock.Name);
        }

        return ([], "Nenhum");
    }

    private async Task<List<JobResult>> FetchSafeAsync(
        IJobProvider provider, Keywords keywords, CancellationToken ct)
    {
        try { return await provider.FetchAsync(keywords, ct); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Provedor '{Provider}' falhou.", provider.Name);
            return [];
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Mapeamento
    // ─────────────────────────────────────────────────────────────────────

    private static JobResultDto MapToDto(JobResult r) => new()
    {
        Id             = r.Id,
        Title          = r.Title,
        Snippet        = r.Snippet,
        Author         = r.Author,
        Url            = r.Url,
        PublishedAt    = r.PublishedAt,
        RelevanceScore = r.RelevanceScore,
        MatchedKeywords = r.MatchedKeywords
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .ToList(),
        ResultType     = r.ResultType,
        RelativeTime   = ToRelativeTime(r.PublishedAt),
        Source         = InferSource(r.Url),
        WorkplaceType  = InferWorkplaceType(r.Title, r.Snippet)
    };

    private static string InferWorkplaceType(string title, string snippet)
    {
        var text = $"{title} {snippet}";

        var remoteKeywords  = new[] { "remoto", "remote", "100% remoto", "home office", "trabalho remoto" };
        var hybridKeywords  = new[] { "híbrido", "hibrido", "hybrid" };
        var onsiteKeywords  = new[] { "presencial", "on-site", "onsite", "on site" };

        if (hybridKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return "hybrid";

        if (remoteKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return "remote";

        if (onsiteKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return "onsite";

        return "";
    }

    private static string InferSource(string url) =>
        url.Contains("gupy.io",       StringComparison.OrdinalIgnoreCase) ? "Gupy"      :
        url.Contains("jobicy.com",    StringComparison.OrdinalIgnoreCase) ? "Jobicy"    :
        url.Contains("remotive.com",  StringComparison.OrdinalIgnoreCase) ? "Remotive"  :
        url.Contains("adzuna.com",    StringComparison.OrdinalIgnoreCase) ? "Adzuna"    :
        url.Contains("indeed.com",    StringComparison.OrdinalIgnoreCase) ? "Indeed"    :
        url.Contains("linkedin.com",  StringComparison.OrdinalIgnoreCase) ? "LinkedIn"  :
        url.Contains("jooble.org",    StringComparison.OrdinalIgnoreCase) ? "Jooble"    :
        url.Contains("glassdoor.com", StringComparison.OrdinalIgnoreCase) ? "Glassdoor" :
        url.Contains("bing.com",      StringComparison.OrdinalIgnoreCase) ? "Bing"      :
        url.Contains("google.com",    StringComparison.OrdinalIgnoreCase) ? "Google"    :
        "Externo";

    private static string ToRelativeTime(DateTime dt)
    {
        var diff = DateTime.UtcNow - dt;
        return diff.TotalMinutes switch
        {
            < 1    => "agora mesmo",
            < 60   => $"há {(int)diff.TotalMinutes}min",
            < 1440 => $"há {(int)diff.TotalHours}h",
            _      => $"há {(int)diff.TotalDays}d"
        };
    }
}
