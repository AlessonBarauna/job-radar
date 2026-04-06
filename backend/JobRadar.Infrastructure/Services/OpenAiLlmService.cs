using System.Net.Http.Json;
using System.Text.Json;
using JobRadar.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Services;

/// <summary>
/// Implementação do ILlmService usando OpenAI Chat Completions API.
/// Modelo padrão: gpt-4o-mini (custo ~$0.002 por relatório).
/// Configuração: Llm:OpenAiApiKey (obrigatório), Llm:Model (opcional).
///
/// Para trocar para Anthropic ou Gemini: implemente ILlmService e altere o registro no Program.cs.
/// </summary>
public class OpenAiLlmService(
    IHttpClientFactory httpFactory,
    IConfiguration config,
    ILogger<OpenAiLlmService> logger) : ILlmService
{
    private const string Endpoint = "https://api.openai.com/v1/chat/completions";

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var apiKey = config["Llm:OpenAiApiKey"] ?? "";
        var model  = config["Llm:Model"] ?? "gpt-4o-mini";

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "LLM não configurado. Adicione 'Llm:OpenAiApiKey' em appsettings.Development.json.");

        var client = httpFactory.CreateClient("OpenAi");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        var body = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user",   content = userPrompt   }
            },
            temperature = 0.7,
            max_tokens  = 4000
        };

        logger.LogInformation("LLM ({Model}): gerando relatório...", model);

        var response = await client.PostAsJsonAsync(Endpoint, body, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
    }
}
