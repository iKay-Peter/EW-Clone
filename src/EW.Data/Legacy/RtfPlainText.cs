using System.Globalization;
using System.Text;

namespace EW.Data.Legacy;

public static class RtfPlainText
{
    private static readonly HashSet<string> DestinationWords = new(StringComparer.Ordinal)
    {
        "colortbl",
        "datastore",
        "filetbl",
        "fonttbl",
        "footer",
        "footerf",
        "footerr",
        "generator",
        "header",
        "headerf",
        "headerr",
        "info",
        "latentstyles",
        "listoverridetable",
        "listtable",
        "object",
        "pict",
        "revtbl",
        "rsidtbl",
        "stylesheet",
        "themedata",
        "xmlnstbl",
    };

    public static string ToPlainText(string? rtf)
    {
        if (string.IsNullOrWhiteSpace(rtf))
        {
            return string.Empty;
        }

        var output = new StringBuilder(rtf.Length);
        var groups = new Stack<bool>();
        var skip = false;
        var index = 0;

        while (index < rtf.Length)
        {
            var current = rtf[index];

            if (current == '{')
            {
                groups.Push(skip);
                index++;
                continue;
            }

            if (current == '}')
            {
                if (groups.Count > 0)
                {
                    skip = groups.Pop();
                }

                index++;
                continue;
            }

            if (current != '\\')
            {
                if (!skip && current is not '\r' and not '\n')
                {
                    output.Append(current);
                }

                index++;
                continue;
            }

            if (index + 1 >= rtf.Length)
            {
                break;
            }

            var escaped = rtf[index + 1];
            if (escaped is '\\' or '{' or '}')
            {
                if (!skip)
                {
                    output.Append(escaped);
                }

                index += 2;
                continue;
            }

            if (escaped == '\'')
            {
                if (index + 3 < rtf.Length &&
                    byte.TryParse(
                        rtf.AsSpan(index + 2, 2),
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture,
                        out var value))
                {
                    if (!skip)
                    {
                        output.Append((char)value);
                    }

                    index += 4;
                }
                else
                {
                    index += 2;
                }

                continue;
            }

            if (escaped == '*')
            {
                skip = true;
                index += 2;
                continue;
            }

            if (escaped is '~' or '_')
            {
                if (!skip)
                {
                    output.Append(escaped == '~' ? ' ' : '-');
                }

                index += 2;
                continue;
            }

            if (escaped == '-')
            {
                index += 2;
                continue;
            }

            if (escaped is '\r' or '\n')
            {
                index += 2;
                continue;
            }

            var wordStart = index + 1;
            var wordEnd = wordStart;
            while (wordEnd < rtf.Length && char.IsLetter(rtf[wordEnd]))
            {
                wordEnd++;
            }

            var word = rtf[wordStart..wordEnd];
            var parameterStart = wordEnd;
            var parameterEnd = parameterStart;
            if (parameterEnd < rtf.Length && rtf[parameterEnd] == '-')
            {
                parameterEnd++;
            }

            while (parameterEnd < rtf.Length && char.IsDigit(rtf[parameterEnd]))
            {
                parameterEnd++;
            }

            var parameter = parameterEnd > parameterStart
                ? rtf[parameterStart..parameterEnd]
                : null;

            index = SkipDelimiter(rtf, parameterEnd);

            if (DestinationWords.Contains(word))
            {
                skip = true;
                continue;
            }

            if (word is "par" or "line" or "row")
            {
                if (!skip)
                {
                    output.Append('\n');
                }

                continue;
            }

            if (word is "tab" or "cell")
            {
                if (!skip)
                {
                    output.Append('\t');
                }

                continue;
            }

            if (word == "u")
            {
                if (!skip &&
                    int.TryParse(parameter, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
                {
                    var scalar = code < 0 ? code + 0x10000 : code;
                    if (scalar > 0 && scalar <= 0x10FFFF)
                    {
                        output.Append(char.ConvertFromUtf32(scalar));
                    }
                }

                if (index < rtf.Length && rtf[index] == '?')
                {
                    index++;
                }

                continue;
            }

            if (word == "bin" &&
                int.TryParse(parameter, NumberStyles.Integer, CultureInfo.InvariantCulture, out var byteCount))
            {
                index = Math.Min(rtf.Length, index + Math.Max(0, byteCount));
            }
        }

        return output.ToString()
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
    }

    private static int SkipDelimiter(string rtf, int index)
    {
        if (index < rtf.Length && rtf[index] == ' ')
        {
            return index + 1;
        }

        if (index + 1 < rtf.Length && rtf[index] == '\r' && rtf[index + 1] == '\n')
        {
            return index + 2;
        }

        if (index < rtf.Length && rtf[index] is '\r' or '\n')
        {
            return index + 1;
        }

        return index;
    }
}
