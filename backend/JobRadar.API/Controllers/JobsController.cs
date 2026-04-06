using JobRadar.Application.DTOs;
using JobRadar.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace JobRadar.API.Controllers;

/// <summary>
/// Endpoints de busca de vagas.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class JobsController(IJobSearchService searchService, ILogger<JobsController> logger) : ControllerBase
{
    /// <summary>Busca vagas pelos provedores configurados, ordenadas por relevância.</summary>
    /// <param name="keywords">Palavras-chave separadas por vírgula (ex: dotnet,aws,angular)</param>
    [HttpGet("search")]
    [ProducesResponseType(typeof(SearchResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search([FromQuery] string keywords, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(keywords))
            return BadRequest(new ProblemDetails
            {
                Title  = "Parâmetro obrigatório",
                Detail = "O parâmetro 'keywords' é obrigatório.",
                Status = 400
            });

        logger.LogInformation("Busca recebida: {Keywords}", keywords);
        var result = await searchService.SearchAsync(keywords, ct);
        return Ok(result);
    }
}
