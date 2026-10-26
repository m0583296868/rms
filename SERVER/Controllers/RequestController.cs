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

        // פונקציית קבלת ערך דף הפניות לפי סינון
        [HttpGet]
        public Task<PagedResult<RequestDto>> Get([FromQuery] RequestQuery query, CancellationToken ct)
        {
            return requestService.Get(query, ct);
        }

        //פונקציית קבלת פנייה מסוימת לפי מזהה
        [HttpGet("{id:int}")]
        public Task<RequestDto> Get(int id, CancellationToken ct)
        {
            return requestService.Get(id, ct);
        }

        //פונקציית קבלת סכום של פניות לפי סטטוס ועדיפות
        [HttpGet("summary")]
        public Task<SummaryDto> Summary(CancellationToken ct)
        {
            return requestService.GetSummary(ct);
        }

        //פונקציית קבלת הסטורייה של פנייה מסוימת
        [HttpGet("{id:int}/history")]
        public Task<List<StatusHistoryDto>> History(int id, CancellationToken ct)
        {
            return requestService.GetHistory(id, ct);
        }

        //פונקציית עדכון סטטוס של פנייה מסוימת
        [HttpPut("{id:int}/status")]
        public Task<RequestDto> UpdateStatus(int id, [FromBody] UpdateStatusDto dto, CancellationToken ct)
        {
            return requestService.UpdateStatus(id, dto, ct);
        }

        //פונקציית עדכון מספר פניות בזמן אחד
        [HttpPost("bulk-status")]
        public Task<BulkStatusResultDto> BulkStatus([FromBody] BulkStatusDto dto, CancellationToken ct)
        {
            return requestService.UpdateBulk(dto, ct);
        }

        //פונקצייה שיוצרת פניות חדשות לפי הערכים שבאובייקט שקיבלה
        [HttpPost("seed")]
        public async Task<IActionResult> Seed([FromBody] SeedDto dto, CancellationToken ct)
        {
            var count = await requestService.Seed(dto, ct);
            return Ok(new { count });
        }
    }
}
