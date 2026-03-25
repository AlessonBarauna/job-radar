using JobRadar.API.Models;

namespace JobRadar.API.Services.Interfaces;

/// <summary>
/// Calcula score de relevância e destaca palavras-chave.
/// </summary>
public interface IRelevanceService
{
    /// <summary>
    /// Calcula score de 0-100 baseado em: match de keywords (título/snippet) + recência.
    /// </summary>
    int CalculateScore(JobResult result, List<string> keywords);

    /// <summary>
    /// Retorna lista de keywords encontradas no título + snippet.
    /// </summary>
    List<string> FindMatchedKeywords(JobResult result, List<string> keywords);
}
