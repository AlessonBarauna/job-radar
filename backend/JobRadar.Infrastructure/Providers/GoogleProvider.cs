using System.Text.Json;
using JobRadar.Application.Interfaces;
using JobRadar.Domain.Entities;
using JobRadar.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Providers;

/// <summary>
/// Provedor Google Custom Search JSON API.
/// Sequential, Priority 2. Requer: Search:GoogleApiKey + Search:GoogleCseId.
/// Free tier: 100 queries/dia.
/// </summary>
public class GoogleProvider(
    IHttpClientFactory httpFactory,
    IConfiguration configuration,
    ILogger<GoogleProvider> logger) : IJobProvider
{
    private const string Endpoint = "https://www.googleapis.com/customsearch/v1";

    public string Name => "Google";
    public ProviderMode Mode => ProviderMode.Sequential;
    public int Priority => 2;
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(configuration["Search:GoogleApiKey"]) &&
        !string.IsNullOrWhiteSpace(configuration["Search:GoogleCseId"]);

    public async Task<List<JobResult>> FetchAsync(Keywords keywords, CancellationToken ct = default)
    {
        var apiKey = configuration["Search:GoogleApiKey"]!;
        var cseId  = configuration["Search:GoogleCseId"]!;
        var query  = $"({string.Join(" OR ", keywords.Values)}) vaga emprego";

        var url = $"{Endpoint}?key={apiKey}&cx={cseId}&q={Uri.EscapeDataString(query)}" +
                  "&dateRestrict=d1&lr=lang_pt&num=10";

        logger.LogInformation("Google CSE search: {Query}", query);
        var client = httpFactory.CreateClient("Google");
        var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return Parse(json);
    }

    private static List<JobResult> Parse(string json)
    {
        var results = new List<JobResult>();
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("items", out var items)) return results;

        foreach (var item in items.EnumerateArray())
        {
            var link    = item.TryGetProperty("link", out var l)    ? l.GetString() ?? "" : "";
            var title   = item.TryGetProperty("title", out var t)   ? t.GetString() ?? "" : "";
            var snippet = item.TryGetProperty("snippet", out var s) ? s.GetString() ?? "" : "";

            if (string.IsNullOrEmpty(link)) continue;

            results.Add(JobResult.Create(title, snippet, link,
                DateTime.UtcNow.AddHours(-Random.Shared.Next(1, 12))));
        }

        return results;
    }
}
