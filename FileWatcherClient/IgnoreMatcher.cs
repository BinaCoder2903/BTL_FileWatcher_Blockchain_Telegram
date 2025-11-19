// IgnoreMatcher.cs - client
// Glob don gian: **, *, ? . Doc them tu file .watchignore trong thu muc watch.

using System.Text.RegularExpressions;

public class IgnoreMatcher
{
    private readonly List<string> _patterns = new();

    public static IgnoreMatcher LoadFrom(string rootFolder)
    {
        var ig = new IgnoreMatcher();
        var cfg = Path.Combine(rootFolder, ".watchignore");

        if (File.Exists(cfg))
        {
            foreach (var line in File.ReadAllLines(cfg))
            {
                var s = line.Trim();
                if (string.IsNullOrWhiteSpace(s)) continue;
                if (s.StartsWith("#")) continue;
                ig._patterns.Add(s);
            }
        }

        // mac dinh bo nhieu tap tin thuong gap
        ig._patterns.Add("**/~$*");
        ig._patterns.Add("**/*.tmp");
        ig._patterns.Add("**/*.log");
        ig._patterns.Add("**/bin/**");
        ig._patterns.Add("**/obj/**");
        ig._patterns.Add("**/.git/**");

        return ig;
    }

    public bool IsIgnored(string path)
    {
        var norm = path.Replace('\\', '/');
        foreach (var p in _patterns)
        {
            if (Glob(norm, p)) return true;
        }
        return false;
    }

    private static bool Glob(string text, string pattern)
    {
        var rx = "^" + Regex.Escape(pattern)
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", @"[^/]*")
            .Replace(@"\?", ".")
            + "$";
        return Regex.IsMatch(text, rx, RegexOptions.IgnoreCase);
    }
}
