using JobRadar.API.Data;
using JobRadar.API.Repositories;
using JobRadar.API.Repositories.Interfaces;
using JobRadar.API.Services;
using JobRadar.API.Services.Interfaces;
using JobRadar.API.Workers;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ─── Database ───────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=jobradar.db"));

// ─── Cache ───────────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();

// ─── HttpClient ──────────────────────────────────────────────────────────────
builder.Services.AddHttpClient("Bing", c =>
{
    c.Timeout = TimeSpan.FromSeconds(10);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("JobRadar/1.0");
});
builder.Services.AddHttpClient("Google", c =>
{
    c.Timeout = TimeSpan.FromSeconds(10);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("JobRadar/1.0");
});

// ─── Services ────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IBingSearchService, BingSearchService>();
builder.Services.AddScoped<IGoogleCustomSearchService, GoogleCustomSearchService>();
builder.Services.AddScoped<IRelevanceService, RelevanceService>();
builder.Services.AddScoped<ISearchHistoryRepository, SearchHistoryRepository>();
builder.Services.AddScoped<IJobSearchService, JobSearchService>();

// ─── Background Worker ───────────────────────────────────────────────────────
builder.Services.AddHostedService<JobCollectorWorker>();

// ─── API ─────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "JobRadar API",
        Version = "v1",
        Description = "API de busca inteligente de vagas no LinkedIn usando dados públicos indexados."
    });
});

// ─── CORS ─────────────────────────────────────────────────────────────────────
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                  ?? ["http://localhost:4200"];

builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(policy =>
        policy.WithOrigins(corsOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()));

// ─── Build ────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ─── Migrations automáticas ───────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// ─── Middleware ───────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "JobRadar v1"));
}

app.UseCors();
app.MapControllers();

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapGet("/health", () => Results.Ok(new { status = "healthy", time = DateTime.UtcNow }));

app.Run();
