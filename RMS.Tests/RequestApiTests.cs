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

        [Fact]
        public async Task OrganizationFilter_MatchesThePrefix_SearchMatchesAnywhere()
        {
            await SeedSample();

            var prefix = await client.GetFromJsonAsync<PagedResult<RequestDto>>(
                "/api/Request?organizationName=" + Uri.EscapeDataString("רכבת") + "&pageSize=10", Json);
            Assert.Equal(2, prefix!.TotalCount);
            Assert.All(prefix.Items, item => Assert.StartsWith("רכבת", item.OrganizationName));

            var insideName = await client.GetFromJsonAsync<PagedResult<RequestDto>>(
                "/api/Request?organizationName=" + Uri.EscapeDataString("ישראל") + "&pageSize=10", Json);
            Assert.Equal(0, insideName!.TotalCount);

            var search = await client.GetFromJsonAsync<PagedResult<RequestDto>>(
                "/api/Request?search=" + Uri.EscapeDataString("ישראל") + "&pageSize=10", Json);
            Assert.Equal(3, search!.TotalCount);
            Assert.All(search.Items, item => Assert.Contains("ישראל", item.OrganizationName));
        }

        [Fact]
        public async Task ParallelUpdates_DifferentTargets_KeepOneVersionAndOneHistory()
        {
            var rows = await SeedSample();
            var id = rows.First(r => r.Status == RequestStatus.New).Id;
            var results = await RunTogether(
                () => client.PutAsJsonAsync($"/api/Request/{id}/status", new UpdateStatusDto
                {
                    Status = "InProgress",
                    Version = 1,
                    ChangedBy = "דנה כהן"
                }),
                () => client.PutAsJsonAsync($"/api/Request/{id}/status", new UpdateStatusDto
                {
                    Status = "Waiting",
                    Version = 1,
                    ChangedBy = "יוסי לוי"
                }));

            var winner = results.Single(r => r.StatusCode == HttpStatusCode.OK);
            var loser = results.Single(r => r.StatusCode == HttpStatusCode.Conflict);
            var winnerBody = await winner.Content.ReadFromJsonAsync<RequestDto>(Json);
            var problem = await loser.Content.ReadFromJsonAsync<ProblemBody>(Json);
            Assert.Contains("עודכנה", problem!.Detail);

            var current = await client.GetFromJsonAsync<RequestDto>($"/api/Request/{id}", Json);
            Assert.Equal(2, current!.Version);
            Assert.Equal(winnerBody!.Status, current.Status);
            Assert.Contains(current.Status, new[] { "InProgress", "Waiting" });

            var history = await client.GetFromJsonAsync<List<StatusHistoryDto>>($"/api/Request/{id}/history", Json);
            Assert.NotNull(history);
            var saved = Assert.Single(history);
            Assert.Equal("New", saved.PreviousStatus);
            Assert.Equal(current.Status, saved.NewStatus);
            Assert.Equal(current.Status == "InProgress" ? "דנה כהן" : "יוסי לוי", saved.ChangedBy);

            var stale = await client.PutAsJsonAsync($"/api/Request/{id}/status", new UpdateStatusDto
            {
                Status = current.Status == "InProgress" ? "Waiting" : "InProgress",
                Version = 1,
                ChangedBy = "נועה ברק"
            });
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

            var afterStale = await client.GetFromJsonAsync<RequestDto>($"/api/Request/{id}", Json);
            Assert.Equal(current.Status, afterStale!.Status);
            Assert.Equal(2, afterStale.Version);
        }

        [Fact]
        public async Task ParallelUpdates_DifferentRows_BothSucceed()
        {
            var rows = await SeedSample();
            var news = rows.Where(r => r.Status == RequestStatus.New).Take(2).ToList();
            var results = await RunTogether(
                () => client.PutAsJsonAsync($"/api/Request/{news[0].Id}/status", new UpdateStatusDto
                {
                    Status = "InProgress",
                    Version = 1,
                    ChangedBy = "דנה כהן"
                }),
                () => client.PutAsJsonAsync($"/api/Request/{news[1].Id}/status", new UpdateStatusDto
                {
                    Status = "Waiting",
                    Version = 1,
                    ChangedBy = "יוסי לוי"
                }));

            Assert.All(results, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

            var first = await client.GetFromJsonAsync<RequestDto>($"/api/Request/{news[0].Id}", Json);
            var second = await client.GetFromJsonAsync<RequestDto>($"/api/Request/{news[1].Id}", Json);
            Assert.Equal("InProgress", first!.Status);
            Assert.Equal(2, first.Version);
            Assert.Equal("Waiting", second!.Status);
            Assert.Equal(2, second.Version);

            var firstHistory = await client.GetFromJsonAsync<List<StatusHistoryDto>>($"/api/Request/{news[0].Id}/history", Json);
            var secondHistory = await client.GetFromJsonAsync<List<StatusHistoryDto>>($"/api/Request/{news[1].Id}/history", Json);
            Assert.NotNull(firstHistory);
            Assert.NotNull(secondHistory);
            Assert.Equal("דנה כהן", Assert.Single(firstHistory).ChangedBy);
            Assert.Equal("יוסי לוי", Assert.Single(secondHistory).ChangedBy);
        }

        [Fact]
        public async Task ParallelBulkAndSingleUpdate_DoNotOverwriteEachOther()
        {
            var rows = await SeedSample();
            var contested = rows.First(r => r.Status == RequestStatus.New);
            var other = rows.First(r => r.Status == RequestStatus.Waiting);
            var results = await RunTogether(
                () => client.PutAsJsonAsync($"/api/Request/{contested.Id}/status", new UpdateStatusDto
                {
                    Status = "Waiting",
                    Version = 1,
                    ChangedBy = "דנה כהן"
                }),
                () => client.PostAsJsonAsync("/api/Request/bulk-status", new BulkStatusDto
                {
                    ChangedBy = "יוסי לוי",
                    Items = new List<BulkStatusItemDto>
                    {
                        new() { Id = contested.Id, Status = "InProgress", Version = 1 },
                        new() { Id = other.Id, Status = "Completed", Version = 1 }
                    }
                }));

            results[1].EnsureSuccessStatusCode();
            var bulk = await results[1].Content.ReadFromJsonAsync<BulkStatusResultDto>(Json);
            var contestedNow = await client.GetFromJsonAsync<RequestDto>($"/api/Request/{contested.Id}", Json);
            var otherNow = await client.GetFromJsonAsync<RequestDto>($"/api/Request/{other.Id}", Json);
            var contestedHistory = await client.GetFromJsonAsync<List<StatusHistoryDto>>($"/api/Request/{contested.Id}/history", Json);
            var otherHistory = await client.GetFromJsonAsync<List<StatusHistoryDto>>($"/api/Request/{other.Id}/history", Json);

            Assert.NotNull(contestedNow);
            Assert.NotNull(otherNow);
            Assert.NotNull(contestedHistory);
            Assert.NotNull(otherHistory);
            Assert.NotNull(bulk);
            Assert.Equal(2, contestedNow.Version);
            var contestedSaved = Assert.Single(contestedHistory);
            Assert.Equal(contestedNow.Status, contestedSaved.NewStatus);

            if (results[0].StatusCode == HttpStatusCode.OK)
            {
                Assert.Equal("Waiting", contestedNow.Status);
                Assert.Equal("דנה כהן", contestedSaved.ChangedBy);
                Assert.Empty(bulk.Succeeded);
                Assert.All(bulk.Failed, item => Assert.Equal("Conflict", item.Reason));
                Assert.Equal("Waiting", otherNow.Status);
                Assert.Equal(1, otherNow.Version);
                Assert.Empty(otherHistory);
            }
            else
            {
                Assert.Equal(HttpStatusCode.Conflict, results[0].StatusCode);
                Assert.Equal(2, bulk.Succeeded.Count);
                Assert.Empty(bulk.Failed);
                Assert.Equal("InProgress", contestedNow.Status);
                Assert.Equal("יוסי לוי", contestedSaved.ChangedBy);
                Assert.Equal("Completed", otherNow.Status);
                Assert.Equal(2, otherNow.Version);
                var otherSaved = Assert.Single(otherHistory);
                Assert.Equal("Completed", otherSaved.NewStatus);
                Assert.Equal("יוסי לוי", otherSaved.ChangedBy);
            }
        }

        [Fact]
        public async Task Bulk_StaleAndMissing_DoNotCancelValidRows()
        {
            var rows = await SeedSample();
            var stale = rows.First(r => r.Status == RequestStatus.InProgress);
            var moved = await client.PutAsJsonAsync($"/api/Request/{stale.Id}/status", new UpdateStatusDto
            {
                Status = "Waiting",
                Version = 1,
                ChangedBy = "דנה כהן"
            });
            moved.EnsureSuccessStatusCode();

            var validNew = rows.First(r => r.Status == RequestStatus.New);
            var validWaiting = rows.First(r => r.Status == RequestStatus.Waiting);
            var response = await client.PostAsJsonAsync("/api/Request/bulk-status", new BulkStatusDto
            {
                ChangedBy = "יוסי לוי",
                Items = new List<BulkStatusItemDto>
                {
                    new() { Id = validNew.Id, Status = "InProgress", Version = 1 },
                    new() { Id = validWaiting.Id, Status = "Completed", Version = 1 },
                    new() { Id = stale.Id, Status = "Completed", Version = 1 },
                    new() { Id = 999999, Status = "InProgress", Version = 1 }
                }
            });

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<BulkStatusResultDto>(Json);
            Assert.Equal(2, result!.Succeeded.Count);
            Assert.Contains(result.Failed, item => item.Id == stale.Id && item.Reason == "Conflict");
            Assert.Contains(result.Failed, item => item.Id == 999999 && item.Reason == "NotFound");

            var history = await client.GetFromJsonAsync<List<StatusHistoryDto>>($"/api/Request/{validNew.Id}/history", Json);
            Assert.Single(history!);
        }

        [Fact]
        public async Task Bulk_ConflictDuringSave_RollsBackEveryAcceptedRow()
        {
            var rows = await SeedSample();
            var first = rows.First(r => r.Status == RequestStatus.New);
            var second = rows.First(r => r.Status == RequestStatus.Waiting);
            TestSaveInterceptor.ConflictRequestId = first.Id;

            BulkStatusResultDto? result;
            try
            {
                var response = await client.PostAsJsonAsync("/api/Request/bulk-status", new BulkStatusDto
                {
                    ChangedBy = "יוסי לוי",
                    Items = new List<BulkStatusItemDto>
                    {
                        new() { Id = first.Id, Status = "InProgress", Version = 1 },
                        new() { Id = second.Id, Status = "Completed", Version = 1 }
                    }
                });
                response.EnsureSuccessStatusCode();
                result = await response.Content.ReadFromJsonAsync<BulkStatusResultDto>(Json);
            }
            finally
            {
                TestSaveInterceptor.ConflictRequestId = 0;
            }

            Assert.Empty(result!.Succeeded);
            Assert.Equal(2, result.Failed.Count);
            Assert.All(result.Failed, item => Assert.Equal("Conflict", item.Reason));

            var firstNow = await client.GetFromJsonAsync<RequestDto>($"/api/Request/{first.Id}", Json);
            var secondNow = await client.GetFromJsonAsync<RequestDto>($"/api/Request/{second.Id}", Json);
            Assert.Equal("New", firstNow!.Status);
            Assert.Equal(1, firstNow.Version);
            Assert.Equal("Waiting", secondNow!.Status);
            Assert.Equal(1, secondNow.Version);

            var history = await client.GetFromJsonAsync<List<StatusHistoryDto>>($"/api/Request/{second.Id}/history", Json);
            Assert.Empty(history!);
        }

        [Fact]
        public async Task Summary_AfterStatusChange_ReturnsUpdatedCounts()
        {
            await SeedSample();
            var before = await client.GetFromJsonAsync<SummaryDto>("/api/Request/summary", Json);
            var cached = await client.GetFromJsonAsync<SummaryDto>("/api/Request/summary", Json);
            Assert.Equal(before!.Total, cached!.Total);

            var page = await client.GetFromJsonAsync<PagedResult<RequestDto>>("/api/Request?status=New&pageSize=1", Json);
            var row = page!.Items[0];
            var update = await client.PutAsJsonAsync($"/api/Request/{row.Id}/status", new UpdateStatusDto
            {
                Status = "InProgress",
                Version = row.Version,
                ChangedBy = "דנה כהן"
            });
            update.EnsureSuccessStatusCode();

            var after = await client.GetFromJsonAsync<SummaryDto>("/api/Request/summary", Json);
            Assert.NotNull(after);
            Assert.Equal(CountOf(before, "New") - 1, CountOf(after, "New"));
            Assert.Equal(CountOf(before, "InProgress") + 1, CountOf(after, "InProgress"));
        }

        private async Task<HttpResponseMessage[]> RunTogether(
            Func<Task<HttpResponseMessage>> first,
            Func<Task<HttpResponseMessage>> second)
        {
            var gate = new Barrier(2);
            TestSaveInterceptor.Gate = gate;
            try
            {
                return await Task.WhenAll(first(), second());
            }
            finally
            {
                TestSaveInterceptor.Gate = null;
                gate.Dispose();
            }
        }

        private static int CountOf(SummaryDto summary, string key)
        {
            return summary.ByStatus.FirstOrDefault(item => item.Key == key)?.Count ?? 0;
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
