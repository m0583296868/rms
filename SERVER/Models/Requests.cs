namespace RMS.Models
{
    public class Requests
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string OrganizationName { get; set; } = "";
        public RequestStatus Status { get; set; }
        public RequestPriority Priority { get; set; }
        public string AssignedTo { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int Version { get; set; } = 1;
        public ICollection<StatusHistories> History { get; set; } = new List<StatusHistories>();
    }
}
