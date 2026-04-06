using System.Text.Json;
using System.Text.RegularExpressions;
using JobRadar.API.Models;
using JobRadar.API.Services.Interfaces;

namespace JobRadar.API.Services;

/// <summary>
/// Integração com Remotive.com API — gratuita, sem API key.
/// Retorna vagas remotas internacionais de tecnologia.
/// Endpoint: GET https://remotive.com/api/remote-jobs?search={query}&limit=20
/// </summary>
public class RemotiveSearchService(
    IHttpClientFactory httpFactory,
    ILogger<RemotiveSearchService> logger) : IRemotiveSearchService
{
    public bool IsConfigured => true;

    /// <summary>
    /// Normaliza keywords para os termos usados internamente pelo Remotive.
    /// Ex: ".net" → "dotnet", "c#" → "csharp"
    /// </summary>
    private static readonly Dictionary<string, string> KeywordMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".net"]        = "dotnet",
            ["c#"]          = "csharp",
            ["node.js"]     = "nodejs",
            ["node"]        = "nodejs",
            ["vue.js"]      = "vue",
            ["react.js"]    = "react",
            ["asp.net"]     = "aspnet",
            ["golang"]      = "go",
            ["k8s"]         = "kubernetes",
        };

    public async Task<List<JobResult>> SearchAsync(List<string> keywords, CancellationToken ct = default)
    {
        var client = httpFactory.CreateClient("Remotive");
        var results = new List<JobResult>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Normaliza keywords para os termos do Remotive
        var normalized = keywords
            .Select(k => KeywordMap.TryGetValue(k, out var mapped) ? mapped : k)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Uma busca combinada com todos os keywords + buscas individuais para cada um
        var searchTerms = new List<string> { string.Join(" ", normalized) };
        searchTerms.AddRange(normalized.Take(2));

        foreach (var term in searchTerms)
        {
            try
            {
                var url = $"https://remotive.com/api/remote-jobs?search={Uri.EscapeDataString(term)}&limit=20";
                logger.LogInformation("Remotive search: '{Term}'", term);

                var response = await client.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(ct);
                var jobs = ParseResponse(json, keywords, seenUrls);
                results.AddRange(jobs);

                await Task.Delay(300, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Remotive falhou para termo '{Term}'", term);
            }
        }

        return results;
    }

    private static List<JobResult> ParseResponse(
        string json, List<string> keywords, HashSet<string> seenUrls)
    {
        var results = new List<JobResult>();

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("jobs", out var jobs))
            return results;

        foreach (var job in jobs.EnumerateArray())
        {
            var jobUrl = job.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(jobUrl) || !seenUrls.Add(jobUrl))
                continue;

            var title       = job.TryGetProperty("title", out var t)   ? t.GetString() ?? "" : "";
            var company     = job.TryGetProperty("company_name", out var c) ? c.GetString() ?? "" : "";
            var category    = job.TryGetProperty("category", out var cat) ? cat.GetString() ?? "" : "";
            var description = job.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
            var pubDateStr  = job.TryGetProperty("publication_date", out var pd) ? pd.GetString() : null;
            var location    = job.TryGetProperty("candidate_required_location", out var loc) ? loc.GetString() ?? "" : "";

            var publishedAt = pubDateStr != null && DateTime.TryParse(pubDateStr, out var parsed)
                ? parsed.ToUniversalTime()
                : DateTime.UtcNow.AddHours(-new Random().Next(1, 12));

            var snippet = BuildSnippet(description, category, location);

            results.Add(new JobResult
            {
                Title       = company.Length > 0 ? $"{title} | {company}" : title,
                Snippet     = snippet,
                Author      = company,
                Url         = jobUrl,
                PublishedAt = publishedAt,
                Keywords    = string.Join(",", keywords),
                ResultType  = "job"
            });
        }

        return results;
    }

    private static string BuildSnippet(string html, string category, string location)
    {
        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = Regex.Replace(text, @"\s{2,}", " ").Trim();
        text = text.Replace("&amp;", "&").Replace("&lt;", "<")
                   .Replace("&gt;", ">").Replace("&nbsp;", " ")
                   .Replace("&#39;", "'").Replace("&quot;", "\"");

        var tags = new List<string>();
        if (category.Length > 0) tags.Add(category);
        if (location.Length > 0) tags.Add(location);

        var prefix = tags.Count > 0 ? $"[{string.Join(" | ", tags)}] " : "";
        var full = prefix + text;

        return full.Length <= 400 ? full : full[..397] + "...";
    }
}
