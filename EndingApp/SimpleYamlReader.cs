namespace EndingApp;

/// <summary>
/// Lightweight YAML reader for simple key-value configuration files.
/// AOT-friendly, no reflection, no dependencies.
/// </summary>
internal static class SimpleYamlReader
{
    public static Dictionary<string, string> Read(string filePath)
    {
        var config = new Dictionary<string, string>();

        if (!File.Exists(filePath))
            return config;

        string[] lines = File.ReadAllLines(filePath);
        string currentSection = string.Empty;

        foreach (string line in lines)
        {
            string trimmed = line.Trim();

            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                continue;

            // Check if it's a section header (no leading spaces and ends with :)
            if (!line.StartsWith(' ') && trimmed.EndsWith(':'))
            {
                currentSection = trimmed.TrimEnd(':');
                continue;
            }

            // Parse key-value pair
            int colonIndex = trimmed.IndexOf(':');
            if (colonIndex > 0)
            {
                string key = trimmed[..colonIndex].Trim();
                string value = trimmed[(colonIndex + 1)..].Trim();

                // Remove quotes if present
                if (value.StartsWith('"') && value.EndsWith('"'))
                    value = value[1..^1];
                else if (value.StartsWith('\'') && value.EndsWith('\''))
                    value = value[1..^1];

                // Build full key with section prefix
                string fullKey = string.IsNullOrEmpty(currentSection)
                    ? key
                    : $"{currentSection}.{key}";
                config[fullKey] = value;
            }
        }

        return config;
    }

    public static string GetString(
        this Dictionary<string, string> config,
        string key,
        string defaultValue = ""
    )
    {
        return config.TryGetValue(key, out string? value) ? value : defaultValue;
    }

    public static int GetInt(
        this Dictionary<string, string> config,
        string key,
        int defaultValue = 0
    )
    {
        return config.TryGetValue(key, out string? value) && int.TryParse(value, out int result)
            ? result
            : defaultValue;
    }

    public static bool GetBool(
        this Dictionary<string, string> config,
        string key,
        bool defaultValue = false
    )
    {
        if (!config.TryGetValue(key, out string? value))
            return defaultValue;

        return value.ToLowerInvariant() switch
        {
            "true" or "yes" or "1" => true,
            "false" or "no" or "0" => false,
            _ => defaultValue,
        };
    }

    public static float GetFloat(
        this Dictionary<string, string> config,
        string key,
        float defaultValue = 0f
    )
    {
        return
            config.TryGetValue(key, out string? value)
            && float.TryParse(
                value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float result
            )
            ? result
            : defaultValue;
    }

    public static double GetDouble(
        this Dictionary<string, string> config,
        string key,
        double defaultValue = 0.0
    )
    {
        return
            config.TryGetValue(key, out string? value)
            && double.TryParse(
                value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double result
            )
            ? result
            : defaultValue;
    }
}
