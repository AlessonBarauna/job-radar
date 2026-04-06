using System.Threading.RateLimiting;
using JobRadar.API.Middleware;
using JobRadar.API.Workers;
using JobRadar.Application.Interfaces;
using JobRadar.Application.Services;
using JobRadar.Domain.Repositories;
using JobRadar.Domain.Services;
using JobRadar.Infrastructure.Data;
using JobRadar.Infrastructure.Providers;
using JobRadar.Infrastructure.Repositories;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ─── Database ─────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=jobradar.db"));

// ─── Cache ────────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();

// ─── Rate Limiting ────────────────────────────────────────────────────────
// Proteção contra abuso: máx 30 req/min por IP no endpoint de busca.
builder.Services.AddRateLimiter(opt =>
{
    opt.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await ctx.HttpContext.Response.WriteAsJsonAsync(new
        {
            status  = 429,
            title   = "Limite de requisições excedido",
            detail  = "Máximo de 30 buscas por minuto por IP. Aguarde e tente novamente.",
            retryAfterSeconds = 60
        }, ct);
    };

    opt.AddSlidingWindowLimiter("search", o =>
    {
        o.PermitLimit         = 30;
        o.Window              = TimeSpan.FromMinutes(1);
        o.SegmentsPerWindow   = 6;   // janela dividida em segmentos de 10s
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit          = 2;
    });
});

// ─── HttpClients ──────────────────────────────────────────────────────────
builder.Services.AddHttpClient("Bing",     c => ConfigureClient(c));
builder.Services.AddHttpClient("Google",   c => ConfigureClient(c));
builder.Services.AddHttpClient("Remotive", c => ConfigureClient(c, timeout: 15));
builder.Services.AddHttpClient("Jooble",   c => ConfigureClient(c));
builder.Services.AddHttpClient("Jobicy",   c => ConfigureClient(c, timeout: 15));
builder.Services.AddHttpClient("Adzuna",   c => ConfigureClient(c, timeout: 15));
builder.Services.AddHttpClient("Gupy",     c => ConfigureClient(c, timeout: 15));

static void ConfigureClient(HttpClient c, int timeout = 10)
{
    c.Timeout = TimeSpan.FromSeconds(timeout);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("JobRadar/2.0");
}

// ─── Domain Services ──────────────────────────────────────────────────────
builder.Services.AddScoped<IRelevanceService, RelevanceService>();

// ─── Repositories ─────────────────────────────────────────────────────────
builder.Services.AddScoped<ISearchHistoryRepository, SearchHistoryRepository>();

// ─── Provedores de busca (IJobProvider) ───────────────────────────────────
// Ordem registrada define a ordem de prioridade para providers Sequential.
// Para adicionar um novo provedor: adicione uma linha aqui — zero mudanças no resto.
builder.Services.AddScoped<IJobProvider, BingProvider>();
builder.Services.AddScoped<IJobProvider, GoogleProvider>();
builder.Services.AddScoped<IJobProvider, AdzunaProvider>();
builder.Services.AddScoped<IJobProvider, JoobleProvider>();
builder.Services.AddScoped<IJobProvider, RemotiveProvider>();
builder.Services.AddScoped<IJobProvider, JobicyProvider>();
builder.Services.AddScoped<IJobProvider, GupyProvider>();
builder.Services.AddScoped<IJobProvider, MockProvider>();

// ─── Application Services ─────────────────────────────────────────────────
builder.Services.AddScoped<IJobSearchService, JobSearchService>();

// ─── Background Worker ────────────────────────────────────────────────────
builder.Services.AddHostedService<JobCollectorWorker>();

// ─── API ──────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title       = "JobRadar API",
        Version     = "v2",
        Description = "Busca inteligente de vagas — Gupy BR · Jobicy · Remotive · Adzuna · Bing · Google."
    });
});

// ─── CORS ─────────────────────────────────────────────────────────────────
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                  ?? ["http://localhost:4200"];

builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(policy =>
        policy.WithOrigins(corsOrigins).AllowAnyMethod().AllowAnyHeader()));

// ─── Build ────────────────────────────────────────────────────────────────
var app = builder.Build();

// ─── Migrations automáticas ───────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// ─── Middleware pipeline ──────────────────────────────────────────────────
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "JobRadar v2"));
}

app.UseCors();
app.MapControllers();
app.MapGet("/",       () => Results.Redirect("/swagger"));
app.MapGet("/health", () => Results.Ok(new { status = "healthy", version = "2.0", time = DateTime.UtcNow }));

app.Run();
