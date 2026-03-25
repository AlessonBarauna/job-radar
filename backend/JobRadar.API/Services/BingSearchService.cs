using System.Text.Json;
using JobRadar.API.Models;
using JobRadar.API.Services.Interfaces;

namespace JobRadar.API.Services;

/// <summary>
/// Integração com Bing Web Search API v7.
/// Documentação: https://learn.microsoft.com/en-us/bing/search-apis/bing-web-search/overview
///
/// Como obter chave gratuita:
/// 1. Acesse portal.azure.com
/// 2. Crie recurso "Bing Search v7"
/// 3. Copie a chave para appsettings: Search:BingApiKey
/// Free tier: 1000 calls/mês.
/// </summary>
public class BingSearchService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<BingSearchService> logger) : IBingSearchService
{
    private const string BingEndpoint = "https://api.bing.microsoft.com/v7.0/search";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(configuration["Search:BingApiKey"]);

    public async Task<List<JobResult>> SearchAsync(List<string> keywords, CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Bing API key não configurada.");

        var apiKey = configuration["Search:BingApiKey"]!;
        var query = BuildQuery(keywords);

        using var client = httpClientFactory.CreateClient("Bing");
        client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);

        // freshness=Day = apenas últimas 24h
        var url = $"{BingEndpoint}?q={Uri.EscapeDataString(query)}&count=20&freshness=Day&mkt=pt-BR";

        logger.LogInformation("Bing search: {Query}", query);

        var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseBingResponse(json, keywords);
    }

    /// <summary>
    /// Monta query: site:linkedin.com (kw1 OR kw2 OR kw3) vaga
    /// </summary>
    private static string BuildQuery(List<string> keywords)
    {
        var kwPart = string.Join(" OR ", keywords.Select(k => k.Trim()));
        return $"site:linkedin.com ({kwPart}) vaga emprego";
    }

    private static List<JobResult> ParseBingResponse(string json, List<string> keywords)
    {
        var results = new List<JobResult>();

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("webPages", out var webPages)) return results;
        if (!webPages.TryGetProperty("value", out var items)) return results;

        foreach (var item in items.EnumerateArray())
        {
            var url = item.GetProperty("url").GetString() ?? "";

            // Apenas URLs do LinkedIn
            if (!url.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase))
                continue;

            var title = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var snippet = item.TryGetProperty("snippet", out var s) ? s.GetString() ?? "" : "";
            var dateStr = item.TryGetProperty("dateLastCrawled", out var d) ? d.GetString() : null;

            var publishedAt = dateStr != null && DateTime.TryParse(dateStr, out var dt)
                ? dt.ToUniversalTime()
                : DateTime.UtcNow.AddHours(-new Random().Next(1, 23));

            // Só últimas 24h
            if (DateTime.UtcNow - publishedAt > TimeSpan.FromHours(24))
                continue;

            results.Add(new JobResult
            {
                Title = title,
                Snippet = snippet,
                Url = url,
                PublishedAt = publishedAt,
                Keywords = string.Join(",", keywords),
                Author = ExtractAuthor(title, snippet),
                ResultType = url.Contains("/jobs/") ? "job" : "post"
            });
        }

        return results;
    }

    private static string? ExtractAuthor(string title, string snippet)
    {
        // Tenta extrair "Empresa · " do título do LinkedIn
        var separators = new[] { " · ", " | ", " - " };
        foreach (var sep in separators)
        {
            var idx = title.IndexOf(sep, StringComparison.Ordinal);
            if (idx > 0)
                return title[idx..].Replace(sep, "").Trim();
        }
        return null;
    }
}
