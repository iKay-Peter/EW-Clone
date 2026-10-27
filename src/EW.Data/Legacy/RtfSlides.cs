using System.Text;

namespace EW.Data.Legacy;

public static class RtfSlides
{
    private const string SlideMarker = "\\sdslidemarker";

    public static IReadOnlyList<RtfSlide> Split(string? rtf)
    {
        if (string.IsNullOrWhiteSpace(rtf))
        {
            return [new RtfSlide(string.Empty, string.Empty)];
        }

        return rtf.Contains(SlideMarker, StringComparison.Ordinal)
            ? SplitAtMarkers(rtf)
            : SplitAtBlankParagraphs(rtf);
    }

    private static List<RtfSlide> SplitAtMarkers(string rtf)
    {
        var slides = new List<RtfSlide>();
        var segmentStart = 0;
        var searchStart = 0;

        while (searchStart < rtf.Length)
        {
            var markerIndex = rtf.IndexOf(SlideMarker, searchStart, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                break;
            }

            var markerEnd = markerIndex + SlideMarker.Length;
            if (markerEnd < rtf.Length && char.IsLetter(rtf[markerEnd]))
            {
                searchStart = markerEnd;
                continue;
            }

            var paragraphStart = FindParagraphStart(rtf, markerIndex);
            if (paragraphStart >= segmentStart)
            {
                var paragraphEnd = FindGroupEnd(rtf, paragraphStart);
                if (paragraphEnd >= markerEnd)
                {
                    AddSegment(slides, rtf, segmentStart, paragraphStart, allowEmpty: true);
                    segmentStart = paragraphEnd + 1;
                    searchStart = segmentStart;
                    continue;
                }
            }

            AddSegment(slides, rtf, segmentStart, markerIndex, allowEmpty: true);
            segmentStart = markerEnd;
            searchStart = markerEnd;
        }

        AddSegment(slides, rtf, segmentStart, rtf.Length, allowEmpty: true);
        if (slides.Count == 0)
        {
            AddSegment(slides, rtf, 0, rtf.Length, allowEmpty: true);
        }

        return slides;
    }

    private static List<RtfSlide> SplitAtBlankParagraphs(string rtf)
    {
        var bodyStart = rtf.IndexOf("\\pard", StringComparison.Ordinal);
        var header = bodyStart > 0 ? rtf[..bodyStart] : string.Empty;
        var body = bodyStart >= 0 ? rtf[(bodyStart + 5)..] : rtf;

        var slides = new List<RtfSlide>();
        var slideRtf = new StringBuilder();
        var slideText = new StringBuilder();
        var segmentStart = 0;
        var searchStart = 0;

        while (true)
        {
            var paragraphEnd = FindControlWord(body, "par", searchStart);
            if (paragraphEnd < 0)
            {
                break;
            }

            AppendParagraph(
                body,
                segmentStart,
                paragraphEnd,
                header,
                slideRtf,
                slideText,
                slides);

            segmentStart = paragraphEnd + 4;
            searchStart = segmentStart;
        }

        AppendParagraph(
            body,
            segmentStart,
            body.Length,
            header,
            slideRtf,
            slideText,
            slides);

        CloseSlide(header, slideRtf, slideText, slides);
        if (slides.Count == 0)
        {
            AddSegment(slides, rtf, 0, rtf.Length, allowEmpty: true);
        }

        return slides;
    }

    private static void AppendParagraph(
        string body,
        int start,
        int end,
        string header,
        StringBuilder slideRtf,
        StringBuilder slideText,
        List<RtfSlide> slides)
    {
        if (end <= start)
        {
            return;
        }

        var paragraph = body[start..end];
        var text = RtfPlainText.ToPlainText(paragraph).Trim();
        if (text.Length == 0)
        {
            CloseSlide(header, slideRtf, slideText, slides);
            return;
        }

        if (slideText.Length > 0)
        {
            slideText.Append('\n');
        }

        slideText.Append(text);
        slideRtf.Append(paragraph).Append("\\par");
    }

    private static void CloseSlide(
        string header,
        StringBuilder slideRtf,
        StringBuilder slideText,
        List<RtfSlide> slides)
    {
        if (slideRtf.Length == 0)
        {
            return;
        }

        var text = slideText.ToString().Trim();
        if (text.Length > 0)
        {
            slides.Add(new RtfSlide(header + "\\pard" + slideRtf, text));
        }

        slideRtf.Clear();
        slideText.Clear();
    }

    private static int FindControlWord(string rtf, string name, int start)
    {
        for (var index = start; index < rtf.Length; index++)
        {
            if (rtf[index] != '\\' || index + 1 >= rtf.Length)
            {
                continue;
            }

            if (!char.IsLetter(rtf[index + 1]))
            {
                index++;
                continue;
            }

            var wordStart = index + 1;
            var wordEnd = wordStart;
            while (wordEnd < rtf.Length && char.IsLetter(rtf[wordEnd]))
            {
                wordEnd++;
            }

            if (rtf.AsSpan(wordStart, wordEnd - wordStart).SequenceEqual(name))
            {
                return index;
            }

            index = wordEnd - 1;
        }

        return -1;
    }

    private static int FindParagraphStart(string rtf, int markerIndex)
    {
        var groupStart = rtf.LastIndexOf('{', markerIndex);
        if (groupStart < 0)
        {
            return -1;
        }

        var wordStart = groupStart + 1;
        if (wordStart >= markerIndex || rtf[wordStart] != '\\')
        {
            return -1;
        }

        var wordEnd = wordStart + 1;
        while (wordEnd < markerIndex && char.IsLetter(rtf[wordEnd]))
        {
            wordEnd++;
        }

        return wordEnd - wordStart == 5 &&
               rtf.AsSpan(wordStart + 1, 4).SequenceEqual("pard")
            ? groupStart
            : -1;
    }

    private static int FindGroupEnd(string rtf, int groupStart)
    {
        var depth = 0;
        for (var index = groupStart; index < rtf.Length; index++)
        {
            if (rtf[index] == '\\')
            {
                index++;
                continue;
            }

            if (rtf[index] == '{')
            {
                depth++;
            }
            else if (rtf[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }
        }

        return -1;
    }

    private static void AddSegment(
        List<RtfSlide> slides,
        string rtf,
        int start,
        int end,
        bool allowEmpty = false)
    {
        if (end <= start)
        {
            if (allowEmpty)
            {
                slides.Add(new RtfSlide(string.Empty, string.Empty));
            }

            return;
        }

        var segment = rtf[start..end];
        var plainText = RtfPlainText.ToPlainText(segment);
        if (plainText.Length == 0 && !allowEmpty)
        {
            return;
        }

        slides.Add(new RtfSlide(segment, plainText));
    }
}

public sealed record RtfSlide(string ContentRtf, string PlainText);
