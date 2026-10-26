using RMS.Models;
using RMS.Models.DTO;

namespace RMS.DAL
{
    public interface IRequestDal
    {
        Task<PagedResult<Requests>> Get(RequestQuery query, CancellationToken ct);
        Task<Requests?> Get(int id, CancellationToken ct);
        Task<SummaryDto> GetSummary(CancellationToken ct);
        Task<List<StatusHistories>> GetHistory(int requestId, CancellationToken ct);
        Task UpdateStatus(Requests entity, RequestStatus newStatus, int expectedVersion, string changedBy, CancellationToken ct);
        Task<int> Seed(int count, bool reset, CancellationToken ct);
    }
}
