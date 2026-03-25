namespace JobRadar.API.DTOs;

/// <summary>
/// DTO do histórico de buscas.
/// </summary>
public class SearchHistoryDto
{
    public int Id { get; set; }
    public string Keywords { get; set; } = string.Empty;
    public int ResultCount { get; set; }
    public DateTime SearchedAt { get; set; }
    public long ElapsedMs { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string RelativeTime { get; set; } = string.Empty;
}
