using System.Text.RegularExpressions;
using AndroidPCController.Core.Models;

namespace AndroidPCController.Core.Notifications;

public static partial class NotificationInfoParser
{
    private static readonly Regex RecordHeaderRegex = NotificationRecordHeader();
    private static readonly Regex WhenRegex = new(@"^\s*when=(\d+)", RegexOptions.Compiled);
    private static readonly Regex FlagsRegex = new(@"^\s*flags=0x([0-9a-f]+)", RegexOptions.Compiled);
    private static readonly Regex ExtraStringRegex = new(@"^\s*android\.(title|text)=(String|CharSequence) \(([^)]*)\)", RegexOptions.Compiled);
    private static readonly Regex ExtraRedactedRegex = new(@"^\s*android\.(title|text)=(String|CharSequence) \[length=\d+\]", RegexOptions.Compiled);

    public static IReadOnlyList<NotificationInfo> Parse(string dumpsysOutput)
    {
        var result = new List<NotificationInfo>();
        if (string.IsNullOrEmpty(dumpsysOutput))
            return result;

        string? pkg = null;
        string? title = null;
        string? text = null;
        long whenMs = 0;
        var ongoing = false;
        var inRecord = false;

        void Flush()
        {
            if (inRecord && !string.IsNullOrEmpty(pkg))
            {
                result.Add(new NotificationInfo(
                    pkg,
                    pkg,
                    title ?? string.Empty,
                    text ?? string.Empty,
                    whenMs,
                    ongoing));
            }

            pkg = null;
            title = null;
            text = null;
            whenMs = 0;
            ongoing = false;
            inRecord = false;
        }

        foreach (var rawLine in dumpsysOutput.Split('\n'))
        {
            var trimmed = rawLine.Trim();

            if (trimmed.Length == 0 || trimmed == "...")
                continue;

            if (trimmed.StartsWith("NotificationRecord(", StringComparison.Ordinal))
            {
                Flush();
                var m = RecordHeaderRegex.Match(rawLine);
                if (!m.Success)
                    continue;

                inRecord = true;
                pkg = m.Groups["pkg"].Value;
                continue;
            }

            var leading = rawLine.Length - rawLine.TrimStart().Length;

            if (inRecord && leading < 4)
            {
                Flush();
                continue;
            }

            if (!inRecord)
                continue;

            var when = WhenRegex.Match(rawLine);
            if (when.Success)
            {
                whenMs = long.Parse(when.Groups[1].Value);
                continue;
            }

            var flags = FlagsRegex.Match(rawLine);
            if (flags.Success)
            {
                ongoing = (Convert.ToInt32(flags.Groups[1].Value, 16) & 0x2) != 0;
                continue;
            }

            var extra = ExtraStringRegex.Match(rawLine);
            if (extra.Success)
            {
                var value = extra.Groups[3].Value.Trim();
                if (extra.Groups[1].Value == "title")
                    title = string.IsNullOrEmpty(value) ? null : value;
                else
                    text = string.IsNullOrEmpty(value) ? null : value;
                continue;
            }

            if (ExtraRedactedRegex.IsMatch(rawLine))
                continue;
        }

        Flush();
        return result;
    }

    [GeneratedRegex(@"NotificationRecord\([^:]*: pkg=(?<pkg>[^\s]+) user=UserHandle\{[^}]+\} id=\d+ tag=\S+")]
    private static partial Regex NotificationRecordHeader();
}