using JobRadar.Domain.Entities;
using JobRadar.Domain.ValueObjects;

namespace JobRadar.Domain.Services;

/// <summary>
/// Domain Service — calcula o score de relevância de um resultado
/// em relação ao conjunto de keywords buscado.
/// </summary>
public interface IRelevanceService
{
    int CalculateScore(JobResult result, Keywords keywords);
    IReadOnlyList<string> FindMatchedKeywords(JobResult result, Keywords keywords);
}
