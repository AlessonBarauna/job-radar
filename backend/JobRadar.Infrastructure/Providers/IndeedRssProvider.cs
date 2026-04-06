using System.Text.RegularExpressions;
using System.Xml.Linq;
using JobRadar.Application.Interfaces;
using JobRadar.Domain.Entities;
using JobRadar.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Providers;

/// <summary>
/// Provedor Indeed Brasil RSS Feed — gratuito, sem API key.
/// Parallel (roda junto com Remotive).
/// Foco no mercado de trabalho brasileiro.
/// </summary>
public class IndeedRssProvider(
    IHttpClientFactory httpFactory,
    ILogger<IndeedRssProvider> logger) : IJobProvider
{
    public string Name => "Indeed";
    public ProviderMode Mode => ProviderMode.Parallel;
    public int Priority => 11;
    public bool IsConfigured => true;

    public async Task<List<JobResult>> FetchAsync(Keywords keywords, CancellationToken ct = default)
    {
        var client   = httpFactory.CreateClient("IndeedRss");
        var results  = new List<JobResult>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var keyword in keywords.Values.Take(3))
        {
            try
            {
                var url = $"https://br.indeed.com/rss?q={Uri.EscapeDataString(keyword)}&l=Brasil&fromage=7&sort=date";
                logger.LogInformation("Indeed RSS: '{Keyword}'", keyword);

                var response = await client.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();

                var xml = await response.Content.ReadAsStringAsync(ct);
                results.AddRange(ParseRss(xml, seenUrls));

                await Task.Delay(500, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Indeed RSS falhou para '{Keyword}'", keyword);
            }
        }

        return results;
    }

    private static List<JobResult> ParseRss(string xml, HashSet<string> seenUrls)
    {
        var results = new List<JobResult>();
        try
        {
            var items = XDocument.Parse(xml).Descendants("item");

            foreach (var item in items)
            {
                var link      = item.Element("link")?.Value ?? "";
                var rawTitle  = item.Element("title")?.Value ?? "";
                var desc      = item.Element("description")?.Value ?? "";
                var pubDate   = item.Element("pubDate")?.Value;
                var source    = item.Element("source")?.Value ?? "";

                if (string.IsNullOrEmpty(link) || !seenUrls.Add(link)) continue;

                var (title, company) = SplitTitle(rawTitle, source);

                var publishedAt = pubDate != null && DateTime.TryParse(pubDate, out var dt)
                    ? dt.ToUniversalTime()
                    : DateTime.UtcNow.AddHours(-Random.Shared.Next(1, 48));

                var snippet = StripHtml(desc);
                var display = company.Length > 0 ? $"{title} | {company}" : title;

                results.Add(JobResult.Create(display, snippet, link, publishedAt, company));
            }
        }
        catch { /* XML malformado — ignora */ }

        return results;
    }

    private static (string title, string company) SplitTitle(string raw, string source)
    {
        foreach (var sep in new[] { " - ", " | " })
        {
            var idx = raw.LastIndexOf(sep, StringComparison.Ordinal);
            if (idx > 0) return (raw[..idx].Trim(), raw[(idx + sep.Length)..].Trim());
        }
        return (raw.Trim(), source.Trim());
    }

    private static string StripHtml(string html)
    {
        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = Regex.Replace(text, @"\s{2,}", " ").Trim();
        text = text.Replace("&amp;", "&").Replace("&nbsp;", " ")
                   .Replace("&#39;", "'").Replace("&quot;", "\"");
        return text.Length <= 400 ? text : text[..397] + "...";
    }
}
