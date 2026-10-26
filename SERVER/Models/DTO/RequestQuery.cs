namespace RMS.Models.DTO
{
    public class RequestQuery
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public string? OrganizationName { get; set; }
        public string? AssignedTo { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string? Search { get; set; }
        public string SortBy { get; set; } = "createdAt";
        public string SortDir { get; set; } = "desc";
    }
}
