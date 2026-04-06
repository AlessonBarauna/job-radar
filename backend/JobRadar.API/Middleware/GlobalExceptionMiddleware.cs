using System.Text.Json;
using JobRadar.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace JobRadar.API.Middleware;

/// <summary>
/// Middleware de tratamento global de exceções.
/// Mapeia exceções de domínio para respostas HTTP padronizadas (RFC 7807 ProblemDetails).
/// </summary>
public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exceção não tratada: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var (status, title) = ex switch
        {
            DomainException          => (StatusCodes.Status400BadRequest,  "Erro de domínio"),
            ArgumentException        => (StatusCodes.Status400BadRequest,  "Parâmetro inválido"),
            OperationCanceledException => (StatusCodes.Status408RequestTimeout, "Requisição cancelada"),
            _                        => (StatusCodes.Status500InternalServerError, "Erro interno do servidor")
        };

        var problem = new ProblemDetails
        {
            Status   = status,
            Title    = title,
            Detail   = ex.Message,
            Instance = context.Request.Path
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode  = status;

        return context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
