using System.Text.Json;
using System.Text.RegularExpressions;
using JobRadar.Application.Interfaces;
using JobRadar.Domain.Entities;
using JobRadar.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Providers;

/// <summary>
/// Provedor Jobicy.com — gratuito, sem API key.
/// Parallel (roda junto com Remotive).
/// Foco em vagas remotas internacionais de tecnologia.
/// API: https://jobicy.com/api/v2/remote-jobs?count=20&amp;tag={keyword}
/// </summary>
public class JobicyProvider(
    IHttpClientFactory httpFactory,
    ILogger<JobicyProvider> logger) : IJobProvider
{
    public string Name => "Jobicy";
    public ProviderMode Mode => ProviderMode.Parallel;
    public int Priority => 11;
    public bool IsConfigured => true;

    /// <summary>
    /// Reverso do mapa de normalização do Domain — Jobicy usa os nomes "bonitos" como tags.
    /// Ex: dotnet → .net, csharp → c#
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> TagMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["dotnet"]     = ".net",
            ["csharp"]     = "c#",
            ["nodejs"]     = "node.js",
            ["aspnet"]     = "asp.net",
            ["golang"]     = "go",
            ["kubernetes"] = "k8s",
            ["vue"]        = "vue.js",
            ["react"]      = "react",
        };

    public async Task<List<JobResult>> FetchAsync(Keywords keywords, CancellationToken ct = default)
    {
        var client   = httpFactory.CreateClient("Jobicy");
        var results  = new List<JobResult>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Busca por cada keyword individualmente (Jobicy filtra melhor por tag)
        foreach (var kw in keywords.Values.Take(3))
        {
            var tag = TagMap.TryGetValue(kw, out var mapped) ? mapped : kw;

            try
            {
                var url = $"https://jobicy.com/api/v2/remote-jobs?count=20&tag={Uri.EscapeDataString(tag)}";
                logger.LogInformation("Jobicy: '{Tag}'", tag);

                var response = await client.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(ct);
                results.AddRange(ParseResponse(json, seenUrls));

                await Task.Delay(300, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Jobicy falhou para '{Tag}'", tag);
            }
        }

        // Busca geral sem tag para capturar mais vagas
        if (results.Count < 5)
        {
            try
            {
                var combined = string.Join(" ", keywords.Values.Take(2).Select(k =>
                    TagMap.TryGetValue(k, out var m) ? m : k));
                var url = $"https://jobicy.com/api/v2/remote-jobs?count=20&tag={Uri.EscapeDataString(combined)}";
                logger.LogInformation("Jobicy (combinado): '{Term}'", combined);

                var response = await client.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(ct);
                results.AddRange(ParseResponse(json, seenUrls));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Jobicy falhou na busca combinada");
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
            var jobUrl  = job.TryGetProperty("url", out var u)           ? u.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(jobUrl) || !seenUrls.Add(jobUrl)) continue;

            var title   = job.TryGetProperty("jobTitle", out var t)      ? t.GetString() ?? "" : "";
            var company = job.TryGetProperty("companyName", out var c)   ? c.GetString() ?? "" : "";
            var geo     = job.TryGetProperty("jobGeo", out var g)        ? g.GetString() ?? "" : "";
            var desc    = job.TryGetProperty("jobDescription", out var d) ? d.GetString() ?? "" : "";
            var pubDate = job.TryGetProperty("pubDate", out var pd)      ? pd.GetString() : null;

            var publishedAt = pubDate != null && DateTime.TryParse(pubDate, out var parsed)
                ? parsed.ToUniversalTime()
                : DateTime.UtcNow.AddHours(-Random.Shared.Next(1, 72));

            var snippet      = BuildSnippet(desc, geo);
            var displayTitle = company.Length > 0 ? $"{title} | {company}" : title;

            results.Add(JobResult.Create(displayTitle, snippet, jobUrl, publishedAt, company));
        }

        return results;
    }

    private static string BuildSnippet(string html, string geo)
    {
        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = Regex.Replace(text, @"\s{2,}", " ").Trim();
        text = text.Replace("&amp;", "&").Replace("&nbsp;", " ")
                   .Replace("&#39;", "'").Replace("&quot;", "\"");

        var prefix = geo.Length > 0 ? $"[{geo}] " : "";
        var full   = prefix + text;
        return full.Length <= 400 ? full : full[..397] + "...";
    }
}
