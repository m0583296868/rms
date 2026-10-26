using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RMS.DAL;
using RMS.Models;
using RMS.Models.DTO;
using Xunit;

namespace RMS.Tests
{
    public class RequestApiTests : IClassFixture<ApiFactory>
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private readonly ApiFactory factory;
        private readonly HttpClient client;

        public RequestApiTests(ApiFactory factory)
        {
            this.factory = factory;
            client = factory.CreateClient();
        }

        [Fact]
        public async Task FilterAndPage_ReturnsOnlyTheMatchingPage()
        {
            await SeedSample();

            var response = await client.GetAsync("/api/Request?status=New&page=1&pageSize=2&sortBy=createdAt&sortDir=asc");
            response.EnsureSuccessStatusCode();

            var page = await response.Content.ReadFromJsonAsync<PagedResult<RequestDto>>(Json);
            Assert.NotNull(page);
            Assert.Equal(3, page!.TotalCount);
            Assert.Equal(2, page.Items.Count);
            Assert.All(page.Items, item => Assert.Equal("New", item.Status));
            Assert.Contains("תקלה בדיווח", page.Items[0].Title);
        }

        [Fact]
        public async Task InvalidPageSize_Returns400()
        {
            var response = await client.GetAsync("/api/Request?pageSize=101");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<ProblemBody>(Json);
            Assert.Contains("גודל עמוד", problem!.Detail);
        }

        [Fact]
        public async Task InvalidTransition_Returns400()
        {
            var rows = await SeedSample();
            var id = rows.First(r => r.Status == RequestStatus.New).Id;

            var response = await client.PutAsJsonAsync($"/api/Request/{id}/status", new UpdateStatusDto
            {
                Status = "Completed",
                Version = 1,
                ChangedBy = "דנה כהן"
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task StaleVersion_Returns409_AndHistoryIsSavedForTheWinner()
        {
            var rows = await SeedSample();
            var id = rows.First(r => r.Status == RequestStatus.New).Id;
            var body = new UpdateStatusDto { Status = "InProgress", Version = 1, ChangedBy = "דנה כהן" };

            var first = await client.PutAsJsonAsync($"/api/Request/{id}/status", body);
            first.EnsureSuccessStatusCode();
            var updated = await first.Content.ReadFromJsonAsync<RequestDto>(Json);
            Assert.Equal(2, updated!.Version);
            Assert.Equal("InProgress", updated.Status);

            var second = await client.PutAsJsonAsync($"/api/Request/{id}/status", body);
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

            var historyResponse = await client.GetAsync($"/api/Request/{id}/history");
            historyResponse.EnsureSuccessStatusCode();
            var history = await historyResponse.Content.ReadFromJsonAsync<List<StatusHistoryDto>>(Json);
            Assert.Single(history!);
            Assert.Equal("New", history![0].PreviousStatus);
            Assert.Equal("InProgress", history[0].NewStatus);
            Assert.Equal("דנה כהן", history[0].ChangedBy);
        }

        [Fact]
        public async Task MissingRequest_Returns404()
        {
            var response = await client.PutAsJsonAsync("/api/Request/999999/status", new UpdateStatusDto
            {
                Status = "InProgress",
                Version = 1,
                ChangedBy = "דנה כהן"
            });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Bulk_UpdatesTheValidRow_AndReportsTheMissingOne()
        {
            var rows = await SeedSample();
            var id = rows.First(r => r.Status == RequestStatus.Waiting).Id;

            var response = await client.PostAsJsonAsync("/api/Request/bulk-status", new BulkStatusDto
            {
                ChangedBy = "יוסי לוי",
                Items = new List<BulkStatusItemDto>
                {
                    new() { Id = id, Status = "Completed", Version = 1 },
                    new() { Id = 999999, Status = "InProgress", Version = 1 }
                }
            });

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<BulkStatusResultDto>(Json);
            Assert.Single(result!.Succeeded);
            Assert.Equal("Completed", result.Succeeded[0].Status);
            Assert.Single(result.Failed);
            Assert.Equal(999999, result.Failed[0].Id);
            Assert.Equal("NotFound", result.Failed[0].Reason);
        }

        private async Task<List<Requests>> SeedSample()
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RmsDBContext>();
            await db.statusHistories.ExecuteDeleteAsync();
            await db.requests.ExecuteDeleteAsync();

            var rows = new List<Requests>
            {
                Row("תקלה בדיווח 1", "רכבת ישראל", RequestStatus.New, RequestPriority.High, "דנה כהן", new DateTime(2026, 1, 10)),
                Row("פתיחת משתמש 2", "רכבת ישראל", RequestStatus.New, RequestPriority.Low, "יוסי לוי", new DateTime(2026, 2, 10)),
                Row("שדרוג גרסה 3", "אל על", RequestStatus.New, RequestPriority.Medium, "דנה כהן", new DateTime(2026, 3, 10)),
                Row("בקשה לדוח 4", "הטכניון", RequestStatus.Completed, RequestPriority.High, "נועה ברק", new DateTime(2026, 4, 10)),
                Row("הקמת ממשק 5", "דואר ישראל", RequestStatus.InProgress, RequestPriority.Low, "יוסי לוי", new DateTime(2026, 5, 10)),
                Row("בדיקת נתונים 6", "משרד החינוך", RequestStatus.Waiting, RequestPriority.Medium, "מיכל אברהם", new DateTime(2026, 6, 10))
            };

            db.requests.AddRange(rows);
            await db.SaveChangesAsync();
            return rows;
        }

        private static Requests Row(string title, string org, RequestStatus status, RequestPriority priority, string assigned, DateTime created)
        {
            return new Requests
            {
                Title = title,
                OrganizationName = org,
                Status = status,
                Priority = priority,
                AssignedTo = assigned,
                CreatedAt = created,
                UpdatedAt = created,
                Version = 1
            };
        }

        private sealed class ProblemBody
        {
            public string Detail { get; set; } = "";
        }
    }
}
