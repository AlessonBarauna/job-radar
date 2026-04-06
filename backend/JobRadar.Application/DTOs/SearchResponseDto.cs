namespace JobRadar.Application.DTOs;

public class SearchResponseDto
{
    public List<JobResultDto> Results { get; set; } = [];
    public int Total { get; set; }
    public List<string> Keywords { get; set; } = [];
    public string Provider { get; set; } = string.Empty;
    public long ElapsedMs { get; set; }
    public DateTime SearchedAt { get; set; }
    public bool FromCache { get; set; }
}
