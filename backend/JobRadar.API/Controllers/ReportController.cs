using JobRadar.Application.DTOs;
using JobRadar.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobRadar.API.Controllers;

/// <summary>
/// Endpoint de geração de relatório inteligente de mercado.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ReportController(IReportService reportService, ILogger<ReportController> logger) : ControllerBase
{
    /// <summary>
    /// Gera relatório de mercado com IA para as keywords fornecidas.
    /// Inclui panorama, empresas, salários, stack e dicas de processo seletivo.
    /// </summary>
    /// <param name="keywords">Palavras-chave (ex: dotnet,csharp,backend)</param>
    [HttpGet("generate")]
    [EnableRateLimiting("search")]
    [ProducesResponseType(typeof(ReportResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Generate([FromQuery] string keywords, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(keywords))
            return BadRequest(new ProblemDetails
            {
                Title  = "Parâmetro obrigatório",
                Detail = "O parâmetro 'keywords' é obrigatório.",
                Status = 400
            });

        logger.LogInformation("Relatório solicitado: {Keywords}", keywords);

        try
        {
            var result = await reportService.GenerateAsync(keywords, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("LLM não configurado"))
        {
            return StatusCode(503, new ProblemDetails
            {
                Title  = "LLM não configurado",
                Detail = ex.Message,
                Status = 503
            });
        }
    }
}
