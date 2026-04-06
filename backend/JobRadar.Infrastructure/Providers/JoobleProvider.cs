using System.Text;
using System.Text.Json;
using JobRadar.Application.Interfaces;
using JobRadar.Domain.Entities;
using JobRadar.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Providers;

/// <summary>
/// Provedor Jooble — vagas brasileiras. API key gratuita em jooble.org/api/about.
/// Sequential, Priority 3. Requer: Search:JoobleApiKey.
/// </summary>
public class JoobleProvider(
    IHttpClientFactory httpFactory,
    IConfiguration configuration,
    ILogger<JoobleProvider> logger) : IJobProvider
{
    public string Name => "Jooble";
    public ProviderMode Mode => ProviderMode.Sequential;
    public int Priority => 3;
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(configuration["Search:JoobleApiKey"]);

    public async Task<List<JobResult>> FetchAsync(Keywords keywords, CancellationToken ct = default)
    {
        var apiKey = configuration["Search:JoobleApiKey"]!;
        var body   = new { keywords = string.Join(" ", keywords.Values), location = "Brasil", page = 1 };
        var json   = JsonSerializer.Serialize(body);

        logger.LogInformation("Jooble search: {Keywords}", keywords.ToDisplay());

        var client   = httpFactory.CreateClient("Jooble");
        var response = await client.PostAsync(
            $"https://br.jooble.org/api/{apiKey}",
            new StringContent(json, Encoding.UTF8, "application/json"),
            ct);

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        return Parse(responseJson);
    }

    private static List<JobResult> Parse(string json)
    {
        var results = new List<JobResult>();
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("jobs", out var jobs)) return results;

        foreach (var job in jobs.EnumerateArray())
        {
            var title    = job.TryGetProperty("title", out var t)   ? t.GetString() ?? "" : "";
            var snippet  = job.TryGetProperty("snippet", out var s) ? s.GetString() ?? "" : "";
            var company  = job.TryGetProperty("company", out var c) ? c.GetString() ?? "" : "";
            var location = job.TryGetProperty("location", out var l) ? l.GetString() ?? "" : "";
            var link     = job.TryGetProperty("link", out var lk)   ? lk.GetString() ?? "" : "";
            var updated  = job.TryGetProperty("updated", out var u) ? u.GetString() : null;

            if (string.IsNullOrEmpty(link)) continue;

            var publishedAt = updated != null && DateTime.TryParse(updated, out var dt)
                ? dt.ToUniversalTime()
                : DateTime.UtcNow.AddHours(-Random.Shared.Next(1, 24));

            var fullSnippet = location.Length > 0 ? $"[{location}] {snippet}" : snippet;
            var display     = company.Length > 0 ? $"{title} | {company}" : title;

            results.Add(JobResult.Create(display,
                fullSnippet.Length <= 400 ? fullSnippet : fullSnippet[..397] + "...",
                link, publishedAt, company));
        }

        return results;
    }
}
