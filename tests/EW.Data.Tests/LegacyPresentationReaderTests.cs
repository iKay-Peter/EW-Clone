using EW.Data.Legacy;

namespace EW.Data.Tests;

public sealed class LegacyPresentationReaderTests : IDisposable
{
    private readonly LegacyPresentationFixture _fixture = new();

    [Fact]
    public void ReadPreservesPresentationGraphAndUnknownValues()
    {
        var batch = LegacyPresentationReader.Read(_fixture.DatabasePath);

        var presentation = Assert.Single(batch.Presentations);
        Assert.Equal("pres-uid-1", presentation.LegacyKey);
        Assert.Equal("Synthetic Presentation", presentation.Title);
        Assert.Equal("Test Author", presentation.Author);
        Assert.Equal("16:9", presentation.AspectRatio);
        Assert.Contains("preserve-presentation", presentation.RawJson, StringComparison.Ordinal);
        Assert.Equal(2, presentation.Slides.Count);

        var firstSlide = presentation.Slides[0];
        Assert.Equal(0, firstSlide.Position);
        var element = Assert.Single(firstSlide.Elements);
        Assert.Equal(1, element.ElementType);
        Assert.Equal(2, element.ElementStyleType);
        Assert.Equal(10.5, element.X);
        Assert.Equal(1, element.ForegroundResourceId);
        Assert.True(element.FromMaster);
        Assert.Contains("preserve-element", element.RawJson, StringComparison.Ordinal);

        var resource = Assert.Single(batch.Resources);
        Assert.Equal("res-uid-1", resource.LegacyKey);
        Assert.Equal("Hello resource", resource.TextPlain);
        Assert.Contains("preserve-resource", resource.RawJson, StringComparison.Ordinal);
        Assert.Equal(64, batch.SourceSha256.Length);
    }

    public void Dispose() => _fixture.Dispose();
}
