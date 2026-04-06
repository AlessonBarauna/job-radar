using System.Text;
using System.Text.Json;
using JobRadar.API.Models;
using JobRadar.API.Services.Interfaces;

namespace JobRadar.API.Services;

/// <summary>
/// Integração com Jooble API — foco no mercado brasileiro.
/// API key gratuita: acesse https://jooble.org/api/about e solicite por e-mail.
/// Configure em appsettings: Search:JoobleApiKey
/// Endpoint: POST https://br.jooble.org/api/{apiKey}
/// </summary>
public class JoobleSearchService(
    IHttpClientFactory httpFactory,
    IConfiguration configuration,
    ILogger<JoobleSearchService> logger) : IJoobleSearchService
{
    private const string JoobleEndpoint = "https://br.jooble.org/api";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(configuration["Search:JoobleApiKey"]);

    public async Task<List<JobResult>> SearchAsync(List<string> keywords, CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Jooble API key não configurada.");

        var apiKey = configuration["Search:JoobleApiKey"]!;
        var client = httpFactory.CreateClient("Jooble");

        var query = string.Join(" ", keywords);
        var body = new { keywords = query, location = "Brasil", page = 1 };
        var json = JsonSerializer.Serialize(body);

        logger.LogInformation("Jooble search: {Query}", query);

        var response = await client.PostAsync(
            $"{JoobleEndpoint}/{apiKey}",
            new StringContent(json, Encoding.UTF8, "application/json"),
            ct);

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        return ParseResponse(responseJson, keywords);
    }

    private static List<JobResult> ParseResponse(string json, List<string> keywords)
    {
        var results = new List<JobResult>();

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("jobs", out var jobs))
            return results;

        foreach (var job in jobs.EnumerateArray())
        {
            var title = job.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            var snippet = job.TryGetProperty("snippet", out var s) ? s.GetString() ?? "" : "";
            var company = job.TryGetProperty("company", out var c) ? c.GetString() ?? "" : "";
            var location = job.TryGetProperty("location", out var l) ? l.GetString() ?? "" : "";
            var link = job.TryGetProperty("link", out var lk) ? lk.GetString() ?? "" : "";
            var updatedStr = job.TryGetProperty("updated", out var u) ? u.GetString() : null;

            if (string.IsNullOrEmpty(link)) continue;

            var publishedAt = updatedStr != null && DateTime.TryParse(updatedStr, out var parsed)
                ? parsed.ToUniversalTime()
                : DateTime.UtcNow.AddHours(-new Random().Next(1, 12));

            var displayTitle = company.Length > 0 ? $"{title} | {company}" : title;
            var fullSnippet = location.Length > 0 ? $"[{location}] {snippet}" : snippet;

            results.Add(new JobResult
            {
                Title = displayTitle,
                Snippet = fullSnippet.Length <= 350 ? fullSnippet : fullSnippet[..347] + "...",
                Author = company,
                Url = link,
                PublishedAt = publishedAt,
                Keywords = string.Join(",", keywords),
                ResultType = "job"
            });
        }

        return results;
    }
}
