using System.Text;
using System.Text.Json;

namespace EW.Data.Legacy;

public static class LegacyThemeReader
{
    public static LegacyThemeBatch Read(string archivePath)
    {
        var archive = LegacyArchiveReader.ReadPresentationArchive(archivePath);
        if (archive.PresentationGraph.Presentations.Count == 0)
        {
            throw new InvalidDataException("Theme archive does not contain a presentation graph.");
        }

        var presentation = archive.PresentationGraph.Presentations[0];

        var name = string.IsNullOrWhiteSpace(presentation.Title)
            ? Path.GetFileNameWithoutExtension(archive.ArchivePath)
            : presentation.Title;

        return new LegacyThemeBatch(
            archive.ArchivePath,
            archive.ArchiveSha256,
            archive.Entries,
            new LegacyTheme(
                presentation.LegacyKey,
                name,
                BuildStyleJson(archive.PresentationGraph)));
    }

    public static string FileSha256(string path) => LegacyDatabaseReader.FileSha256(path);

    private static string BuildStyleJson(LegacyPresentationBatch graph)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("presentation");
            WriteRawJson(writer, graph.Presentations[0].RawJson);

            writer.WriteStartArray("slides");
            foreach (var slide in graph.Presentations[0].Slides)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("slide");
                WriteRawJson(writer, slide.RawJson);

                writer.WriteStartArray("elements");
                foreach (var element in slide.Elements)
                {
                    WriteRawJson(writer, element.RawJson);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteStartArray("resources");
            foreach (var resource in graph.Resources)
            {
                WriteRawJson(writer, resource.RawJson);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteRawJson(Utf8JsonWriter writer, string rawJson)
    {
        using var document = JsonDocument.Parse(rawJson);
        document.RootElement.WriteTo(writer);
    }
}

public sealed record LegacyThemeBatch(
    string ArchivePath,
    string ArchiveSha256,
    IReadOnlyList<LegacyArchiveEntry> Entries,
    LegacyTheme Theme);

public sealed record LegacyTheme(
    string LegacyKey,
    string Name,
    string StyleJson);
