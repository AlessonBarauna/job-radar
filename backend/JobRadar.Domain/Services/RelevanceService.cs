using JobRadar.Domain.Entities;
using JobRadar.Domain.ValueObjects;

namespace JobRadar.Domain.Services;

/// <summary>
/// Implementação do Domain Service de relevância.
/// Score = matches no título (×3) + matches no snippet (×1) + bônus de recência.
/// Normalizado para 0–100.
/// </summary>
public class RelevanceService : IRelevanceService
{
    public int CalculateScore(JobResult result, Keywords keywords)
    {
        if (keywords.Values.Count == 0) return 50;

        double score = 0;
        double maxPossible = (keywords.Values.Count * 3) + keywords.Values.Count + 30;

        var title   = result.Title.ToLowerInvariant();
        var snippet = result.Snippet.ToLowerInvariant();

        foreach (var kw in keywords.Values)
        {
            var kwLower = kw.ToLowerInvariant().Trim();
            if (title.Contains(kwLower))   score += 3;
            if (snippet.Contains(kwLower)) score += 1;
        }

        // Bônus de recência
        var age = DateTime.UtcNow - result.PublishedAt;
        score += age.TotalHours switch
        {
            <= 1  => 30,
            <= 3  => 25,
            <= 6  => 20,
            <= 12 => 15,
            <= 18 => 10,
            _     => 5
        };

        var normalized = (int)Math.Round((score / maxPossible) * 100);
        return Math.Clamp(normalized, 1, 100);
    }

    public IReadOnlyList<string> FindMatchedKeywords(JobResult result, Keywords keywords)
    {
        var content = $"{result.Title} {result.Snippet}".ToLowerInvariant();

        return keywords.Values
            .Where(kw => !string.IsNullOrWhiteSpace(kw) && content.Contains(kw.ToLowerInvariant()))
            .Select(kw => kw.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }
}
