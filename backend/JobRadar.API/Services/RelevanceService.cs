using JobRadar.API.Models;
using JobRadar.API.Services.Interfaces;

namespace JobRadar.API.Services;

/// <summary>
/// Calcula relevância baseado em:
/// - Match de keywords no título (peso 3x)
/// - Match de keywords no snippet (peso 1x)
/// - Bônus de recência (quanto mais novo, maior o score)
/// Score final normalizado para 0-100.
/// </summary>
public class RelevanceService : IRelevanceService
{
    public int CalculateScore(JobResult result, List<string> keywords)
    {
        if (keywords.Count == 0) return 50;

        double score = 0;
        double maxPossible = (keywords.Count * 3) + keywords.Count + 30; // title + snippet + recency

        var title = result.Title.ToLowerInvariant();
        var snippet = result.Snippet.ToLowerInvariant();

        foreach (var kw in keywords)
        {
            var kwLower = kw.ToLowerInvariant().Trim();
            if (string.IsNullOrEmpty(kwLower)) continue;

            // Título vale 3x mais que snippet
            if (title.Contains(kwLower)) score += 3;
            if (snippet.Contains(kwLower)) score += 1;
        }

        // Bônus de recência
        var age = DateTime.UtcNow - result.PublishedAt;
        score += age.TotalHours switch
        {
            <= 1 => 30,
            <= 3 => 25,
            <= 6 => 20,
            <= 12 => 15,
            <= 18 => 10,
            _ => 5
        };

        // Normaliza para 0-100
        var normalized = (int)Math.Round((score / maxPossible) * 100);
        return Math.Clamp(normalized, 1, 100);
    }

    public List<string> FindMatchedKeywords(JobResult result, List<string> keywords)
    {
        var content = $"{result.Title} {result.Snippet}".ToLowerInvariant();

        return keywords
            .Where(kw => !string.IsNullOrWhiteSpace(kw) && content.Contains(kw.ToLowerInvariant().Trim()))
            .Select(kw => kw.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
