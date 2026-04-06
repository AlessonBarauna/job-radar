using JobRadar.Application.DTOs;

namespace JobRadar.Application.Interfaces;

public interface IReportService
{
    Task<ReportResponseDto> GenerateAsync(string rawKeywords, CancellationToken ct = default);
}
