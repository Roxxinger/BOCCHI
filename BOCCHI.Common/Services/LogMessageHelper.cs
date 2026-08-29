using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace BOCCHI.Common.Services;

public static class LogMessageHelper
{
    private static readonly ConcurrentDictionary<uint, string> PatternCache = new();

    private static readonly Regex NumPlaceholder = new(@"^<num\((\w+)\)>", RegexOptions.Compiled);

    private static readonly Regex OtherMacro = new(@"^<[^>]+>", RegexOptions.Compiled);

    /// <summary>
    /// Build a regex from a LogMessage row: literal text escaped, <c>&lt;num(Name)&gt;</c> → named \d+ groups.
    /// </summary>
    public static string GetLogMessagePattern(IDataManager data, uint id) =>
        PatternCache.GetOrAdd(id, key => BuildLogMessagePattern(data, key));

    private static string BuildLogMessagePattern(IDataManager data, uint id)
    {
        string raw = data.GetExcelSheet<LogMessage>().GetRow(id).Text.ToString();
        var result = new StringBuilder(raw.Length);
        int i = 0;
        while (i < raw.Length)
        {
            // Slice so ^ anchors at the current parse position (Match(raw, i) still anchors ^ to string start).
            string rest = raw[i..];
            Match num = NumPlaceholder.Match(rest);
            if (num.Success)
            {
                result.Append($"(?<{num.Groups[1].Value}>\\d+)");
                i += num.Length;
                continue;
            }

            // Strip other SeString macros like <SoftHyphen/> so they do not break matching.
            Match macro = OtherMacro.Match(rest);
            if (macro.Success)
            {
                i += macro.Length;
                continue;
            }

            result.Append(Regex.Escape(raw[i].ToString()));
            i++;
        }

        return result.ToString();
    }
}
