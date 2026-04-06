using System.Diagnostics;
using System.Text;
using JobRadar.Application.DTOs;
using JobRadar.Application.Interfaces;
using JobRadar.Domain.ValueObjects;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace JobRadar.Application.Services;

/// <summary>
/// Orquestra a geração de relatório inteligente de mercado:
///   1. Busca vagas reais nos providers configurados
///   2. Monta contexto rico com os dados encontrados
///   3. Chama o LLM para gerar análise em markdown
/// </summary>
public class ReportService(
    IEnumerable<IJobProvider> providers,
    ILlmService llm,
    IMemoryCache cache,
    ILogger<ReportService> logger) : IReportService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    private static readonly string SystemPrompt = """
        Você é um especialista em mercado de trabalho de tecnologia no Brasil.
        Sua missão é analisar dados de vagas reais e gerar um relatório completo e útil
        para desenvolvedores que buscam emprego.

        O relatório deve ser em português brasileiro, formatado em Markdown, e incluir:
        - Panorama geral do mercado para as tecnologias buscadas
        - Empresas que mais contratam (com portais de carreiras quando souber)
        - Faixas salariais estimadas por nível (Júnior, Pleno, Sênior)
        - Stack técnico mais exigido
        - Dicas para processo seletivo
        - Portais de busca recomendados

        Use os dados de vagas reais fornecidos como base. Seja específico, prático e atual.
        Não invente dados que não estejam no contexto — indique quando for estimativa.
        """;

    public async Task<ReportResponseDto> GenerateAsync(string rawKeywords, CancellationToken ct = default)
    {
        var keywords = Keywords.Parse(rawKeywords);
        var cacheKey = $"report:{keywords.ToCacheKey()}";
        var sw       = Stopwatch.StartNew();

        if (cache.TryGetValue(cacheKey, out ReportResponseDto? cached) && cached != null)
        {
            logger.LogInformation("Report cache hit: {Keywords}", keywords.ToDisplay());
            cached.FromCache = true;
            return cached;
        }

        // 1. Busca vagas reais (paralelo — Gupy + Jobicy + Remotive)
        var jobListings = await FetchJobsAsync(keywords, ct);
        logger.LogInformation("Report: {Count} vagas coletadas para '{Keywords}'",
            jobListings.Count, keywords.ToDisplay());

        // 2. Monta o prompt com contexto das vagas
        var userPrompt = BuildUserPrompt(keywords, jobListings);

        // 3. Chama o LLM
        var markdown = await llm.CompleteAsync(SystemPrompt, userPrompt, ct);

        sw.Stop();

        var response = new ReportResponseDto
        {
            Keywords    = keywords.ToDisplay(),
            Markdown    = markdown,
            GeneratedAt = DateTime.UtcNow,
            ElapsedMs   = sw.ElapsedMilliseconds,
            FromCache   = false
        };

        cache.Set(cacheKey, response, CacheTtl);
        return response;
    }

    // ─── Coleta vagas dos providers paralelos ────────────────────────────

    private async Task<List<(string Title, string Company, string Url, string Snippet, string Workplace)>>
        FetchJobsAsync(Keywords keywords, CancellationToken ct)
    {
        var parallelProviders = providers
            .Where(p => p.Mode == ProviderMode.Parallel && p.IsConfigured)
            .ToList();

        if (parallelProviders.Count == 0) return [];

        var tasks = parallelProviders.Select(p => FetchSafeAsync(p, keywords, ct));
        var allResults = await Task.WhenAll(tasks);

        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return allResults
            .SelectMany(r => r)
            .Where(r => seenUrls.Add(r.Url))
            .Take(30) // limita o contexto enviado ao LLM
            .Select(r => (
                Title:     r.Title,
                Company:   r.Author ?? "",
                Url:       r.Url,
                Snippet:   r.Snippet.Length > 200 ? r.Snippet[..200] : r.Snippet,
                Workplace: InferWorkplace(r.Snippet)
            ))
            .ToList();
    }

    private async Task<List<Domain.Entities.JobResult>> FetchSafeAsync(
        IJobProvider provider, Keywords keywords, CancellationToken ct)
    {
        try { return await provider.FetchAsync(keywords, ct); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Provider '{Name}' falhou no report.", provider.Name);
            return [];
        }
    }

    // ─── Prompt ──────────────────────────────────────────────────────────

    private static string BuildUserPrompt(
        Keywords keywords,
        List<(string Title, string Company, string Url, string Snippet, string Workplace)> jobs)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## Busca: {keywords.ToDisplay()}");
        sb.AppendLine($"Data: {DateTime.UtcNow:dd/MM/yyyy}");
        sb.AppendLine();

        if (jobs.Count > 0)
        {
            sb.AppendLine($"### {jobs.Count} vagas reais encontradas agora:");
            sb.AppendLine();

            foreach (var job in jobs)
            {
                sb.AppendLine($"**{job.Title}**");
                if (!string.IsNullOrEmpty(job.Company))
                    sb.AppendLine($"Empresa: {job.Company}");
                if (!string.IsNullOrEmpty(job.Workplace))
                    sb.AppendLine($"Modalidade: {job.Workplace}");
                sb.AppendLine($"Link: {job.Url}");
                if (!string.IsNullOrEmpty(job.Snippet))
                    sb.AppendLine($"Descrição: {job.Snippet}");
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("Nenhuma vaga encontrada nos providers no momento.");
            sb.AppendLine("Use seu conhecimento de mercado para gerar o relatório.");
        }

        sb.AppendLine("---");
        sb.AppendLine("Gere um relatório completo de mercado para essas tecnologias no Brasil.");

        return sb.ToString();
    }

    private static string InferWorkplace(string snippet)
    {
        if (snippet.Contains("Remoto",    StringComparison.OrdinalIgnoreCase) ||
            snippet.Contains("remote",    StringComparison.OrdinalIgnoreCase) ||
            snippet.Contains("home office", StringComparison.OrdinalIgnoreCase))
            return "Remoto";

        if (snippet.Contains("Híbrido",  StringComparison.OrdinalIgnoreCase) ||
            snippet.Contains("hybrid",   StringComparison.OrdinalIgnoreCase))
            return "Híbrido";

        if (snippet.Contains("Presencial", StringComparison.OrdinalIgnoreCase))
            return "Presencial";

        return "";
    }
}
