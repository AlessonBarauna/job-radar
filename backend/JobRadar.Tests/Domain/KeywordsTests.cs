using FluentAssertions;
using JobRadar.Domain.Exceptions;
using JobRadar.Domain.ValueObjects;

namespace JobRadar.Tests.Domain;

public class KeywordsTests
{
    // ─── Parse válido ────────────────────────────────────────────────────

    [Fact]
    public void Parse_ComKeywordsValidas_RetornaListaNormalizada()
    {
        var kw = Keywords.Parse("dotnet, aws, angular");

        kw.Values.Should().BeEquivalentTo(["dotnet", "aws", "angular"]);
    }

    [Theory]
    [InlineData(".net",    "dotnet")]
    [InlineData("c#",     "csharp")]
    [InlineData("node.js","nodejs")]
    [InlineData("vue.js", "vue")]
    [InlineData("golang", "go")]
    [InlineData("k8s",    "kubernetes")]
    public void Parse_NormalizaTermosConhecidos(string input, string expected)
    {
        var kw = Keywords.Parse(input);

        kw.Values.Should().Contain(expected);
    }

    [Fact]
    public void Parse_RemoveDuplicatas()
    {
        var kw = Keywords.Parse("dotnet, dotnet, DOTNET");

        kw.Values.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_LimitaA10Keywords()
    {
        var input = string.Join(",", Enumerable.Range(1, 15).Select(i => $"kw{i}"));
        var kw = Keywords.Parse(input);

        kw.Values.Should().HaveCount(10);
    }

    [Fact]
    public void Parse_SeparadoresDiferentes_FuncionaComVirgulaPontoEVirgulaEEspaco()
    {
        var kw = Keywords.Parse("dotnet;aws angular");

        kw.Values.Should().BeEquivalentTo(["dotnet", "aws", "angular"]);
    }

    [Fact]
    public void Parse_IgnoraKeywordsComMenosDe2Caracteres()
    {
        var kw = Keywords.Parse("dotnet, a, aws");

        kw.Values.Should().NotContain("a");
        kw.Values.Should().BeEquivalentTo(["dotnet", "aws"]);
    }

    // ─── Parse inválido ──────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Parse_EntradaVaziaOuNula_LancaDomainException(string? input)
    {
        var act = () => Keywords.Parse(input!);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Parse_SomenteKeywordsInvalidas_LancaDomainException()
    {
        // "a" e "b" têm menos de 2 chars → nenhuma keyword válida
        var act = () => Keywords.Parse("a, b");

        act.Should().Throw<DomainException>()
            .WithMessage("*válida*");
    }

    // ─── Value Object — igualdade por valor ──────────────────────────────

    [Fact]
    public void Equals_MesmasKeywords_OrdemDiferente_SaoIguais()
    {
        var a = Keywords.Parse("aws, dotnet");
        var b = Keywords.Parse("dotnet, aws");

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equals_KeywordsDiferentes_NaoSaoIguais()
    {
        var a = Keywords.Parse("dotnet");
        var b = Keywords.Parse("angular");

        a.Should().NotBe(b);
    }

    // ─── ToCacheKey ───────────────────────────────────────────────────────

    [Fact]
    public void ToCacheKey_MesmoResultadoIndependenteDaOrdem()
    {
        var a = Keywords.Parse("aws, dotnet, angular");
        var b = Keywords.Parse("dotnet, angular, aws");

        a.ToCacheKey().Should().Be(b.ToCacheKey());
    }
}
