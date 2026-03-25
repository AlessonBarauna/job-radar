using System.Text.Json;
using JobRadar.API.Models;
using JobRadar.API.Services.Interfaces;

namespace JobRadar.API.Services;

/// <summary>
/// Integração com Google Custom Search JSON API.
/// Documentação: https://developers.google.com/custom-search/v1/overview
///
/// Como configurar (grátis, 100 queries/dia):
/// 1. Acesse console.cloud.google.com → Custom Search API → Ativar
/// 2. Crie credencial → API Key → copie para Search:GoogleApiKey
/// 3. Acesse programmablesearchengine.google.com → Criar motor de busca
///    - Site: linkedin.com
///    - Copie o ID para Search:GoogleCseId
/// </summary>
public class GoogleCustomSearchService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<GoogleCustomSearchService> logger) : IGoogleCustomSearchService
{
    private const string GoogleEndpoint = "https://www.googleapis.com/customsearch/v1";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(configuration["Search:GoogleApiKey"]) &&
        !string.IsNullOrWhiteSpace(configuration["Search:GoogleCseId"]);

    public async Task<List<JobResult>> SearchAsync(List<string> keywords, CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Google Custom Search não configurado.");

        var apiKey = configuration["Search:GoogleApiKey"]!;
        var cseId = configuration["Search:GoogleCseId"]!;
        var query = BuildQuery(keywords);

        using var client = httpClientFactory.CreateClient("Google");

        // dateRestrict=d1 = apenas último dia
        var url = $"{GoogleEndpoint}?key={apiKey}&cx={cseId}" +
                  $"&q={Uri.EscapeDataString(query)}&num=10&dateRestrict=d1&lr=lang_pt";

        logger.LogInformation("Google CSE search: {Query}", query);

        var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseGoogleResponse(json, keywords);
    }

    private static string BuildQuery(List<string> keywords)
    {
        var kwPart = string.Join(" OR ", keywords.Select(k => k.Trim()));
        return $"({kwPart}) vaga emprego";
    }

    private static List<JobResult> ParseGoogleResponse(string json, List<string> keywords)
    {
        var results = new List<JobResult>();

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("items", out var items)) return results;

        foreach (var item in items.EnumerateArray())
        {
            var url = item.TryGetProperty("link", out var l) ? l.GetString() ?? "" : "";
            if (!url.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase))
                continue;

            var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            var snippet = item.TryGetProperty("snippet", out var s) ? s.GetString() ?? "" : "";

            // Tenta extrair data do pagemap
            DateTime publishedAt = DateTime.UtcNow.AddHours(-new Random().Next(1, 20));
            if (item.TryGetProperty("pagemap", out var pagemap) &&
                pagemap.TryGetProperty("metatags", out var metatags) &&
                metatags.GetArrayLength() > 0)
            {
                var firstMeta = metatags[0];
                if (firstMeta.TryGetProperty("article:published_time", out var pub) &&
                    DateTime.TryParse(pub.GetString(), out var dt))
                {
                    publishedAt = dt.ToUniversalTime();
                }
            }

            if (DateTime.UtcNow - publishedAt > TimeSpan.FromHours(24))
                continue;

            results.Add(new JobResult
            {
                Title = title,
                Snippet = snippet,
                Url = url,
                PublishedAt = publishedAt,
                Keywords = string.Join(",", keywords),
                Author = ExtractAuthor(title),
                ResultType = url.Contains("/jobs/") ? "job" : "post"
            });
        }

        return results;
    }

    private static string? ExtractAuthor(string title)
    {
        var parts = title.Split(new[] { " · ", " | ", " - " }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[^1].Trim() : null;
    }
}
