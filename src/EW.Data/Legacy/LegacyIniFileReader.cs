using System.Text;

namespace EW.Data.Legacy;

public static class LegacyIniFileReader
{
    public static LegacyIniFile Read(string path)
    {
        var sourcePath = Path.GetFullPath(path);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Legacy settings file not found.", sourcePath);
        }

        var settings = new List<LegacyIniSetting>();
        var warnings = new List<string>();
        string? section = null;
        var lineNumber = 0;

        using var stream = File.OpenRead(sourcePath);
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            detectEncodingFromByteOrderMarks: true);

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed[0] is ';' or '#')
            {
                continue;
            }

            if (trimmed[0] == '[')
            {
                var end = trimmed.IndexOf(']');
                if (end <= 1)
                {
                    warnings.Add($"Line {lineNumber}: malformed section header.");
                    section = null;
                    continue;
                }

                section = trimmed[1..end].Trim();
                if (section.Length == 0)
                {
                    warnings.Add($"Line {lineNumber}: empty section name.");
                    section = null;
                }

                continue;
            }

            var equals = line.IndexOf('=');
            if (equals <= 0)
            {
                warnings.Add($"Line {lineNumber}: missing key/value separator.");
                continue;
            }

            if (section is null)
            {
                warnings.Add($"Line {lineNumber}: value before any section.");
                continue;
            }

            var key = line[..equals].Trim();
            if (key.Length == 0)
            {
                warnings.Add($"Line {lineNumber}: empty key.");
                continue;
            }

            settings.Add(new LegacyIniSetting(
                section,
                key,
                line[(equals + 1)..].Trim(),
                lineNumber,
                line));
        }

        foreach (var duplicate in settings
                     .GroupBy(setting => $"{setting.Section}\u001F{setting.Key}", StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            var first = duplicate.First();
            warnings.Add(
                $"Duplicate key '{first.Section}/{first.Key}' at lines "
                + string.Join(", ", duplicate.Select(setting => setting.LineNumber))
                + "; the last value wins.");
        }

        return new LegacyIniFile(
            sourcePath,
            LegacyDatabaseReader.FileSha256(sourcePath),
            settings,
            warnings);
    }
}

public sealed record LegacyIniFile(
    string SourcePath,
    string SourceSha256,
    IReadOnlyList<LegacyIniSetting> Settings,
    IReadOnlyList<string> Warnings);

public sealed record LegacyIniSetting(
    string Section,
    string Key,
    string Value,
    int LineNumber,
    string RawLine);
