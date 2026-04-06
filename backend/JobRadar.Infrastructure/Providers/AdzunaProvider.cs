using System.Text.Json;
using System.Text.RegularExpressions;
using JobRadar.Application.Interfaces;
using JobRadar.Domain.Entities;
using JobRadar.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Providers;

/// <summary>
/// Provedor Adzuna — gratuito com key (1000 calls/mês).
/// Registro em: https://developer.adzuna.com/
/// Sequential, Priority 4. Foco no mercado de trabalho brasileiro.
/// API: https://api.adzuna.com/v1/api/jobs/br/search/1?app_id={id}&amp;app_key={key}&amp;what={keywords}
/// </summary>
public class AdzunaProvider(
    IHttpClientFactory httpFactory,
    IConfiguration config,
    ILogger<AdzunaProvider> logger) : IJobProvider
{
    public string Name => "Adzuna";
    public ProviderMode Mode => ProviderMode.Sequential;
    public int Priority => 4;

    private string AppId  => config["Search:AdzunaAppId"]  ?? "";
    private string AppKey => config["Search:AdzunaAppKey"] ?? "";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(AppKey);

    /// <summary>
    /// Termos no formato legível para busca textual (não normalizado).
    /// Adzuna busca título/descrição, não tags.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> DisplayMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["dotnet"]     = ".NET",
            ["csharp"]     = "C#",
            ["nodejs"]     = "Node.js",
            ["aspnet"]     = "ASP.NET",
            ["golang"]     = "Go",
            ["kubernetes"] = "Kubernetes",
        };

    public async Task<List<JobResult>> FetchAsync(Keywords keywords, CancellationToken ct = default)
    {
        var client   = httpFactory.CreateClient("Adzuna");
        var results  = new List<JobResult>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Busca combinada principal
        var displayTerms = keywords.Values
            .Select(k => DisplayMap.TryGetValue(k, out var m) ? m : k)
            .ToList();
        var combined = string.Join(" ", displayTerms);

        try
        {
            results.AddRange(await SearchAsync(client, combined, seenUrls, ct));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Adzuna falhou para '{Term}'", combined);
            return results;
        }

        // Busca individual para keywords restantes se resultado foi fraco
        if (results.Count < 5)
        {
            foreach (var term in displayTerms.Take(2))
            {
                try
                {
                    results.AddRange(await SearchAsync(client, term, seenUrls, ct));
                    await Task.Delay(500, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Adzuna falhou para '{Term}'", term);
                }
            }
        }

        return results;
    }

    private async Task<List<JobResult>> SearchAsync(
        HttpClient client, string what, HashSet<string> seenUrls, CancellationToken ct)
    {
        var url = $"https://api.adzuna.com/v1/api/jobs/br/search/1" +
                  $"?app_id={Uri.EscapeDataString(AppId)}" +
                  $"&app_key={Uri.EscapeDataString(AppKey)}" +
                  $"&what={Uri.EscapeDataString(what)}" +
                  $"&results_per_page=20" +
                  $"&sort_by=date" +
                  $"&content-type=application/json";

        logger.LogInformation("Adzuna BR: '{What}'", what);

        var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseResponse(json, seenUrls);
    }

    private static List<JobResult> ParseResponse(string json, HashSet<string> seenUrls)
    {
        var results = new List<JobResult>();
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("results", out var items)) return results;

        foreach (var item in items.EnumerateArray())
        {
            var jobUrl  = item.TryGetProperty("redirect_url", out var u) ? u.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(jobUrl) || !seenUrls.Add(jobUrl)) continue;

            var title   = item.TryGetProperty("title", out var t)       ? t.GetString() ?? "" : "";
            var desc    = item.TryGetProperty("description", out var d)  ? d.GetString() ?? "" : "";
            var created = item.TryGetProperty("created", out var cr)     ? cr.GetString() : null;

            var company  = "";
            var location = "";

            if (item.TryGetProperty("company",  out var co) && co.TryGetProperty("display_name", out var cn))
                company = cn.GetString() ?? "";
            if (item.TryGetProperty("location", out var lo) && lo.TryGetProperty("display_name", out var ln))
                location = ln.GetString() ?? "";

            var publishedAt = created != null && DateTime.TryParse(created, out var parsed)
                ? parsed.ToUniversalTime()
                : DateTime.UtcNow.AddHours(-Random.Shared.Next(1, 72));

            var snippet      = BuildSnippet(desc, location);
            var displayTitle = company.Length > 0 ? $"{title} | {company}" : title;

            results.Add(JobResult.Create(displayTitle, snippet, jobUrl, publishedAt, company));
        }

        return results;
    }

    private static string BuildSnippet(string text, string location)
    {
        text = Regex.Replace(text, @"\s{2,}", " ").Trim();
        var prefix = location.Length > 0 ? $"[{location}] " : "";
        var full   = prefix + text;
        return full.Length <= 400 ? full : full[..397] + "...";
    }
}
