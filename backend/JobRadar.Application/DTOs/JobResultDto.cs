namespace JobRadar.Application.DTOs;

public class JobResultDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string Url { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public int RelevanceScore { get; set; }
    public List<string> MatchedKeywords { get; set; } = [];
    public string ResultType { get; set; } = "job";
    public string RelativeTime { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}
