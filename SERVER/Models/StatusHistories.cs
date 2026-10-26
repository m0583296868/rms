namespace RMS.Models
{
    public class StatusHistories
    {
        public int Id { get; set; }
        public int RequestId { get; set; }
        public RequestStatus PreviousStatus { get; set; }
        public RequestStatus NewStatus { get; set; }
        public DateTime ChangedAt { get; set; }
        public string ChangedBy { get; set; } = "";
        public Requests? Request { get; set; }
    }
}
