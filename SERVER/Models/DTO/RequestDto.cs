namespace RMS.Models.DTO
{
    public class RequestDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string OrganizationName { get; set; } = "";
        public string Status { get; set; } = "";
        public string Priority { get; set; } = "";
        public string AssignedTo { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int Version { get; set; }
    }
}
