using System.Text.Json;
using System.Text.RegularExpressions;
using JobRadar.Application.Interfaces;
using JobRadar.Domain.Entities;
using JobRadar.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Providers;

/// <summary>
/// Provedor Gupy — gratuito, sem API key.
/// Plataforma de RH usada por centenas de empresas brasileiras:
/// iFood, Nubank, Stone, Creditas, Totvs, QuintoAndar, Banco Inter e outras.
/// Parallel, Priority 12. Foco 100% no mercado brasileiro.
/// API: https://portal.api.gupy.io/api/job?name={keywords}&amp;limit=20
/// </summary>
public class GupyProvider(
    IHttpClientFactory httpFactory,
    ILogger<GupyProvider> logger) : IJobProvider
{
    public string Name => "Gupy";
    public ProviderMode Mode => ProviderMode.Parallel;
    public int Priority => 12;
    public bool IsConfigured => true;

    /// <summary>
    /// Gupy faz busca textual em título e descrição — usa termos legíveis.
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
        var client   = httpFactory.CreateClient("Gupy");
        var results  = new List<JobResult>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var displayTerms = keywords.Values
            .Select(k => DisplayMap.TryGetValue(k, out var m) ? m : k)
            .ToList();

        // Busca combinada — Gupy busca em título e descrição
        var combined = string.Join(" ", displayTerms);
        try
        {
            results.AddRange(await SearchAsync(client, combined, seenUrls, ct));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Gupy falhou para '{Term}'", combined);
            return results;
        }

        // Busca individual pelas primeiras 2 keywords para ampliar resultados
        foreach (var term in displayTerms.Take(2))
        {
            if (results.Count >= 20) break;
            try
            {
                results.AddRange(await SearchAsync(client, term, seenUrls, ct));
                await Task.Delay(300, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Gupy falhou para '{Term}'", term);
            }
        }

        return results;
    }

    private async Task<List<JobResult>> SearchAsync(
        HttpClient client, string term, HashSet<string> seenUrls, CancellationToken ct)
    {
        var url = $"https://portal.api.gupy.io/api/job" +
                  $"?name={Uri.EscapeDataString(term)}" +
                  $"&limit=20";

        logger.LogInformation("Gupy BR: '{Term}'", term);

        var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseResponse(json, seenUrls);
    }

    private static List<JobResult> ParseResponse(string json, HashSet<string> seenUrls)
    {
        var results = new List<JobResult>();
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("data", out var data)) return results;

        foreach (var item in data.EnumerateArray())
        {
            var jobUrl = item.TryGetProperty("jobUrl", out var u) ? u.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(jobUrl) || !seenUrls.Add(jobUrl)) continue;

            var title       = item.TryGetProperty("name", out var t)            ? t.GetString() ?? "" : "";
            var company     = item.TryGetProperty("careerPageName", out var c)   ? c.GetString() ?? "" : "";
            var description = item.TryGetProperty("description", out var d)      ? d.GetString() ?? "" : "";
            var pubDate     = item.TryGetProperty("publishedDate", out var pd)   ? pd.GetString() : null;
            var city        = item.TryGetProperty("city", out var ci)            ? ci.GetString() ?? "" : "";
            var state       = item.TryGetProperty("state", out var st)           ? st.GetString() ?? "" : "";
            var workplace   = item.TryGetProperty("workplaceType", out var wt)   ? wt.GetString() ?? "" : "";

            var publishedAt = pubDate != null && DateTime.TryParse(pubDate, out var parsed)
                ? parsed.ToUniversalTime()
                : DateTime.UtcNow.AddHours(-Random.Shared.Next(1, 72));

            var location    = BuildLocation(city, state, workplace);
            var snippet     = BuildSnippet(description, location);
            var displayTitle = company.Length > 0 && !company.Equals("Confidencial", StringComparison.OrdinalIgnoreCase)
                ? $"{title} | {company}"
                : title;

            results.Add(JobResult.Create(displayTitle, snippet, jobUrl, publishedAt, company));
        }

        return results;
    }

    private static string BuildLocation(string city, string state, string workplace)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(city) && !string.IsNullOrEmpty(state))
            parts.Add($"{city}, {state}");
        else if (!string.IsNullOrEmpty(city))
            parts.Add(city);

        parts.Add(workplace switch
        {
            "remote"  => "Remoto",
            "hybrid"  => "Híbrido",
            "on-site" => "Presencial",
            _         => ""
        });

        return string.Join(" · ", parts.Where(p => p.Length > 0));
    }

    private static string BuildSnippet(string text, string location)
    {
        // Remove HTML se vier com tags
        text = Regex.Replace(text, "<[^>]+>", " ");
        text = Regex.Replace(text, @"\s{2,}", " ").Trim();
        text = text.Replace("&amp;", "&").Replace("&nbsp;", " ")
                   .Replace("&#39;", "'").Replace("&quot;", "\"");

        var prefix = location.Length > 0 ? $"[{location}] " : "";
        var full   = prefix + text;
        return full.Length <= 400 ? full : full[..397] + "...";
    }
}
