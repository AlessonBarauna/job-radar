using JobRadar.API.Models;

namespace JobRadar.API.Services;

/// <summary>
/// Serviço mock para desenvolvimento sem API keys.
/// Gera dados realistas de vagas/posts do LinkedIn.
/// Usado automaticamente quando Bing e Google não estão configurados.
/// </summary>
public static class MockSearchService
{
    private static readonly string[] Companies =
    [
        "Nubank", "iFood", "Mercado Livre", "PicPay", "Totvs",
        "Creditas", "QuintoAndar", "Stone", "Pagar.me", "Loft",
        "Conta Simples", "Warren", "Nomad", "Stark Bank", "Dock"
    ];

    private static readonly string[] Roles =
    [
        "Desenvolvedor(a) Backend", "Engenheiro(a) de Software Pleno",
        "Software Engineer Senior", "Tech Lead", "Desenvolvedor(a) Full Stack",
        "Arquiteto(a) de Software", "Backend Developer", "Platform Engineer"
    ];

    private static readonly string[] Locations =
    [
        "São Paulo, SP", "Remoto", "Híbrido - SP", "Rio de Janeiro, RJ",
        "100% Remoto - Brasil", "Florianópolis, SC"
    ];

    public static List<JobResult> Generate(List<string> keywords, int count = 12)
    {
        var results = new List<JobResult>();
        var rng = new Random();
        var now = DateTime.UtcNow;

        for (int i = 0; i < count; i++)
        {
            var company = Companies[rng.Next(Companies.Length)];
            var role = Roles[rng.Next(Roles.Length)];
            var location = Locations[rng.Next(Locations.Length)];
            var hoursAgo = rng.Next(1, 23);

            // Injeta uma keyword no título para simular match
            var kwInTitle = keywords.Count > 0 ? keywords[rng.Next(keywords.Count)] : "";
            var title = $"{role} {kwInTitle} | {company}".Trim();

            var snippet = BuildSnippet(company, role, location, keywords, rng);
            var jobId = rng.NextInt64(3000000000L, 4000000000L);
            var url = $"https://www.linkedin.com/jobs/view/{jobId}/";

            results.Add(new JobResult
            {
                Id = i + 1,
                Title = title,
                Snippet = snippet,
                Author = company,
                Url = url,
                PublishedAt = now.AddHours(-hoursAgo),
                Keywords = string.Join(",", keywords),
                ResultType = rng.Next(5) == 0 ? "post" : "job", // 80% vagas, 20% posts
                CollectedAt = now
            });
        }

        return results;
    }

    private static string BuildSnippet(string company, string role, string location,
        List<string> keywords, Random rng)
    {
        var stacks = new[] { ".NET 8", "C#", "AWS", "Azure", "Docker", "Kubernetes",
            "PostgreSQL", "Redis", "RabbitMQ", "Angular", "React", "Microserviços" };

        var selectedStack = stacks.OrderBy(_ => rng.Next()).Take(4).ToList();
        if (keywords.Count > 0)
            selectedStack.Add(keywords[rng.Next(keywords.Count)].ToUpper());

        var stackStr = string.Join(", ", selectedStack.Distinct());

        return $"{company} está contratando {role} para trabalhar {location}. " +
               $"Stack: {stackStr}. " +
               $"Benefícios: PLR, stock options, plano de saúde, home office. " +
               $"Experiência mínima: {rng.Next(2, 6)} anos. " +
               "Venha fazer parte do nosso time! Candidate-se pelo link.";
    }
}
