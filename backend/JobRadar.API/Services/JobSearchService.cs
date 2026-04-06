using System.Diagnostics;
using JobRadar.API.Data;
using JobRadar.API.DTOs;
using JobRadar.API.Models;
using JobRadar.API.Repositories.Interfaces;
using JobRadar.API.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace JobRadar.API.Services;

/// <summary>
/// Serviço principal de busca.
/// Estratégia:
///   1. Bing ou Google (se configurados) — retorna imediatamente.
///   2. Remotive + Indeed Brasil em paralelo (gratuitos, sem API key) — combina os resultados.
///   3. Jooble (se configurado).
///   4. Mock como último fallback.
/// Aplica cache de 15 minutos por conjunto de keywords.
/// </summary>
public class JobSearchService(
    IBingSearchService bingService,
    IGoogleCustomSearchService googleService,
    IRemotiveSearchService remotiveService,
    IIndeedRssSearchService indeedService,
    IJoobleSearchService joobleService,
    IRelevanceService relevanceService,
    ISearchHistoryRepository historyRepo,
    IMemoryCache cache,
    ILogger<JobSearchService> logger) : IJobSearchService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

    public async Task<SearchResponseDto> SearchAsync(string keywords, CancellationToken ct = default)
    {
        var keywordList = ParseKeywords(keywords);
        if (keywordList.Count == 0)
            return new SearchResponseDto { Results = [], Total = 0 };

        var cacheKey = $"search:{string.Join("|", keywordList.OrderBy(k => k))}";
        var sw = Stopwatch.StartNew();

        // Verifica cache
        if (cache.TryGetValue(cacheKey, out SearchResponseDto? cached) && cached != null)
        {
            logger.LogInformation("Cache hit para keywords: {Keywords}", keywords);
            cached.FromCache = true;
            return cached;
        }

        // Executa busca no provedor disponível
        var (results, provider) = await FetchFromProviderAsync(keywordList, ct);

        // Calcula relevância e ordena
        foreach (var result in results)
        {
            result.RelevanceScore = relevanceService.CalculateScore(result, keywordList);
            var matched = relevanceService.FindMatchedKeywords(result, keywordList);
            result.MatchedKeywords = string.Join(",", matched);
            result.Keywords = string.Join(",", keywordList);
        }

        var ordered = results
            .OrderByDescending(r => r.RelevanceScore)
            .ThenByDescending(r => r.PublishedAt)
            .ToList();

        sw.Stop();

        var response = new SearchResponseDto
        {
            Results = ordered.Select(MapToDto).ToList(),
            Total = ordered.Count,
            Keywords = keywordList,
            Provider = provider,
            ElapsedMs = sw.ElapsedMilliseconds,
            SearchedAt = DateTime.UtcNow,
            FromCache = false
        };

        // Salva no cache
        cache.Set(cacheKey, response, CacheTtl);

        // Persiste histórico
        await historyRepo.SaveAsync(new SearchHistory
        {
            Keywords = string.Join(",", keywordList),
            ResultCount = response.Total,
            ElapsedMs = sw.ElapsedMilliseconds,
            Provider = provider,
            SearchedAt = DateTime.UtcNow
        }, ct);

        return response;
    }

    // -------------------------------------------------
    // Cascata: Bing → Google → Remotive → Jooble → Mock
    // -------------------------------------------------
    private async Task<(List<JobResult>, string)> FetchFromProviderAsync(
        List<string> keywords, CancellationToken ct)
    {
        // Bing (requer BingApiKey em appsettings)
        if (bingService.IsConfigured)
        {
            try
            {
                logger.LogInformation("Usando provedor: Bing");
                var results = await bingService.SearchAsync(keywords, ct);
                if (results.Count > 0) return (results, "Bing");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Bing falhou, tentando Google.");
            }
        }

        // Google CSE (requer GoogleApiKey + GoogleCseId em appsettings)
        if (googleService.IsConfigured)
        {
            try
            {
                logger.LogInformation("Usando provedor: Google CSE");
                var results = await googleService.SearchAsync(keywords, ct);
                if (results.Count > 0) return (results, "Google");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Google falhou, tentando Remotive.");
            }
        }

        // Remotive + Indeed Brasil em paralelo (gratuitos, sem API key)
        logger.LogInformation("Usando provedores gratuitos: Remotive + Indeed Brasil (paralelo)");
        var remotiveTask = FetchSafe(() => remotiveService.SearchAsync(keywords, ct), "Remotive");
        var indeedTask   = FetchSafe(() => indeedService.SearchAsync(keywords, ct), "Indeed RSS");

        await Task.WhenAll(remotiveTask, indeedTask);

        var freeResults = remotiveTask.Result.Concat(indeedTask.Result).ToList();
        if (freeResults.Count > 0)
        {
            var providerLabel = (remotiveTask.Result.Count > 0, indeedTask.Result.Count > 0) switch
            {
                (true, true)   => "Remotive + Indeed",
                (true, false)  => "Remotive",
                (false, true)  => "Indeed",
                _              => "Gratuito"
            };
            return (freeResults, providerLabel);
        }

        // Jooble (gratuito com API key — vagas brasileiras)
        if (joobleService.IsConfigured)
        {
            try
            {
                logger.LogInformation("Usando provedor: Jooble");
                var results = await joobleService.SearchAsync(keywords, ct);
                if (results.Count > 0) return (results, "Jooble");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Jooble falhou, usando Mock.");
            }
        }

        // Mock (fallback de desenvolvimento)
        logger.LogInformation("Usando provedor: Mock");
        return (MockSearchService.Generate(keywords), "Mock");
    }

    // -------------------------------------------------
    // Helpers
    // -------------------------------------------------

    /// <summary>
    /// Executa uma busca capturando exceções — retorna lista vazia em caso de falha.
    /// Usado para chamar provedores em paralelo sem interromper os demais.
    /// </summary>
    private async Task<List<JobResult>> FetchSafe(
        Func<Task<List<JobResult>>> fetch, string providerName)
    {
        try { return await fetch(); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Provedor '{Provider}' falhou.", providerName);
            return [];
        }
    }

    private static List<string> ParseKeywords(string input) =>
        input.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
             .Where(k => k.Length >= 2)
             .Distinct(StringComparer.OrdinalIgnoreCase)
             .Take(10)
             .ToList();

    private static JobResultDto MapToDto(JobResult r) => new()
    {
        Id = r.Id,
        Title = r.Title,
        Snippet = r.Snippet,
        Author = r.Author,
        Url = r.Url,
        PublishedAt = r.PublishedAt,
        RelevanceScore = r.RelevanceScore,
        MatchedKeywords = r.MatchedKeywords.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
        ResultType = r.ResultType,
        RelativeTime = ToRelativeTime(r.PublishedAt)
    };

    private static string ToRelativeTime(DateTime dt)
    {
        var diff = DateTime.UtcNow - dt;
        return diff.TotalMinutes switch
        {
            < 1 => "agora mesmo",
            < 60 => $"há {(int)diff.TotalMinutes}min",
            < 1440 => $"há {(int)diff.TotalHours}h",
            _ => $"há {(int)diff.TotalDays}d"
        };
    }
}
