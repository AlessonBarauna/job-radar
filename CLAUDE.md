# CLAUDE.md

Este arquivo fornece orientações ao Claude Code (claude.ai/code) ao trabalhar neste repositório.

---

## Executando o Projeto

```bash
# Backend — http://localhost:5180 (Swagger: /swagger)
cd backend/JobRadar.API
dotnet run

# Frontend — http://localhost:4200
cd frontend/job-radar-frontend
npm install && npm start

# Builds de produção
dotnet publish -c Release -o out
npm run build:prod   # gera em dist/job-radar-frontend/browser
```

---

## Fluxo Git — OBRIGATÓRIO

**Nunca escreva código sem criar a branch primeiro.**

```bash
# 1. Sincronizar main
git checkout main
git pull origin main

# 2. Criar branch ANTES de implementar
git checkout -b feat/nome-descritivo
# Exemplos: feat/sistema-busca, feat/relevancia-score, fix/cache-bug

# 3. Implementar (seguindo arquitetura abaixo)

# 4. Commit semântico em português
git add .
git commit -m "feat: descrição da funcionalidade implementada"
# Outros prefixos: fix:, refactor:, docs:, test:

# 5. Subir branch
git push origin feat/nome-descritivo
```

---

## Arquitetura Atual

**Backend** (`backend/JobRadar.API/`): .NET 8 + EF Core + SQLite.
Fluxo: `Controller → Service → Repository → DbContext`

| Componente | Responsabilidade |
|---|---|
| `JobsController` | `GET /api/jobs/search?keywords=...` — entrada da API |
| `JobSearchService` | Orquestra: parse → cache (15min TTL) → Bing/Google/Mock → score → salva histórico |
| `RelevanceService` | Score 0–100: título (×3), snippet (×1), bônus de recência |
| `BingSearchService` / `GoogleCustomSearchService` | Integrações externas; `MockSearchService` como fallback |
| `HistoryController` | `GET /api/history?limit=20` |
| `JobCollectorWorker` | Worker background para pre-warm de cache (desabilitado por padrão) |

**Banco de dados**: SQLite (`jobradar.db`), migrado automaticamente via `EnsureCreated()`. Tabelas: `JobResults`, `SearchHistories`.

**Frontend** (`frontend/job-radar-frontend/`): Angular 17 standalone + TailwindCSS tema cyberpunk.

| Componente | Responsabilidade |
|---|---|
| `SearchComponent` | Página principal — estado de busca, sidebar de histórico, stats |
| `JobSearchService` | HTTP client chamando o backend |
| `JobCardComponent` | Card de resultado com score, keywords, tempo relativo |

**URL base da API** configurada em `src/environments/environment.ts` (`http://localhost:5180` dev).

---

## Configuração

### Provedores de busca — cascata: Bing → Google → Remotive → Jooble → Mock

| Provedor | Custo | Setup | Foco |
|---|---|---|---|
| **Remotive** | Gratuito | Nenhum | Remote jobs globais — **ativo por padrão** |
| **Jooble** | Gratuito | API key via e-mail em jooble.org/api/about | Vagas brasileiras |
| **Bing** | 1000 calls/mês grátis | Azure key | LinkedIn Brasil |
| **Google CSE** | 100 queries/dia grátis | API key + CSE ID | LinkedIn Brasil |
| **Mock** | — | Nenhum | Fallback de desenvolvimento |

Chaves opcionais em `backend/JobRadar.API/appsettings.Development.json`:

```json
{
  "Search": {
    "JoobleApiKey": "...",
    "BingApiKey": "",
    "GoogleApiKey": "",
    "GoogleCseId": ""
  }
}
```

Sem nenhuma configuração, o app usa **Remotive** automaticamente — dados reais de vagas remotas.
CORS configurado em `appsettings.json` (`localhost:4200` + URL de produção Vercel).

---

## Padrões de Desenvolvimento

### Clean Architecture — Camadas e Dependências

As dependências sempre apontam para dentro (Domain nunca depende de Infrastructure):

```
Presentation (API)
    ↓ depende de
Application (Services, UseCases, DTOs)
    ↓ depende de
Domain (Entities, Interfaces, Value Objects)
    ↑ implementado por
Infrastructure (EF Core, Repositories, APIs externas)
```

**Estrutura de pastas de referência:**

```
JobRadar.sln
├── JobRadar.Domain/
│   ├── Entities/          # Entidades de domínio
│   ├── ValueObjects/      # Objetos de valor imutáveis
│   ├── Repositories/      # Interfaces (IJobRepository, etc.)
│   ├── Services/          # Domain services (lógica multi-entidade)
│   └── Exceptions/        # JobNotFoundException, etc.
├── JobRadar.Application/
│   ├── DTOs/              # Input/Output dos use cases
│   ├── Services/          # JobSearchService, RelevanceService
│   ├── UseCases/          # (futura CQRS: Commands/Queries)
│   └── Mappings/          # AutoMapper profiles
├── JobRadar.Infrastructure/
│   ├── Data/              # AppDbContext, Fluent API configs
│   ├── Repositories/      # Implementações concretas
│   └── Services/          # BingSearchService, GoogleSearchService
└── JobRadar.API/
    ├── Controllers/
    ├── Middleware/        # GlobalExceptionHandler
    └── Program.cs
```

> **Regra de ouro:** Domain Layer **nunca** referencia EF Core, HttpClient ou qualquer infra.

---

### Domain-Driven Design (DDD)

**Entities** — têm identidade e ciclo de vida:

```csharp
public class JobResult
{
    public int Id { get; private set; }
    public string Titulo { get; private set; }
    public string Url { get; private set; }
    public int RelevanceScore { get; private set; }

    private JobResult() { } // EF Core

    public static JobResult Criar(string titulo, string url)
    {
        if (string.IsNullOrWhiteSpace(titulo))
            throw new DomainException("Título é obrigatório.");
        return new JobResult { Titulo = titulo, Url = url };
    }

    public void AtualizarScore(int score)
    {
        if (score < 0 || score > 100)
            throw new DomainException("Score deve estar entre 0 e 100.");
        RelevanceScore = score;
    }
}
```

**Value Objects** — imutáveis, sem identidade:

```csharp
public record Keywords
{
    public IReadOnlyList<string> Valores { get; }

    public Keywords(IEnumerable<string> valores)
    {
        Valores = valores
            .Select(v => v.Trim().ToLower())
            .Where(v => !string.IsNullOrEmpty(v))
            .Take(10)
            .ToList();

        if (!Valores.Any())
            throw new DomainException("Pelo menos uma keyword é obrigatória.");
    }
}
```

**Repository Interface** (no Domain, implementação na Infrastructure):

```csharp
// Domain/Repositories/IJobRepository.cs
public interface IJobRepository
{
    Task<JobResult?> ObterPorUrlAsync(string url);
    Task<IEnumerable<JobResult>> ListarPorKeywordsAsync(IEnumerable<string> keywords);
    Task AdicionarAsync(JobResult job);
    Task SalvarAsync();
}
```

---

### Camada de Aplicação — Services e DTOs

Services de aplicação orquestram o fluxo sem conter regras de domínio:

```csharp
public class JobSearchService
{
    private readonly IJobRepository _repository;
    private readonly IBingSearchService _bingSearch;
    private readonly IRelevanceService _relevance;
    private readonly IMemoryCache _cache;

    public JobSearchService(
        IJobRepository repository,
        IBingSearchService bingSearch,
        IRelevanceService relevance,
        IMemoryCache cache)
    {
        _repository = repository;
        _bingSearch = bingSearch;
        _relevance = relevance;
        _cache = cache;
    }

    public async Task<SearchResponseDto> BuscarAsync(SearchRequestDto request)
    {
        var keywords = new Keywords(request.Keywords); // validação no Value Object

        if (_cache.TryGetValue(keywords, out SearchResponseDto? cached))
            return cached! with { FromCache = true };

        var resultados = await _bingSearch.BuscarAsync(keywords.Valores);
        var comScore = _relevance.Calcular(resultados, keywords.Valores);

        var response = new SearchResponseDto(comScore, keywords.Valores, "Bing");
        _cache.Set(keywords, response, TimeSpan.FromMinutes(15));

        return response;
    }
}
```

**DTOs** — nunca expor entidades de domínio diretamente:

```csharp
public record SearchRequestDto(IEnumerable<string> Keywords);

public record SearchResponseDto(
    IEnumerable<JobResultDto> Results,
    IEnumerable<string> Keywords,
    string Provider,
    int Total = 0,
    bool FromCache = false,
    long ElapsedMs = 0);

public record JobResultDto(
    int Id,
    string Titulo,
    string Snippet,
    string Url,
    int RelevanceScore,
    IEnumerable<string> MatchedKeywords,
    string TempoRelativo);
```

---

### Camada de Apresentação — Controllers

Controllers têm responsabilidade mínima: receber, validar input e delegar:

```csharp
[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly JobSearchService _searchService;

    public JobsController(JobSearchService searchService)
        => _searchService = searchService;

    [HttpGet("search")]
    [ProducesResponseType(typeof(SearchResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Buscar([FromQuery] string keywords)
    {
        if (string.IsNullOrWhiteSpace(keywords))
            return BadRequest(new ProblemDetails
            {
                Title = "Keywords inválidas",
                Detail = "Informe ao menos uma keyword de busca.",
                Status = 400
            });

        var request = new SearchRequestDto(keywords.Split(',', ';'));
        var resultado = await _searchService.BuscarAsync(request);
        return Ok(resultado);
    }
}
```

**Global Exception Handler** (middleware em `Program.cs`):

```csharp
app.UseExceptionHandler(appError =>
{
    appError.Run(async context =>
    {
        context.Response.ContentType = "application/problem+json";
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var ex = feature?.Error;

        var (status, title) = ex switch
        {
            DomainException => (400, "Erro de domínio"),
            NotFoundException => (404, "Recurso não encontrado"),
            _ => (500, "Erro interno do servidor")
        };

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = ex?.Message
        });
    });
});
```

---

### Dependency Injection — Registro de Serviços

```csharp
// Program.cs
builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<JobSearchService>();
builder.Services.AddScoped<IRelevanceService, RelevanceService>();
builder.Services.AddScoped<IBingSearchService, BingSearchService>();
builder.Services.AddScoped<IGoogleSearchService, GoogleCustomSearchService>();
builder.Services.AddMemoryCache();
```

| Lifetime | Quando usar |
|---|---|
| `Singleton` | Cache, configurações, workers |
| `Scoped` | Services e Repositories (padrão para HTTP requests) |
| `Transient` | Utilitários leves, sem estado |

---

### Entity Framework Core — Boas Práticas

```csharp
// Fluent API no DbContext (nunca Data Annotations em entidades de domínio)
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<JobResult>(entity =>
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Titulo).IsRequired().HasMaxLength(500);
        entity.HasIndex(e => e.Url).IsUnique();
        entity.Property(e => e.RelevanceScore).HasDefaultValue(0);
    });
}
```

```bash
# Migrations
dotnet ef migrations add NomeMigracao
dotnet ef database update
```

---

### async/await — Padrões

```csharp
// CORRETO — async do início ao fim
public async Task<SearchResponseDto> BuscarAsync(SearchRequestDto request)
{
    var resultados = await _repository.ListarPorKeywordsAsync(request.Keywords);
    return MapToDto(resultados);
}

// EVITAR — .Result ou .Wait() causam deadlock
var resultado = _service.BuscarAsync(request).Result; // NUNCA
```

---

## Checklist — Antes de Implementar Qualquer Feature

- [ ] Qual entidade do Domain está envolvida?
- [ ] Preciso de novo Repository ou reutilizo o existente?
- [ ] Qual é o use case? (criar, atualizar, listar, deletar)
- [ ] Validação é de domínio (Domain Exception) ou de input (BadRequest)?
- [ ] Qual verbo HTTP? (`GET` leitura, `POST` criação, `PUT` substituição, `PATCH` parcial, `DELETE`)
- [ ] Está usando `async/await` em toda a cadeia?
- [ ] O Controller delega tudo para o Service?
- [ ] O Domain Layer não referencia EF Core nem HttpClient?
- [ ] Os DTOs estão separados das entidades?
- [ ] Erros estão sendo tratados e propagando `DomainException` quando apropriado?

---

## Red Flags — Evitar Sempre

| Anti-padrão | Por quê evitar |
|---|---|
| `DbContext` injetado diretamente no Controller | Viola separação de camadas |
| Service com mais de 300 linhas | Responsabilidade única violada — dividir |
| Lógica de negócio no Controller | Controller deve apenas receber e delegar |
| Método com mais de 4 parâmetros | Criar um objeto de request/DTO |
| Retornar entidades de domínio direto na API | Expõe estrutura interna — usar DTOs |
| `async void` em services | Exceções são perdidas — usar `async Task` |
| Código duplicado em múltiplos services | Extrair para um service compartilhado |

---

## Padrões de Referência

| Padrão | Quando Usar |
|---|---|
| **Repository** | Todo acesso a dados — sempre |
| **DTO** | Input/Output de controllers e services |
| **Domain Service** | Lógica que envolve múltiplas entidades |
| **Specification** | Queries complexas com filtragem/paginação |
| **Factory** | Criação de entidades complexas com validação |
| **Mediator/CQRS** | Quando commands e queries precisam ser separados (futuro) |
| **Decorator** | Cross-cutting concerns: logging, cache, retry |

---

## Resposta de Erro Padronizada (RFC 7807)

Toda resposta de erro deve seguir o formato `ProblemDetails`:

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Recurso não encontrado",
  "status": 404,
  "detail": "JobResult com Id '42' não foi encontrado.",
  "instance": "/api/jobs/42"
}
```
