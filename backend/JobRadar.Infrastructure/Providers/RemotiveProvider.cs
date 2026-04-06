using System.Text.Json;
using System.Text.RegularExpressions;
using JobRadar.Application.Interfaces;
using JobRadar.Domain.Entities;
using JobRadar.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Providers;

/// <summary>
/// Provedor Remotive.com — gratuito, sem API key.
/// Parallel (roda junto com outros provedores gratuitos).
/// Retorna vagas remotas internacionais de tecnologia.
/// </summary>
public class RemotiveProvider(
    IHttpClientFactory httpFactory,
    ILogger<RemotiveProvider> logger) : IJobProvider
{
    public string Name => "Remotive";
    public ProviderMode Mode => ProviderMode.Parallel;
    public int Priority => 10;
    public bool IsConfigured => true;

    /// <summary>
    /// Remotive busca em título/descrição — usa termos "bonitos" para melhor recall.
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
        var client   = httpFactory.CreateClient("Remotive");
        var results  = new List<JobResult>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Converte para termos legíveis antes de buscar
        var displayTerms = keywords.Values
            .Select(k => DisplayMap.TryGetValue(k, out var m) ? m : k)
            .ToList();

        // Busca combinada + individuais para maximizar resultados
        var searchTerms = new List<string> { string.Join(" ", displayTerms) };
        searchTerms.AddRange(displayTerms.Take(2));

        foreach (var term in searchTerms)
        {
            try
            {
                var url = $"https://remotive.com/api/remote-jobs?search={Uri.EscapeDataString(term)}&limit=20";
                logger.LogInformation("Remotive: '{Term}'", term);

                var response = await client.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(ct);
                results.AddRange(ParseResponse(json, seenUrls));

                await Task.Delay(300, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Remotive falhou para '{Term}'", term);
            }
        }

        return results;
    }

    private static List<JobResult> ParseResponse(string json, HashSet<string> seenUrls)
    {
        var results = new List<JobResult>();
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("jobs", out var jobs)) return results;

        foreach (var job in jobs.EnumerateArray())
        {
            var jobUrl = job.TryGetProperty("url", out var u)         ? u.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(jobUrl) || !seenUrls.Add(jobUrl)) continue;

            var title       = job.TryGetProperty("title", out var t)          ? t.GetString() ?? "" : "";
            var company     = job.TryGetProperty("company_name", out var c)    ? c.GetString() ?? "" : "";
            var category    = job.TryGetProperty("category", out var cat)      ? cat.GetString() ?? "" : "";
            var description = job.TryGetProperty("description", out var d)     ? d.GetString() ?? "" : "";
            var location    = job.TryGetProperty("candidate_required_location", out var loc) ? loc.GetString() ?? "" : "";
            var pubDateStr  = job.TryGetProperty("publication_date", out var pd) ? pd.GetString() : null;

            var publishedAt = pubDateStr != null && DateTime.TryParse(pubDateStr, out var parsed)
                ? parsed.ToUniversalTime()
                : DateTime.UtcNow.AddHours(-Random.Shared.Next(1, 48));

            var snippet = BuildSnippet(description, category, location);
            var displayTitle = company.Length > 0 ? $"{title} | {company}" : title;

            results.Add(JobResult.Create(displayTitle, snippet, jobUrl, publishedAt, company));
        }

        return results;
    }

    private static string BuildSnippet(string html, string category, string location)
    {
        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = Regex.Replace(text, @"\s{2,}", " ").Trim();
        text = text.Replace("&amp;", "&").Replace("&nbsp;", " ")
                   .Replace("&#39;", "'").Replace("&quot;", "\"");

        var tags = new List<string>();
        if (category.Length > 0) tags.Add(category);
        if (location.Length > 0) tags.Add(location);

        var prefix = tags.Count > 0 ? $"[{string.Join(" | ", tags)}] " : "";
        var full   = prefix + text;
        return full.Length <= 400 ? full : full[..397] + "...";
    }
}
