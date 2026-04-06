using FluentAssertions;
using JobRadar.Application.Interfaces;
using JobRadar.Application.Services;
using JobRadar.Domain.Entities;
using JobRadar.Domain.Repositories;
using JobRadar.Domain.Services;
using JobRadar.Domain.ValueObjects;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace JobRadar.Tests.Application;

public class JobSearchServiceTests
{
    // ─── Helpers ──────────────────────────────────────────────────────────

    private static JobResult MakeJob(string title = "Dotnet Developer", int hoursAgo = 2) =>
        JobResult.Create(title, "Snippet da vaga",
            $"https://remotive.com/jobs/{Guid.NewGuid()}",
            DateTime.UtcNow.AddHours(-hoursAgo));

    private static Mock<IJobProvider> MakeProvider(
        string name,
        ProviderMode mode,
        int priority,
        List<JobResult>? returns = null,
        bool configured = true)
    {
        var mock = new Mock<IJobProvider>();
        mock.Setup(p => p.Name).Returns(name);
        mock.Setup(p => p.Mode).Returns(mode);
        mock.Setup(p => p.Priority).Returns(priority);
        mock.Setup(p => p.IsConfigured).Returns(configured);
        mock.Setup(p => p.FetchAsync(It.IsAny<Keywords>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns ?? [MakeJob()]);
        return mock;
    }

    private static JobSearchService BuildService(
        IEnumerable<IJobProvider> providers,
        IMemoryCache? cache = null)
    {
        var relevance   = new RelevanceService();
        var historyRepo = new Mock<ISearchHistoryRepository>();
        historyRepo.Setup(r => r.SaveAsync(It.IsAny<SearchHistory>(), It.IsAny<CancellationToken>()))
                   .Returns(Task.CompletedTask);

        cache ??= new MemoryCache(new MemoryCacheOptions());

        return new JobSearchService(
            providers,
            relevance,
            historyRepo.Object,
            cache,
            NullLogger<JobSearchService>.Instance);
    }

    // ─── Cascata de provedores ────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_ProviderSequentialConfigurado_UsaEsse()
    {
        var bing   = MakeProvider("Bing",   ProviderMode.Sequential, priority: 1);
        var mock   = MakeProvider("Mock",   ProviderMode.Sequential, priority: int.MaxValue);
        var svc    = BuildService([bing.Object, mock.Object]);

        var result = await svc.SearchAsync("dotnet");

        result.Provider.Should().Be("Bing");
        mock.Verify(p => p.FetchAsync(It.IsAny<Keywords>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_SequentialRetornaVazio_CaiParaParalelo()
    {
        var bing     = MakeProvider("Bing",     ProviderMode.Sequential, priority: 1, returns: []);
        var remotive = MakeProvider("Remotive", ProviderMode.Parallel,   priority: 10);
        var mock     = MakeProvider("Mock",     ProviderMode.Sequential, priority: int.MaxValue);
        var svc      = BuildService([bing.Object, remotive.Object, mock.Object]);

        var result = await svc.SearchAsync("dotnet");

        result.Provider.Should().Contain("Remotive");
        mock.Verify(p => p.FetchAsync(It.IsAny<Keywords>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_TodosParaleloshRetornamVazio_UsaMock()
    {
        var remotive = MakeProvider("Remotive", ProviderMode.Parallel,   priority: 10, returns: []);
        var indeed   = MakeProvider("Indeed",   ProviderMode.Parallel,   priority: 11, returns: []);
        var mock     = MakeProvider("Mock",     ProviderMode.Sequential, priority: int.MaxValue);
        var svc      = BuildService([remotive.Object, indeed.Object, mock.Object]);

        var result = await svc.SearchAsync("dotnet");

        result.Provider.Should().Be("Mock");
    }

    [Fact]
    public async Task SearchAsync_ProvedoresParaleloCombinados_RetornaTodosResultados()
    {
        var remotive = MakeProvider("Remotive", ProviderMode.Parallel, priority: 10,
            returns: [MakeJob("Dotnet Remotive")]);
        var indeed = MakeProvider("Indeed", ProviderMode.Parallel, priority: 11,
            returns: [MakeJob("Dotnet Indeed")]);
        var svc = BuildService([remotive.Object, indeed.Object]);

        var result = await svc.SearchAsync("dotnet");

        result.Results.Should().HaveCount(2);
        result.Provider.Should().Contain("Remotive").And.Contain("Indeed");
    }

    [Fact]
    public async Task SearchAsync_ProviderNaoConfigurado_EIgnorado()
    {
        var bing     = MakeProvider("Bing",     ProviderMode.Sequential, priority: 1, configured: false);
        var remotive = MakeProvider("Remotive", ProviderMode.Parallel,   priority: 10);
        var svc      = BuildService([bing.Object, remotive.Object]);

        var result = await svc.SearchAsync("dotnet");

        bing.Verify(p => p.FetchAsync(It.IsAny<Keywords>(), It.IsAny<CancellationToken>()), Times.Never);
        result.Provider.Should().Contain("Remotive");
    }

    // ─── Cache ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_SegundaChamadaMesmasKeywords_RetornaDoCache()
    {
        var provider = MakeProvider("Bing", ProviderMode.Sequential, priority: 1);
        var cache    = new MemoryCache(new MemoryCacheOptions());
        var svc      = BuildService([provider.Object], cache);

        await svc.SearchAsync("dotnet");
        var result = await svc.SearchAsync("dotnet");

        result.FromCache.Should().BeTrue();
        // Provider chamado apenas 1 vez (segunda bateu no cache)
        provider.Verify(p => p.FetchAsync(It.IsAny<Keywords>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_KeywordsDiferentes_NaoBateNoCache()
    {
        var provider = MakeProvider("Bing", ProviderMode.Sequential, priority: 1);
        var cache    = new MemoryCache(new MemoryCacheOptions());
        var svc      = BuildService([provider.Object], cache);

        await svc.SearchAsync("dotnet");
        await svc.SearchAsync("angular");

        provider.Verify(p => p.FetchAsync(It.IsAny<Keywords>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ─── Keywords inválidas ───────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]  // menos de 2 chars
    public async Task SearchAsync_KeywordsInvalidas_LancaExcecao(string keywords)
    {
        var svc = BuildService([]);

        var act = async () => await svc.SearchAsync(keywords);

        await act.Should().ThrowAsync<Exception>();
    }

    // ─── Ordenação por relevância ─────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_ResultadosOrdenadosPorScore_MaisRelevantePrimeiro()
    {
        var alta  = MakeJob("Senior Dotnet Developer dotnet csharp", hoursAgo: 1);
        var baixa = MakeJob("Java Engineer", hoursAgo: 20);
        var provider = MakeProvider("Mock", ProviderMode.Sequential, priority: int.MaxValue,
            returns: [baixa, alta]);
        var svc = BuildService([provider.Object]);

        var result = await svc.SearchAsync("dotnet");

        result.Results[0].RelevanceScore.Should()
            .BeGreaterOrEqualTo(result.Results[1].RelevanceScore);
    }
}
