using Microsoft.EntityFrameworkCore;
using RMS.Models;
using RMS.Models.DTO;

namespace RMS.DAL
{
    public class RequestDal : IRequestDal
    {
        private readonly RmsDBContext rmsDBContext;

        public RequestDal(RmsDBContext rmsDBContext)
        {
            this.rmsDBContext = rmsDBContext;
        }

        public async Task<PagedResult<Requests>> Get(RequestQuery query, CancellationToken ct)
        {
            IQueryable<Requests> rows = rmsDBContext.requests.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                var status = Enum.Parse<RequestStatus>(query.Status, true);
                rows = rows.Where(r => r.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(query.Priority))
            {
                var priority = Enum.Parse<RequestPriority>(query.Priority, true);
                rows = rows.Where(r => r.Priority == priority);
            }

            if (!string.IsNullOrWhiteSpace(query.OrganizationName))
            {
                var org = query.OrganizationName.Trim();
                rows = rows.Where(r => r.OrganizationName.Contains(org));
            }

            if (!string.IsNullOrWhiteSpace(query.AssignedTo))
            {
                var assigned = query.AssignedTo.Trim();
                rows = rows.Where(r => r.AssignedTo == assigned);
            }

            if (query.From.HasValue)
            {
                var from = query.From.Value.Date;
                rows = rows.Where(r => r.CreatedAt >= from);
            }

            if (query.To.HasValue)
            {
                var to = query.To.Value.Date.AddDays(1);
                rows = rows.Where(r => r.CreatedAt < to);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                rows = rows.Where(r => r.Title.Contains(search) || r.OrganizationName.Contains(search));
            }

            var total = await rows.CountAsync(ct);
            var desc = query.SortDir.Equals("desc", StringComparison.OrdinalIgnoreCase);
            rows = ApplySort(rows, query.SortBy, desc);

            var items = await rows
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(ct);

            return new PagedResult<Requests>
            {
                Items = items,
                TotalCount = total,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        public Task<Requests?> Get(int id, CancellationToken ct)
        {
            return rmsDBContext.requests.FirstOrDefaultAsync(r => r.Id == id, ct);
        }

        public async Task<SummaryDto> GetSummary(CancellationToken ct)
        {
            var byStatus = await rmsDBContext.requests.AsNoTracking()
                .GroupBy(r => r.Status)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var byPriority = await rmsDBContext.requests.AsNoTracking()
                .GroupBy(r => r.Priority)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync(ct);

            return new SummaryDto
            {
                ByStatus = byStatus.Select(x => new CountItemDto { Key = x.Key.ToString(), Count = x.Count }).ToList(),
                ByPriority = byPriority.Select(x => new CountItemDto { Key = x.Key.ToString(), Count = x.Count }).ToList(),
                Total = byStatus.Sum(x => x.Count)
            };
        }


        public Task<List<StatusHistories>> GetHistory(int requestId, CancellationToken ct)
        {
            return rmsDBContext.statusHistories.AsNoTracking()
                .Where(h => h.RequestId == requestId)
                .OrderByDescending(h => h.ChangedAt)
                .ThenByDescending(h => h.Id)
                .ToListAsync(ct);
        }



        public async Task UpdateStatus(Requests entity, RequestStatus newStatus, int expectedVersion, string changedBy, CancellationToken ct)
        {
            var previous = entity.Status;
            var now = DateTime.UtcNow;

            rmsDBContext.Entry(entity).Property(r => r.Version).OriginalValue = expectedVersion;
            entity.Status = newStatus;
            entity.UpdatedAt = now;
            entity.Version = expectedVersion + 1;

            rmsDBContext.statusHistories.Add(new StatusHistories
            {
                RequestId = entity.Id,
                PreviousStatus = previous,
                NewStatus = newStatus,
                ChangedAt = now,
                ChangedBy = changedBy
            });

            await rmsDBContext.SaveChangesAsync(ct);
        }

        public async Task<int> Seed(int count, bool reset, CancellationToken ct)
        {
            rmsDBContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));

            if (reset)
            {
                await rmsDBContext.statusHistories.ExecuteDeleteAsync(ct);
                await rmsDBContext.requests.ExecuteDeleteAsync(ct);
            }

            var rng = new Random(42);
            var now = DateTime.UtcNow;
            const int batchSize = 2000;
            var buffer = new List<Requests>(batchSize);

            for (var i = 1; i <= count; i++)
            {
                var created = now.AddDays(-rng.Next(0, 400)).AddMinutes(-rng.Next(0, 24 * 60));
                buffer.Add(new Requests
                {
                    Title = $"{Topics[rng.Next(Topics.Length)]} {i}",
                    OrganizationName = Organizations[rng.Next(Organizations.Length)],
                    Status = (RequestStatus)rng.Next(0, 4),
                    Priority = (RequestPriority)rng.Next(0, 3),
                    AssignedTo = Assignees[rng.Next(Assignees.Length)],
                    CreatedAt = created,
                    UpdatedAt = created,
                    Version = 1
                });

                if (buffer.Count == batchSize)
                    await SaveBatch(buffer, ct);
            }

            if (buffer.Count > 0)
                await SaveBatch(buffer, ct);

            return count;
        }

        private async Task SaveBatch(List<Requests> buffer, CancellationToken ct)
        {
            await rmsDBContext.requests.AddRangeAsync(buffer, ct);
            await rmsDBContext.SaveChangesAsync(ct);
            rmsDBContext.ChangeTracker.Clear();
            buffer.Clear();
        }

        private static IQueryable<Requests> ApplySort(IQueryable<Requests> query, string sortBy, bool desc)
        {
            IOrderedQueryable<Requests> ordered = (sortBy, desc) switch
            {
                ("title", false) => query.OrderBy(r => r.Title),
                ("title", true) => query.OrderByDescending(r => r.Title),
                ("organizationName", false) => query.OrderBy(r => r.OrganizationName),
                ("organizationName", true) => query.OrderByDescending(r => r.OrganizationName),
                ("priority", false) => query.OrderBy(r => r.Priority),
                ("priority", true) => query.OrderByDescending(r => r.Priority),
                ("status", false) => query.OrderBy(r => r.Status),
                ("status", true) => query.OrderByDescending(r => r.Status),
                ("updatedAt", false) => query.OrderBy(r => r.UpdatedAt),
                ("updatedAt", true) => query.OrderByDescending(r => r.UpdatedAt),
                ("createdAt", true) => query.OrderByDescending(r => r.CreatedAt),
                _ => query.OrderBy(r => r.CreatedAt)
            };

            return ordered.ThenBy(r => r.Id);
        }

        private static readonly string[] Organizations =
        {
            "עיריית תל אביב", "משרד החינוך", "רכבת ישראל", "שירותי בריאות כללית","המשרד להגנת הסביבה",
            "בנק הפועלים", "חברת חשמל", "רשות המסים", "אוניברסיטת תל אביב",
            "עיריית חיפה", "משרד התחבורה", "קופת חולים מכבי", "אל על",
            "נמל אשדוד", "עיריית ירושלים", "הטכניון", "דואר ישראל",
            "משרד הבריאות", "עיריית באר שבע", "רכבת קלה", "תעשייה אווירית"
        };

        private static readonly string[] Assignees =
        {
            "דינה כהן", "יוסי לוי", "מיכל אברהם", "נועה כהן", "אורי מזרחי",
            "שירה שץ", "דוד לוי", "הילה ביטון", "טלי אזולאי", "שושי כץ"
        };

        private static readonly string[] Topics =
        {
            "בקשה לחיבור מערכת", "תקלה בדיווח", "עדכון הרשאות", "פתיחת משתמש",
            "בקשת הדרכה", "שדרוג גרסה", "בדיקת נתונים", "הקמת ממשק",
            "דיווח חריגה", "בקשה לדוח"
        };
    }
}
