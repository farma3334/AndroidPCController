namespace AndroidPCController.Core.Models;

public sealed record NotificationInfo(
    string PackageName,
    string AppName,
    string Title,
    string Text,
    long WhenMs,
    bool IsOngoing)
{
    public string DisplayTime
    {
        get
        {
            var when = DateTimeOffset.FromUnixTimeMilliseconds(WhenMs).LocalDateTime;
            var elapsed = DateTime.Now - when;
            if (elapsed.TotalSeconds < 60) return "just now";
            if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
            if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours}h ago";
            return when.ToString("MMM d, HH:mm");
        }
    }

    public char IconChar => string.IsNullOrEmpty(AppName) ? '?' : char.ToUpperInvariant(AppName[0]);

    public string IconColor
    {
        get
        {
            var palette = new[]
            {
                "#00d2ff", "#7c4dff", "#ff6d00", "#00e676", "#ff1744",
                "#2979ff", "#d500f9", "#00bfa5", "#ff9100", "#536dfe"
            };
            var hash = 0;
            foreach (var c in PackageName)
                hash = (hash * 31 + c) & 0x7fffffff;
            return palette[hash % palette.Length];
        }
    }
}