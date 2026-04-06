using JobRadar.Application.DTOs;
using JobRadar.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace JobRadar.API.Controllers;

/// <summary>
/// Endpoints do histórico de buscas.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HistoryController(ISearchHistoryRepository historyRepo) : ControllerBase
{
    /// <summary>Retorna as buscas mais recentes.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<SearchHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecent([FromQuery] int limit = 20, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var history = await historyRepo.GetRecentAsync(limit, ct);

        var dtos = history.Select(h => new SearchHistoryDto
        {
            Id          = h.Id,
            Keywords    = h.Keywords,
            ResultCount = h.ResultCount,
            SearchedAt  = h.SearchedAt,
            ElapsedMs   = h.ElapsedMs,
            Provider    = h.Provider,
            RelativeTime = ToRelativeTime(h.SearchedAt)
        }).ToList();

        return Ok(dtos);
    }

    private static string ToRelativeTime(DateTime dt)
    {
        var diff = DateTime.UtcNow - dt;
        return diff.TotalMinutes switch
        {
            < 1    => "agora mesmo",
            < 60   => $"há {(int)diff.TotalMinutes}min",
            < 1440 => $"há {(int)diff.TotalHours}h",
            _      => $"há {(int)diff.TotalDays}d"
        };
    }
}
