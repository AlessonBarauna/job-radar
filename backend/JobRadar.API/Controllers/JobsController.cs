using JobRadar.API.DTOs;
using JobRadar.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace JobRadar.API.Controllers;

/// <summary>
/// Endpoints de busca de vagas e posts do LinkedIn.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class JobsController(IJobSearchService searchService, ILogger<JobsController> logger) : ControllerBase
{
    /// <summary>
    /// Busca vagas e posts do LinkedIn publicados nas últimas 24h.
    /// </summary>
    /// <param name="keywords">Palavras-chave separadas por vírgula (ex: .net,aws,csharp)</param>
    /// <param name="ct">Token de cancelamento</param>
    /// <returns>Lista de resultados ordenados por relevância</returns>
    /// <response code="200">Busca executada com sucesso</response>
    /// <response code="400">Keywords não fornecidas</response>
    [HttpGet("search")]
    [ProducesResponseType(typeof(SearchResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] string keywords,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(keywords))
            return BadRequest(new { error = "O parâmetro 'keywords' é obrigatório." });

        logger.LogInformation("Busca recebida: {Keywords}", keywords);

        var result = await searchService.SearchAsync(keywords, ct);
        return Ok(result);
    }
}
