namespace JobRadar.Application.Interfaces;

/// <summary>
/// Abstração para chamadas a modelos de linguagem (OpenAI, Anthropic, Gemini).
/// A implementação concreta fica em Infrastructure.
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// Envia um prompt e retorna o texto gerado pelo modelo.
    /// </summary>
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}
