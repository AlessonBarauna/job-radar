using JobRadar.Application.Interfaces;

namespace JobRadar.API.Workers;

/// <summary>
/// Background worker que pre-aquece o cache com keywords configuradas em appsettings.
/// Desabilitado por padrão: Worker:Enabled = false.
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
            logger.LogInformation("JobCollectorWorker desabilitado.");
            return;
        }

        logger.LogInformation("JobCollectorWorker iniciado. Intervalo: {H}h", _interval.TotalHours);
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
                          ?? ["dotnet,csharp", "angular,typescript", "aws,devops"];

        using var scope         = scopeFactory.CreateScope();
        var searchService = scope.ServiceProvider.GetRequiredService<IJobSearchService>();

        foreach (var keywords in keywordSets)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                logger.LogInformation("Worker coletando: {Keywords}", keywords);
                await searchService.SearchAsync(keywords, ct);
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Worker falhou para: {Keywords}", keywords);
            }
        }
    }
}
