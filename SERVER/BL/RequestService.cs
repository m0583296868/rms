using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RMS.DAL;
using RMS.Models;
using RMS.Models.DTO;
using System.Diagnostics;

namespace RMS.BL
{
    public class RequestService : IRequestService
    {
        public const string SummaryCacheKey = "requests-summary";
        private static readonly TimeSpan SummaryCacheTime = TimeSpan.FromMinutes(2);
        private static readonly string[] SortFields = { "createdAt", "updatedAt", "title", "organizationName", "priority", "status" };

        private readonly IRequestDal requestDal;
        private readonly IMapper mapper;
        private readonly IMemoryCache cache;
        private readonly ILogger<RequestService> logger;

        public RequestService(
            IRequestDal requestDal,
            IMapper mapper,
            IMemoryCache cache,
            ILogger<RequestService> logger)
        {
            this.requestDal = requestDal;
            this.mapper = mapper;
            this.cache = cache;
            this.logger = logger;
        }

        public async Task<PagedResult<RequestDto>> Get(RequestQuery query, CancellationToken ct)
        {
            ValidateQuery(query);
            var watch = Stopwatch.StartNew();
            var page = await requestDal.Get(query, ct);
            watch.Stop();
            logger.LogInformation(
                "List query page {Page} size {PageSize} returned {Count} of {Total} in {Ms} ms",
                page.Page, page.PageSize, page.Items.Count, page.TotalCount, watch.ElapsedMilliseconds);

            return new PagedResult<RequestDto>
            {
                Items = mapper.Map<List<RequestDto>>(page.Items),
                TotalCount = page.TotalCount,
                Page = page.Page,
                PageSize = page.PageSize
            };
        }

        public async Task<RequestDto> Get(int id, CancellationToken ct)
        {
            var entity = await requestDal.Get(id, ct);
            if (entity == null)
                throw new NotFoundException("הפנייה לא נמצאה");
            return mapper.Map<RequestDto>(entity);
        }

        public async Task<SummaryDto> GetSummary(CancellationToken ct)
        {
            if (cache.TryGetValue(SummaryCacheKey, out SummaryDto? cached) && cached != null)
            {
                logger.LogInformation("Summary cache hit");
                return cached;
            }

            var watch = Stopwatch.StartNew();
            var summary = await requestDal.GetSummary(ct);
            watch.Stop();
            logger.LogInformation("Summary aggregation took {Ms} ms", watch.ElapsedMilliseconds);

            cache.Set(SummaryCacheKey, summary, SummaryCacheTime);
            return summary;
        }

        public async Task<List<StatusHistoryDto>> GetHistory(int id, CancellationToken ct)
        {
            var entity = await requestDal.Get(id, ct);
            if (entity == null)
                throw new NotFoundException("הפנייה לא נמצאה");

            var history = await requestDal.GetHistory(id, ct);
            return mapper.Map<List<StatusHistoryDto>>(history);
        }

        public async Task<RequestDto> UpdateStatus(int id, UpdateStatusDto dto, CancellationToken ct)
        {
            if (dto == null)
                throw new AppValidationException("חסר גוף בקשה");

            var entity = await UpdateOne(requestDal, id, dto.Status, dto.Version, dto.ChangedBy, ct);
            cache.Remove(SummaryCacheKey);
            return mapper.Map<RequestDto>(entity);
        }



        public async Task<BulkStatusResultDto> UpdateBulk(BulkStatusDto dto, CancellationToken ct)
        {
            if (dto == null)
                throw new AppValidationException("חסר גוף בקשה");
            if (dto.Items == null || dto.Items.Count == 0)
                throw new AppValidationException("יש לבחור לפחות פנייה אחת");
            if (dto.Items.Count > 100)
                throw new AppValidationException("ניתן לעדכן עד 100 פניות בבקשה");
            if (dto.Items.Select(i => i.Id).Distinct().Count() != dto.Items.Count)
                throw new AppValidationException("אותה פנייה מופיעה פעמיים בבקשה");
            if (string.IsNullOrWhiteSpace(dto.ChangedBy))
                throw new AppValidationException("יש להזין מי ביצע את העדכון");
            if (dto.ChangedBy.Trim().Length > 100)
                throw new AppValidationException("יש להזין שם מבצע עד 100 תווים");

            var changedBy = dto.ChangedBy.Trim();
            var entities = await requestDal.GetByIds(dto.Items.Select(i => i.Id).ToList(), ct);
            var byId = entities.ToDictionary(r => r.Id);
            var result = new BulkStatusResultDto();
            var changes = new List<RequestStatusChange>();

            foreach (var item in dto.Items)
            {
                if (!StatusRules.TryParseStatus(item.Status, out var newStatus))
                {
                    result.Failed.Add(new BulkFailureDto { Id = item.Id, Reason = "סטטוס לא חוקי" });
                    continue;
                }
                if (item.Version < 1)
                {
                    result.Failed.Add(new BulkFailureDto { Id = item.Id, Reason = "גרסה לא תקינה" });
                    continue;
                }
                if (!byId.TryGetValue(item.Id, out var entity))
                {
                    result.Failed.Add(new BulkFailureDto { Id = item.Id, Reason = "NotFound" });
                    continue;
                }
                if (entity.Version != item.Version)
                {
                    result.Failed.Add(new BulkFailureDto { Id = item.Id, Reason = "Conflict" });
                    continue;
                }
                if (!StatusRules.CanMove(entity.Status, newStatus))
                {
                    result.Failed.Add(new BulkFailureDto { Id = item.Id, Reason = $"לא ניתן לעבור מסטטוס {entity.Status} לסטטוס {newStatus}" });
                    continue;
                }

                changes.Add(new RequestStatusChange(entity, newStatus, item.Version));
            }

            try
            {
                await requestDal.UpdateStatuses(changes, changedBy, ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                foreach (var change in changes)
                    result.Failed.Add(new BulkFailureDto { Id = change.Entity.Id, Reason = "Conflict" });

                logger.LogInformation(
                    "Bulk status update succeeded {Ok} failed {Fail}",
                    result.Succeeded.Count, result.Failed.Count);
                return result;
            }

            foreach (var change in changes)
            {
                logger.LogInformation(
                    "Request {Id} moved to {Status} by {User}",
                    change.Entity.Id, change.NewStatus, changedBy);
                result.Succeeded.Add(mapper.Map<RequestDto>(change.Entity));
            }

            if (result.Succeeded.Count > 0)
                cache.Remove(SummaryCacheKey);

            logger.LogInformation(
                "Bulk status update succeeded {Ok} failed {Fail}",
                result.Succeeded.Count, result.Failed.Count);
            return result;
        }

        public async Task<int> Seed(SeedDto dto, CancellationToken ct)
        {
            if (dto == null)
                throw new AppValidationException("חסר גוף בקשה");
            if (dto.Count < 1 || dto.Count > 200000)
                throw new AppValidationException("כמות הרשומות חייבת להיות בין 1 ל-200000");

            logger.LogInformation("Seed started. Count {Count} reset {Reset}", dto.Count, dto.Reset);
            var watch = Stopwatch.StartNew();
            var count = await requestDal.Seed(dto.Count, dto.Reset, ct);
            watch.Stop();
            cache.Remove(SummaryCacheKey);
            logger.LogInformation("Seed finished. {Count} rows in {Ms} ms", count, watch.ElapsedMilliseconds);
            return count;
        }


        private async Task<Requests> UpdateOne(
            IRequestDal dal, int id, string status, int version, string changedBy, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(changedBy) || changedBy.Trim().Length > 100)
                throw new AppValidationException("יש להזין שם מבצע עד 100 תווים");
            if (!StatusRules.TryParseStatus(status, out var newStatus))
                throw new AppValidationException("סטטוס לא חוקי");
            if (version < 1)
                throw new AppValidationException("גרסה לא תקינה");

            var entity = await dal.Get(id, ct);
            if (entity == null)
                throw new NotFoundException("הפנייה לא נמצאה");
            if (entity.Version != version)
                throw new ConflictException("הפנייה עודכנה על ידי משתמש אחר. רענן ונסה שוב.");
            if (!StatusRules.CanMove(entity.Status, newStatus))
                throw new AppValidationException($"לא ניתן לעבור מסטטוס {entity.Status} לסטטוס {newStatus}");

            try
            {
                await dal.UpdateStatus(entity, newStatus, version, changedBy.Trim(), ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException("הפנייה עודכנה על ידי משתמש אחר. רענן ונסה שוב.");
            }

            logger.LogInformation("Request {Id} moved to {Status} by {User}", id, newStatus, changedBy.Trim());
            return entity;
        }


//פנקציית בדיקת תקינות הquery
        private static void ValidateQuery(RequestQuery query)
        {
            if (query.Page < 1)
                throw new AppValidationException("מספר עמוד חייב להיות 1 ומעלה");
            if (query.PageSize < 1 || query.PageSize > 100)
                throw new AppValidationException("גודל עמוד חייב להיות בין 1 ל-100");
            if (query.From.HasValue && query.To.HasValue && query.From.Value.Date > query.To.Value.Date)
                throw new AppValidationException("תאריך התחלה מאוחר מתאריך הסיום");
            if (!string.IsNullOrWhiteSpace(query.Search) && query.Search.Trim().Length > 100)
                throw new AppValidationException("החיפוש ארוך מדי");
            if (string.IsNullOrWhiteSpace(query.SortBy) || !SortFields.Contains(query.SortBy))
                throw new AppValidationException("שדה מיון לא נתמך");
            if (query.SortDir == null ||
                (!query.SortDir.Equals("asc", StringComparison.OrdinalIgnoreCase) &&
                 !query.SortDir.Equals("desc", StringComparison.OrdinalIgnoreCase)))
                throw new AppValidationException("כיוון מיון חייב להיות asc או desc");
            if (!string.IsNullOrWhiteSpace(query.Status) && !StatusRules.TryParseStatus(query.Status, out _))
                throw new AppValidationException("סטטוס לא חוקי");
            if (!string.IsNullOrWhiteSpace(query.Priority) && !StatusRules.TryParsePriority(query.Priority, out _))
                throw new AppValidationException("עדיפות לא חוקית");
        }
    }
}
