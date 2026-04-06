using JobRadar.Domain.Exceptions;

namespace JobRadar.Domain.ValueObjects;

/// <summary>
/// Value Object que encapsula e valida uma coleção de palavras-chave de busca.
/// Imutável — garante invariantes do domínio na criação.
/// </summary>
public sealed class Keywords : IEquatable<Keywords>
{
    public IReadOnlyList<string> Values { get; }

    /// <summary>Normaliza termos comuns para os usados pelos provedores.</summary>
    private static readonly IReadOnlyDictionary<string, string> NormalizationMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".net"]     = "dotnet",
            ["c#"]       = "csharp",
            ["node.js"]  = "nodejs",
            ["node"]     = "nodejs",
            ["vue.js"]   = "vue",
            ["react.js"] = "react",
            ["asp.net"]  = "aspnet",
            ["golang"]   = "go",
            ["k8s"]      = "kubernetes",
        };

    private Keywords(IReadOnlyList<string> values)
    {
        Values = values;
    }

    public static Keywords Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new DomainException("Informe ao menos uma palavra-chave.");

        var values = input
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(k => k.Length >= 2)
            .Select(k => NormalizationMap.TryGetValue(k, out var mapped) ? mapped : k.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList()
            .AsReadOnly();

        if (values.Count == 0)
            throw new DomainException("Nenhuma palavra-chave válida encontrada (mínimo 2 caracteres).");

        return new Keywords(values);
    }

    public string ToCacheKey() =>
        string.Join("|", Values.OrderBy(k => k));

    public string ToDisplay() =>
        string.Join(", ", Values);

    public override string ToString() => ToDisplay();

    // ─── Igualdade por valor (Value Object) ──────────────────
    public bool Equals(Keywords? other) =>
        other != null && ToCacheKey() == other.ToCacheKey();

    public override bool Equals(object? obj) => Equals(obj as Keywords);
    public override int GetHashCode() => ToCacheKey().GetHashCode();
}
