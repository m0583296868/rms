namespace RMS.Models.DTO
{
    public class BulkStatusDto
    {
        public string ChangedBy { get; set; } = "";
        public List<BulkStatusItemDto> Items { get; set; } = new();
    }

    public class BulkStatusItemDto
    {
        public int Id { get; set; }
        public string Status { get; set; } = "";
        public int Version { get; set; }
    }

    public class BulkStatusResultDto
    {
        public List<RequestDto> Succeeded { get; set; } = new();
        public List<BulkFailureDto> Failed { get; set; } = new();
    }

    public class BulkFailureDto
    {
        public int Id { get; set; }
        public string Reason { get; set; } = "";
    }
}
