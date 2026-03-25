using JobRadar.API.Services.Interfaces;

namespace JobRadar.API.Workers;

/// <summary>
/// Background worker que dispara buscas automáticas a cada hora
/// para keywords configuradas em appsettings: Worker:Keywords.
///
/// Útil para pre-aquecer o cache com termos populares.
/// Desative via appsettings: Worker:Enabled = false.
/// </summary>
public class JobCollectorWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<JobCollectorWorker> logger) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromHours(
        configuration.GetValue("Worker:IntervalHours", 1));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Worker:Enabled", false))
        {
            logger.LogInformation("JobCollectorWorker desabilitado (Worker:Enabled=false).");
            return;
        }

        logger.LogInformation("JobCollectorWorker iniciado. Intervalo: {Interval}h", _interval.TotalHours);

        // Aguarda 30s no startup para não sobrecarregar a inicialização
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await CollectAsync(stoppingToken);
            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task CollectAsync(CancellationToken ct)
    {
        var keywordSets = configuration.GetSection("Worker:Keywords").Get<string[]>()
                          ?? [".net,csharp", "angular,frontend", "aws,cloud"];

        using var scope = scopeFactory.CreateScope();
        var searchService = scope.ServiceProvider.GetRequiredService<IJobSearchService>();

        foreach (var keywords in keywordSets)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                logger.LogInformation("Worker coletando: {Keywords}", keywords);
                await searchService.SearchAsync(keywords, ct);
                await Task.Delay(TimeSpan.FromSeconds(5), ct); // rate limiting
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Worker falhou para keywords: {Keywords}", keywords);
            }
        }
    }
}
