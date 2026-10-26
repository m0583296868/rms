namespace RMS.Models.DTO
{
    public class SummaryDto
    {
        public int Total { get; set; }
        public List<CountItemDto> ByStatus { get; set; } = new();
        public List<CountItemDto> ByPriority { get; set; } = new();
    }

    public class CountItemDto
    {
        public string Key { get; set; } = "";
        public int Count { get; set; }
    }
}
