using JobRadar.Application.Interfaces;
using JobRadar.Domain.Entities;
using JobRadar.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Providers;

/// <summary>
/// Provedor Mock — fallback de desenvolvimento.
/// Gera dados realistas quando nenhum provedor real está disponível.
/// Sequential, Priority int.MaxValue (último recurso).
/// </summary>
public class MockProvider(ILogger<MockProvider> logger) : IJobProvider
{
    public string Name => "Mock";
    public ProviderMode Mode => ProviderMode.Sequential;
    public int Priority => int.MaxValue;
    public bool IsConfigured => true;

    private static readonly string[] Companies =
    [
        "Nubank", "iFood", "Mercado Livre", "PicPay", "Totvs",
        "Creditas", "QuintoAndar", "Stone", "Pagar.me", "Dock"
    ];

    private static readonly string[] Roles =
    [
        "Desenvolvedor(a) Backend", "Software Engineer Pleno",
        "Software Engineer Senior", "Tech Lead", "Full Stack Developer",
        "Arquiteto(a) de Software", "Platform Engineer"
    ];

    private static readonly string[] Locations =
    [
        "São Paulo, SP", "Remoto", "Híbrido - SP",
        "Rio de Janeiro, RJ", "100% Remoto - Brasil"
    ];

    public Task<List<JobResult>> FetchAsync(Keywords keywords, CancellationToken ct = default)
    {
        logger.LogWarning("Usando Mock — configure um provedor real em appsettings.");
        var results = Generate(keywords.Values, count: 12);
        return Task.FromResult(results);
    }

    private static List<JobResult> Generate(IReadOnlyList<string> keywords, int count)
    {
        var results = new List<JobResult>();
        var now     = DateTime.UtcNow;

        for (var i = 0; i < count; i++)
        {
            var company  = Companies[Random.Shared.Next(Companies.Length)];
            var role     = Roles[Random.Shared.Next(Roles.Length)];
            var location = Locations[Random.Shared.Next(Locations.Length)];
            var kw       = keywords.Count > 0 ? keywords[Random.Shared.Next(keywords.Count)] : "";
            var hoursAgo = Random.Shared.Next(1, 23);
            var jobId    = Random.Shared.NextInt64(3_000_000_000L, 4_000_000_000L);

            var title   = $"[DEMO] {role} {kw} | {company}".Trim();
            var snippet = $"⚠️ Dados de demonstração — configure um provedor real (Remotive, Jooble, Bing) em appsettings.json para ver vagas reais. | {BuildSnippet(company, role, location, keywords)}";
            // URL aponta para busca real no LinkedIn (não um ID falso)
            var kwSearch = Uri.EscapeDataString(kw.Length > 0 ? kw : string.Join(" ", keywords.Take(2)));
            var url      = $"https://www.linkedin.com/jobs/search/?keywords={kwSearch}&location=Brasil";

            results.Add(JobResult.Create(
                title, snippet, url,
                now.AddHours(-hoursAgo),
                author: company,
                resultType: Random.Shared.Next(5) == 0 ? "post" : "job"));
        }

        return results;
    }

    private static string BuildSnippet(string company, string role, string location,
        IReadOnlyList<string> keywords)
    {
        var stacks = new[] { ".NET 8", "C#", "AWS", "Azure", "Docker",
                             "PostgreSQL", "Redis", "Angular", "React" };
        var stack  = stacks.OrderBy(_ => Random.Shared.Next()).Take(4).ToList();
        if (keywords.Count > 0) stack.Add(keywords[Random.Shared.Next(keywords.Count)].ToUpper());

        return $"{company} contrata {role} para {location}. " +
               $"Stack: {string.Join(", ", stack.Distinct())}. " +
               $"Benefícios: PLR, stock options, plano de saúde. " +
               $"Experiência: {Random.Shared.Next(2, 6)} anos. Candidate-se!";
    }
}
