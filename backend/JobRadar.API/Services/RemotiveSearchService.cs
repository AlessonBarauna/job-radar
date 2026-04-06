using System.Text.Json;
using System.Text.RegularExpressions;
using JobRadar.API.Models;
using JobRadar.API.Services.Interfaces;

namespace JobRadar.API.Services;

/// <summary>
/// Integração com Remotive.com API — gratuita, sem API key.
/// Retorna vagas remotas reais de tecnologia.
/// Endpoint: GET https://remotive.com/api/remote-jobs?search={keyword}&limit=20
/// </summary>
public class RemotiveSearchService(
    IHttpClientFactory httpFactory,
    ILogger<RemotiveSearchService> logger) : IRemotiveSearchService
{
    // Sempre disponível — sem necessidade de configuração
    public bool IsConfigured => true;

    public async Task<List<JobResult>> SearchAsync(List<string> keywords, CancellationToken ct = default)
    {
        var client = httpFactory.CreateClient("Remotive");
        var results = new List<JobResult>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Busca pelos 3 primeiros keywords para evitar excesso de chamadas
        foreach (var keyword in keywords.Take(3))
        {
            try
            {
                var url = $"https://remotive.com/api/remote-jobs?search={Uri.EscapeDataString(keyword)}&limit=20";
                logger.LogInformation("Remotive search: {Keyword}", keyword);

                var response = await client.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(ct);
                var jobs = ParseResponse(json, keywords, seenUrls);
                results.AddRange(jobs);

                // Pequeno delay entre chamadas para não sobrecarregar a API
                if (keywords.IndexOf(keyword) < keywords.Take(3).Count() - 1)
                    await Task.Delay(300, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Remotive falhou para keyword '{Keyword}'", keyword);
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

            var title = job.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            var company = job.TryGetProperty("company_name", out var c) ? c.GetString() ?? "" : "";
            var category = job.TryGetProperty("category", out var cat) ? cat.GetString() ?? "" : "";
            var description = job.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
            var pubDateStr = job.TryGetProperty("publication_date", out var pd) ? pd.GetString() : null;

            var publishedAt = pubDateStr != null && DateTime.TryParse(pubDateStr, out var parsed)
                ? parsed.ToUniversalTime()
                : DateTime.UtcNow.AddHours(-new Random().Next(1, 12));

            var snippet = BuildSnippet(description, company, category);
            var displayTitle = company.Length > 0 ? $"{title} | {company}" : title;

            results.Add(new JobResult
            {
                Title = displayTitle,
                Snippet = snippet,
                Author = company,
                Url = jobUrl,
                PublishedAt = publishedAt,
                Keywords = string.Join(",", keywords),
                ResultType = "job"
            });
        }

        return results;
    }

    /// <summary>
    /// Remove tags HTML e extrai os primeiros 350 caracteres como snippet.
    /// </summary>
    private static string BuildSnippet(string html, string company, string category)
    {
        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = Regex.Replace(text, @"\s{2,}", " ").Trim();
        text = text.Replace("&amp;", "&").Replace("&lt;", "<")
                   .Replace("&gt;", ">").Replace("&nbsp;", " ")
                   .Replace("&#39;", "'").Replace("&quot;", "\"");

        var prefix = category.Length > 0 ? $"[{category}] " : "";
        var full = prefix + text;

        return full.Length <= 350 ? full : full[..347] + "...";
    }
}
