using RMS.Models;

namespace RMS.BL
{
    public static class StatusRules
    {
        private static readonly Dictionary<RequestStatus, RequestStatus[]> Allowed = new()
        {
            [RequestStatus.New] = new[] { RequestStatus.InProgress, RequestStatus.Waiting },
            [RequestStatus.InProgress] = new[] { RequestStatus.Waiting, RequestStatus.Completed },
            [RequestStatus.Waiting] = new[] { RequestStatus.InProgress, RequestStatus.Completed },
            [RequestStatus.Completed] = Array.Empty<RequestStatus>()
        };

        public static bool CanMove(RequestStatus from, RequestStatus to)
        {
            return Allowed.TryGetValue(from, out var next) && next.Contains(to);
        }

        public static bool TryParseStatus(string? value, out RequestStatus status)
        {
            return TryParse(value, out status);
        }

        public static bool TryParsePriority(string? value, out RequestPriority priority)
        {
            return TryParse(value, out priority);
        }

        private static bool TryParse<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum
        {
            result = default;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var text = value.Trim();
            if (!Enum.TryParse(text, true, out result))
                return false;

            return Enum.GetNames<TEnum>().Any(name => name.Equals(text, StringComparison.OrdinalIgnoreCase));
        }
    }
}
