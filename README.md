# JobRadar 🎯

Busca inteligente de vagas e posts no LinkedIn usando dados públicos indexados.
Filtra apenas resultados das **últimas 24 horas** e ordena por **score de relevância**.

---

## Stack

| Camada | Tecnologia |
|--------|-----------|
| Backend | .NET 8 · EF Core · SQLite · IMemoryCache |
| Busca | Bing Web Search API v7 · Google Custom Search API · Mock (dev) |
| Frontend | Angular 17 · Standalone · TailwindCSS · Signals |
| Deploy | Render (backend) · Vercel (frontend) |

---

## Rodar localmente

### Backend

```bash
cd backend/JobRadar.API

# Restaurar + rodar (porta 5180 por padrão)
dotnet restore
dotnet run

# Swagger disponível em: http://localhost:5180/swagger
```

### Frontend

```bash
cd frontend/job-radar-frontend

npm install
npm start
# http://localhost:4200
```

---

## Configurar APIs de busca (opcional)

Por padrão a aplicação usa dados **mock** para desenvolvimento.
Para dados reais, configure as chaves em `backend/JobRadar.API/appsettings.Development.json`:

### Bing Web Search API (recomendado)
- Gratuito: **1.000 calls/mês**
- Acesse [portal.azure.com](https://portal.azure.com) → Criar recurso → "Bing Search v7"
- Copie a chave para `Search:BingApiKey`

### Google Custom Search API (alternativa)
- Gratuito: **100 queries/dia**
- [console.cloud.google.com](https://console.cloud.google.com) → Ativar "Custom Search JSON API" → Criar API Key
- [programmablesearchengine.google.com](https://programmablesearchengine.google.com) → Criar motor → Site: `linkedin.com`
- Configure `Search:GoogleApiKey` e `Search:GoogleCseId`

```json
// appsettings.Development.json
{
  "Search": {
    "BingApiKey": "SUA_CHAVE_AQUI",
    "GoogleApiKey": "",
    "GoogleCseId": ""
  }
}
```

---

## Endpoints da API

```
GET  /api/jobs/search?keywords=.net,aws,csharp   → Busca vagas (últimas 24h)
GET  /api/history?limit=20                        → Histórico de buscas
GET  /health                                      → Health check
GET  /swagger                                     → Documentação interativa
```

### Exemplo de resposta

```json
{
  "results": [
    {
      "id": 1,
      "title": "Desenvolvedor .NET Senior | Nubank",
      "snippet": "Nubank está contratando...",
      "author": "Nubank",
      "url": "https://www.linkedin.com/jobs/view/...",
      "publishedAt": "2024-01-15T10:30:00Z",
      "relevanceScore": 87,
      "matchedKeywords": [".net", "csharp"],
      "resultType": "job",
      "relativeTime": "há 3h"
    }
  ],
  "total": 12,
  "keywords": [".net", "aws", "csharp"],
  "provider": "Bing",
  "elapsedMs": 342,
  "fromCache": false
}
```

---

## Score de Relevância (0–100)

| Critério | Peso |
|----------|------|
| Keyword no título | +3 por keyword |
| Keyword no snippet | +1 por keyword |
| Publicado há < 1h | +30 |
| Publicado há < 3h | +25 |
| Publicado há < 6h | +20 |
| Publicado há < 12h | +15 |
| Publicado há < 18h | +10 |
| Publicado há < 24h | +5 |

---

## Deploy gratuito

### Backend → Render

1. Crie conta em [render.com](https://render.com)
2. Novo → Web Service → conecte o repositório
3. Build command: `dotnet publish -c Release -o out`
4. Start command: `dotnet out/JobRadar.API.dll`
5. Adicione variáveis de ambiente: `Search__BingApiKey`, etc.

### Frontend → Vercel

1. Crie conta em [vercel.com](https://vercel.com)
2. Importe o repositório → selecione `frontend/job-radar-frontend`
3. Framework: Angular
4. Build command: `npm run build:prod`
5. Output directory: `dist/job-radar-frontend/browser`
6. Adicione variável: `apiUrl` com a URL do Render

---

## Regras de uso

- ✅ Apenas dados públicos e indexados por motores de busca
- ✅ Redireciona sempre para o link original no LinkedIn
- ✅ Sem scraping autenticado
- ✅ Sem bypass de login
- ✅ Sem reprodução de conteúdo completo
