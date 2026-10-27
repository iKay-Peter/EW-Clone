using System.Text;
using EW.Data.Legacy;

namespace EW.Data.Tests;

public sealed class LegacyIniFileReaderTests : IDisposable
{
    private readonly string _root;

    public LegacyIniFileReaderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "EWCloneTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void ReadParsesSectionsValuesAndComments()
    {
        var path = Path.Combine(_root, "Sample.ini");
        File.WriteAllText(
            path,
            """
            [Main]
            Key=Value
            Empty=
            Equals=a=b
            ; comment
            # comment
            [ Other ]
             Space = x 
            Bare
            """,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var file = LegacyIniFileReader.Read(path);

        Assert.Equal(4, file.Settings.Count);
        Assert.Equal("Main", file.Settings[0].Section);
        Assert.Equal("Key", file.Settings[0].Key);
        Assert.Equal("Value", file.Settings[0].Value);
        Assert.Equal("", file.Settings[1].Value);
        Assert.Equal("a=b", file.Settings[2].Value);
        Assert.Equal("Other", file.Settings[3].Section);
        Assert.Equal("Space", file.Settings[3].Key);
        Assert.Equal("x", file.Settings[3].Value);
        Assert.Contains(file.Warnings, warning => warning.Contains("Line 9", StringComparison.Ordinal));
        Assert.Equal(64, file.SourceSha256.Length);
    }

    [Fact]
    public void ReadReportsDuplicateKeysAndKeepsBothValues()
    {
        var path = Path.Combine(_root, "Duplicate.ini");
        File.WriteAllText(
            path,
            """
            [Main]
            Key=one
            KEY=two
            """,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var file = LegacyIniFileReader.Read(path);

        Assert.Equal(2, file.Settings.Count);
        Assert.Contains(
            file.Warnings,
            warning => warning.Contains("Duplicate key", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
