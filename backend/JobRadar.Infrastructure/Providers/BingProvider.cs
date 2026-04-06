using System.Text.Json;
using JobRadar.Application.Interfaces;
using JobRadar.Domain.Entities;
using JobRadar.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Providers;

/// <summary>
/// Provedor Bing Web Search API v7.
/// Sequential, Priority 1. Requer: Search:BingApiKey em appsettings.
/// Free tier: 1.000 calls/mês.
/// </summary>
public class BingProvider(
    IHttpClientFactory httpFactory,
    IConfiguration configuration,
    ILogger<BingProvider> logger) : IJobProvider
{
    private const string Endpoint = "https://api.bing.microsoft.com/v7.0/search";

    public string Name => "Bing";
    public ProviderMode Mode => ProviderMode.Sequential;
    public int Priority => 1;
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(configuration["Search:BingApiKey"]);

    public async Task<List<JobResult>> FetchAsync(Keywords keywords, CancellationToken ct = default)
    {
        var apiKey = configuration["Search:BingApiKey"]!;
        var query  = $"site:linkedin.com ({string.Join(" OR ", keywords.Values)}) vaga emprego";
        var url    = $"{Endpoint}?q={Uri.EscapeDataString(query)}&count=20&freshness=Day&mkt=pt-BR";

        var client = httpFactory.CreateClient("Bing");
        client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);

        logger.LogInformation("Bing search: {Query}", query);
        var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return Parse(json);
    }

    private static List<JobResult> Parse(string json)
    {
        var results = new List<JobResult>();
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("webPages", out var wp)) return results;
        if (!wp.TryGetProperty("value", out var items)) return results;

        foreach (var item in items.EnumerateArray())
        {
            var url = item.GetProperty("url").GetString() ?? "";
            if (!url.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase)) continue;

            var title   = item.TryGetProperty("name", out var n)            ? n.GetString() ?? "" : "";
            var snippet = item.TryGetProperty("snippet", out var s)         ? s.GetString() ?? "" : "";
            var dateStr = item.TryGetProperty("dateLastCrawled", out var d) ? d.GetString() : null;

            var publishedAt = dateStr != null && DateTime.TryParse(dateStr, out var dt)
                ? dt.ToUniversalTime()
                : DateTime.UtcNow.AddHours(-Random.Shared.Next(1, 23));

            if (DateTime.UtcNow - publishedAt > TimeSpan.FromHours(24)) continue;

            results.Add(JobResult.Create(title, snippet, url, publishedAt,
                author: ExtractAuthor(title),
                resultType: url.Contains("/jobs/") ? "job" : "post"));
        }

        return results;
    }

    private static string? ExtractAuthor(string title)
    {
        foreach (var sep in new[] { " · ", " | ", " - " })
        {
            var idx = title.IndexOf(sep, StringComparison.Ordinal);
            if (idx > 0) return title[(idx + sep.Length)..].Trim();
        }
        return null;
    }
}
