using Microsoft.AspNetCore.Mvc;
using RMS.BL;
using RMS.Models.DTO;

namespace RMS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RequestController : ControllerBase
    {
        private readonly IRequestService requestService;

        public RequestController(IRequestService requestService)
        {
            this.requestService = requestService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<RequestDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public Task<PagedResult<RequestDto>> Get([FromQuery] RequestQuery query, CancellationToken ct)
        {
            return requestService.Get(query, ct);
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public Task<RequestDto> Get(int id, CancellationToken ct)
        {
            return requestService.Get(id, ct);
        }

        [HttpGet("summary")]
        [ProducesResponseType(typeof(SummaryDto), StatusCodes.Status200OK)]
        public Task<SummaryDto> Summary(CancellationToken ct)
        {
            return requestService.GetSummary(ct);
        }

        [HttpGet("{id:int}/history")]
        [ProducesResponseType(typeof(List<StatusHistoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public Task<List<StatusHistoryDto>> History(int id, CancellationToken ct)
        {
            return requestService.GetHistory(id, ct);
        }

        [HttpPut("{id:int}/status")]
        [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public Task<RequestDto> UpdateStatus(int id, [FromBody] UpdateStatusDto dto, CancellationToken ct)
        {
            return requestService.UpdateStatus(id, dto, ct);
        }

        [HttpPost("bulk-status")]
        [ProducesResponseType(typeof(BulkStatusResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public Task<BulkStatusResultDto> BulkStatus([FromBody] BulkStatusDto dto, CancellationToken ct)
        {
            return requestService.UpdateBulk(dto, ct);
        }

        [HttpPost("seed")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Seed([FromBody] SeedDto dto, CancellationToken ct)
        {
            var count = await requestService.Seed(dto, ct);
            return Ok(new { count });
        }
    }
}
