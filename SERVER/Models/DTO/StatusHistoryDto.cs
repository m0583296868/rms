namespace RMS.Models.DTO
{
    public class StatusHistoryDto
    {
        public int Id { get; set; }
        public int RequestId { get; set; }
        public string PreviousStatus { get; set; } = "";
        public string NewStatus { get; set; } = "";
        public DateTime ChangedAt { get; set; }
        public string ChangedBy { get; set; } = "";
    }
}
