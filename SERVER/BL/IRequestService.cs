using RMS.Models.DTO;

namespace RMS.BL
{
    public interface IRequestService
    {
        Task<PagedResult<RequestDto>> Get(RequestQuery query, CancellationToken ct);
        Task<RequestDto> Get(int id, CancellationToken ct);
        Task<SummaryDto> GetSummary(CancellationToken ct);
        Task<List<StatusHistoryDto>> GetHistory(int id, CancellationToken ct);
        Task<RequestDto> UpdateStatus(int id, UpdateStatusDto dto, CancellationToken ct);
        Task<BulkStatusResultDto> UpdateBulk(BulkStatusDto dto, CancellationToken ct);
        Task<int> Seed(SeedDto dto, CancellationToken ct);
    }
}
