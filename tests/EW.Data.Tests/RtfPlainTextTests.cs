using EW.Data.Legacy;

namespace EW.Data.Tests;

public sealed class RtfPlainTextTests
{
    [Fact]
    public void StripsDestinationsAndConvertsParagraphs()
    {
        const string rtf = @"{\rtf1\ansi\deff0{\fonttbl{\f0 Arial;}}Amazing grace\par how sweet the sound}";

        var text = RtfPlainText.ToPlainText(rtf);

        Assert.Equal("Amazing grace\nhow sweet the sound", text);
    }

    [Fact]
    public void SkipsIgnorableDestinations()
    {
        const string rtf = @"{\rtf1\ansi{\*\pnseclvl1{\pntxtb{(}}{\pntxta{)}}}Amazing grace\par}";

        var text = RtfPlainText.ToPlainText(rtf);

        Assert.Equal("Amazing grace", text);
    }

    [Fact]
    public void SplitsSlidesAtParagraphLevel()
    {
        const string rtf = @"{\rtf1\ansi{\fonttbl{\f0 Arial;}}{\pard First slide\par}{\pard\sdslidemarker\qc\par}{\pard Second slide\par}{\pard\sdslidemarker\qc\par}{\pard Third slide\par}}";

        var slides = RtfSlides.Split(rtf);

        Assert.Collection(
            slides,
            slide => Assert.Equal("First slide", slide.PlainText),
            slide => Assert.Equal("Second slide", slide.PlainText),
            slide => Assert.Equal("Third slide", slide.PlainText));
    }

    [Fact]
    public void PreservesEmptySlidesBetweenMarkers()
    {
        const string rtf = @"{\rtf1\ansi{\fonttbl{\f0 Arial;}}{\pard First slide\par}{\pard\sdslidemarker\qc\par}{\pard Second slide\par}{\pard\sdslidemarker\qc\par}}";

        var slides = RtfSlides.Split(rtf);

        Assert.Collection(
            slides,
            slide => Assert.Equal("First slide", slide.PlainText),
            slide => Assert.Equal("Second slide", slide.PlainText),
            slide => Assert.Equal(string.Empty, slide.PlainText));
    }

    [Fact]
    public void SplitsSlidesAtBlankParagraphsWithoutMarkers()
    {
        const string rtf = @"{\rtf1\ansi{\*\pnseclvl1{\pntxtb{(}}{\pntxta{)}}}\pard Chorus 1\par Come to my soul.\par \li0\par Verse 1\par Give me a heart.\par \li0\par}";

        var slides = RtfSlides.Split(rtf);

        Assert.Collection(
            slides,
            slide => Assert.Equal("Chorus 1\nCome to my soul.", slide.PlainText),
            slide => Assert.Equal("Verse 1\nGive me a heart.", slide.PlainText));
    }
}
