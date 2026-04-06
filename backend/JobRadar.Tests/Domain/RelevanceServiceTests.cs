using FluentAssertions;
using JobRadar.Domain.Entities;
using JobRadar.Domain.Services;
using JobRadar.Domain.ValueObjects;

namespace JobRadar.Tests.Domain;

public class RelevanceServiceTests
{
    private readonly RelevanceService _sut = new();

    // ─── CalculateScore ───────────────────────────────────────────────────

    [Fact]
    public void CalculateScore_KeywordNoTitulo_RetornaScoreAlto()
    {
        var result = MakeJob(title: "Senior Dotnet Developer | Nubank");
        var kw = Keywords.Parse("dotnet");

        var score = _sut.CalculateScore(result, kw);

        score.Should().BeGreaterThan(50);
    }

    [Fact]
    public void CalculateScore_KeywordNoSnippet_RetornaScoreMenorQueNoTitulo()
    {
        var comTitulo  = MakeJob(title: "Dotnet Developer");
        var comSnippet = MakeJob(title: "Backend Developer", snippet: "Experiência com dotnet");
        var kw         = Keywords.Parse("dotnet");

        var scoreTitulo  = _sut.CalculateScore(comTitulo,  kw);
        var scoreSnippet = _sut.CalculateScore(comSnippet, kw);

        scoreTitulo.Should().BeGreaterThan(scoreSnippet);
    }

    [Fact]
    public void CalculateScore_SemMatch_ScoreMenorDoQueComMatch()
    {
        var kw         = Keywords.Parse("dotnet");
        var semMatch   = MakeJob(title: "Java Developer",     hoursAgo: 2);
        var comMatch   = MakeJob(title: "Dotnet Developer",   hoursAgo: 2);

        var scoreSem = _sut.CalculateScore(semMatch, kw);
        var scoreCom = _sut.CalculateScore(comMatch, kw);

        scoreSem.Should().BeLessThan(scoreCom);
    }

    [Fact]
    public void CalculateScore_VagaMaisRecente_ScoreMaiorQueVagaAntiga()
    {
        var kw    = Keywords.Parse("dotnet");
        var nova  = MakeJob(title: "Dotnet Dev", hoursAgo: 1);
        var velha = MakeJob(title: "Dotnet Dev", hoursAgo: 20);

        var scoreNova  = _sut.CalculateScore(nova,  kw);
        var scoreVelha = _sut.CalculateScore(velha, kw);

        scoreNova.Should().BeGreaterThan(scoreVelha);
    }

    [Fact]
    public void CalculateScore_SempreEntre1E100()
    {
        var result = MakeJob(title: "dotnet dotnet dotnet dotnet dotnet", hoursAgo: 0);
        var kw     = Keywords.Parse("dotnet,aws,angular,csharp,react");

        var score = _sut.CalculateScore(result, kw);

        score.Should().BeInRange(1, 100);
    }

    // ─── FindMatchedKeywords ──────────────────────────────────────────────

    [Fact]
    public void FindMatchedKeywords_RetornaApenasAsQueAparecemNoConteudo()
    {
        var result  = MakeJob(title: "Senior Dotnet Engineer", snippet: "Usando AWS diariamente");
        var kw      = Keywords.Parse("dotnet, aws, angular");

        var matched = _sut.FindMatchedKeywords(result, kw);

        matched.Should().BeEquivalentTo(["dotnet", "aws"]);
        matched.Should().NotContain("angular");
    }

    [Fact]
    public void FindMatchedKeywords_SemMatch_RetornaListaVazia()
    {
        var result  = MakeJob(title: "Java Developer");
        var kw      = Keywords.Parse("dotnet");

        var matched = _sut.FindMatchedKeywords(result, kw);

        matched.Should().BeEmpty();
    }

    [Fact]
    public void FindMatchedKeywords_CaseInsensitive()
    {
        var result  = MakeJob(title: "DOTNET Senior Dev");
        var kw      = Keywords.Parse("dotnet");

        var matched = _sut.FindMatchedKeywords(result, kw);

        matched.Should().Contain("dotnet");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private static JobResult MakeJob(
        string title    = "Developer",
        string snippet  = "Vaga de tecnologia",
        int hoursAgo    = 5)
    {
        var result = JobResult.Create(
            title:       title,
            snippet:     snippet,
            url:         $"https://remotive.com/jobs/{Guid.NewGuid()}",
            publishedAt: DateTime.UtcNow.AddHours(-hoursAgo));

        return result;
    }
}
