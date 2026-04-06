namespace JobRadar.Application.DTOs;

public class ReportResponseDto
{
    public string Keywords  { get; set; } = string.Empty;
    public string Markdown  { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public bool FromCache   { get; set; }
    public long ElapsedMs   { get; set; }
}
