namespace RMS.Models.DTO
{
    public class UpdateStatusDto
    {
        public string Status { get; set; } = "";
        public int Version { get; set; }
        public string ChangedBy { get; set; } = "";
    }
}
