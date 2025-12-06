namespace EndingApp;

/// <summary>
/// Reader for credits.yaml file with YAML array support.
/// AOT-friendly, no reflection, no dependencies.
/// </summary>
internal static class CreditsReader
{
    public static List<CreditEntry> Read(string filePath)
    {
        var credits = new List<CreditEntry>();

        if (!File.Exists(filePath))
            return credits;

        string[] lines = File.ReadAllLines(filePath);
        CreditEntry? currentEntry = null;
        bool inValuesArray = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.Trim();

            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                continue;

            // Skip the root "credits:" line
            if (trimmed == "credits:")
                continue;

            // Detect new entry (starts with "  - ")
            if (line.StartsWith("  - ") || line.StartsWith("  -\t"))
            {
                // Save previous entry if exists
                if (currentEntry != null)
                {
                    credits.Add(currentEntry);
                }

                // Start new entry
                currentEntry = new CreditEntry();
                inValuesArray = false;

                // Check if this line has content after the dash
                string afterDash = line[4..].Trim();
                if (!string.IsNullOrEmpty(afterDash))
                {
                    ParseKeyValue(afterDash, currentEntry);
                }
                continue;
            }

            // Parse properties of current entry
            if (currentEntry != null && line.StartsWith("    "))
            {
                string content = line.Trim();

                // Check if we're entering the values array
                if (content == "values:")
                {
                    inValuesArray = true;
                    continue;
                }

                // Parse values array items
                if (inValuesArray && (content.StartsWith("- ") || content.StartsWith("-\t")))
                {
                    string value = content[1..].Trim();
                    currentEntry.Values.Add(value);
                    continue;
                }

                // Parse other properties
                if (!inValuesArray)
                {
                    ParseKeyValue(content, currentEntry);
                    inValuesArray = false;
                }
            }
        }

        // Add the last entry
        if (currentEntry != null)
        {
            credits.Add(currentEntry);
        }

        return credits;
    }

    private static void ParseKeyValue(string content, CreditEntry entry)
    {
        int colonIndex = content.IndexOf(':');
        if (colonIndex <= 0)
            return;

        string key = content[..colonIndex].Trim();
        string value = content[(colonIndex + 1)..].Trim();

        // Remove quotes if present
        if (value.StartsWith('"') && value.EndsWith('"'))
            value = value[1..^1];
        else if (value.StartsWith('\'') && value.EndsWith('\''))
            value = value[1..^1];

        switch (key)
        {
            case "section":
                entry.Section = value;
                break;
            case "separator":
                entry.Separator = value;
                break;
            case "twoColumns":
                entry.TwoColumns = value.ToLowerInvariant() is "true" or "yes" or "1";
                break;
        }
    }
}

/// <summary>
/// Represents a single credit entry (section with values or separator)
/// </summary>
internal sealed class CreditEntry
{
    public string? Section { get; set; }
    public string? Separator { get; set; }
    public bool TwoColumns { get; set; }
    public List<string> Values { get; set; } = [];

    public bool IsSeparator => !string.IsNullOrEmpty(Separator);
}
