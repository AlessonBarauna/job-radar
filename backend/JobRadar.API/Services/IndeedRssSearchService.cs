using System.Text.RegularExpressions;
using System.Xml.Linq;
using JobRadar.API.Models;
using JobRadar.API.Services.Interfaces;

namespace JobRadar.API.Services;

/// <summary>
/// Busca vagas brasileiras via Indeed Brasil RSS Feed — gratuito, sem API key.
/// Endpoint: https://br.indeed.com/rss?q={keywords}&l=Brasil&fromage=7
/// </summary>
public class IndeedRssSearchService(
    IHttpClientFactory httpFactory,
    ILogger<IndeedRssSearchService> logger) : IIndeedRssSearchService
{
    public bool IsConfigured => true;

    public async Task<List<JobResult>> SearchAsync(List<string> keywords, CancellationToken ct = default)
    {
        var client = httpFactory.CreateClient("IndeedRss");
        var results = new List<JobResult>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Indeed RSS aceita query com múltiplas palavras (AND implícito)
        // Fazemos uma busca por keyword para maximizar resultados
        foreach (var keyword in keywords.Take(3))
        {
            try
            {
                // fromage=7 = últimos 7 dias
                var query = Uri.EscapeDataString(keyword);
                var url = $"https://br.indeed.com/rss?q={query}&l=Brasil&fromage=7&sort=date";

                logger.LogInformation("Indeed RSS search: {Keyword}", keyword);

                var response = await client.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();

                var xml = await response.Content.ReadAsStringAsync(ct);
                var parsed = ParseRss(xml, keywords, seenUrls);
                results.AddRange(parsed);

                if (keywords.IndexOf(keyword) < keywords.Take(3).Count() - 1)
                    await Task.Delay(500, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Indeed RSS falhou para keyword '{Keyword}'", keyword);
            }
        }

        return results;
    }

    private static List<JobResult> ParseRss(string xml, List<string> keywords, HashSet<string> seenUrls)
    {
        var results = new List<JobResult>();

        try
        {
            var doc = XDocument.Parse(xml);
            var items = doc.Descendants("item");

            foreach (var item in items)
            {
                var title = item.Element("title")?.Value ?? "";
                var link = item.Element("link")?.Value ?? "";
                var description = item.Element("description")?.Value ?? "";
                var pubDateStr = item.Element("pubDate")?.Value;
                var source = item.Element("source")?.Value ?? "";

                if (string.IsNullOrEmpty(link) || !seenUrls.Add(link))
                    continue;

                // Título do Indeed vem como "Cargo - Empresa"
                var (jobTitle, company) = SplitTitleAndCompany(title, source);

                var publishedAt = pubDateStr != null && DateTime.TryParse(pubDateStr, out var parsed)
                    ? parsed.ToUniversalTime()
                    : DateTime.UtcNow.AddHours(-new Random().Next(1, 48));

                var snippet = BuildSnippet(description);

                results.Add(new JobResult
                {
                    Title = company.Length > 0 ? $"{jobTitle} | {company}" : jobTitle,
                    Snippet = snippet,
                    Author = company,
                    Url = link,
                    PublishedAt = publishedAt,
                    Keywords = string.Join(",", keywords),
                    ResultType = "job"
                });
            }
        }
        catch (Exception)
        {
            // XML malformado — ignora silenciosamente
        }

        return results;
    }

    private static (string title, string company) SplitTitleAndCompany(string rawTitle, string source)
    {
        // Padrão Indeed: "Título do Cargo - Nome da Empresa"
        var separators = new[] { " - ", " | " };
        foreach (var sep in separators)
        {
            var idx = rawTitle.LastIndexOf(sep, StringComparison.Ordinal);
            if (idx > 0)
                return (rawTitle[..idx].Trim(), rawTitle[(idx + sep.Length)..].Trim());
        }

        return (rawTitle.Trim(), source.Trim());
    }

    private static string BuildSnippet(string html)
    {
        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = Regex.Replace(text, @"\s{2,}", " ").Trim();
        text = text.Replace("&amp;", "&").Replace("&lt;", "<")
                   .Replace("&gt;", ">").Replace("&nbsp;", " ")
                   .Replace("&#39;", "'").Replace("&quot;", "\"");

        return text.Length <= 400 ? text : text[..397] + "...";
    }
}
