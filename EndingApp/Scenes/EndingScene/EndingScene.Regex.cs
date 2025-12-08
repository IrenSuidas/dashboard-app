using System.Text.RegularExpressions;

namespace EndingApp;

internal sealed partial class EndingScene
{
    [GeneratedRegex("(?:,|\\r?\\n|\\\\n)")]
    private static partial Regex MyRegex();
}
