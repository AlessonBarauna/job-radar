using JobRadar.Domain.Entities;
using JobRadar.Domain.ValueObjects;

namespace JobRadar.Application.Interfaces;

/// <summary>
/// Modo de execução do provedor dentro da estratégia de busca.
/// </summary>
public enum ProviderMode
{
    /// <summary>Executado em sequência — para no primeiro que retornar resultados.</summary>
    Sequential,

    /// <summary>Executado em paralelo com outros provedores do mesmo modo — resultados combinados.</summary>
    Parallel
}

/// <summary>
/// Interface unificada para todos os provedores de busca de vagas.
///
/// Para adicionar um novo provedor:
///   1. Crie uma classe em Infrastructure/Providers implementando IJobProvider.
///   2. Registre em Program.cs com AddScoped&lt;IJobProvider, NomeDoProvider&gt;().
///   3. Nenhuma outra mudança é necessária — JobSearchService descobre automaticamente.
/// </summary>
public interface IJobProvider
{
    /// <summary>Nome do provedor exibido ao usuário (ex: "Remotive", "Bing").</summary>
    string Name { get; }

    /// <summary>Indica se o provedor está disponível (chave configurada, sem erros de init).</summary>
    bool IsConfigured { get; }

    /// <summary>Modo de execução — Sequential (pago) ou Parallel (gratuito).</summary>
    ProviderMode Mode { get; }

    /// <summary>Ordem de tentativa entre provedores Sequential. Menor = maior prioridade.</summary>
    int Priority { get; }

    /// <summary>Executa a busca e retorna os resultados brutos (sem score).</summary>
    Task<List<JobResult>> FetchAsync(Keywords keywords, CancellationToken ct = default);
}
