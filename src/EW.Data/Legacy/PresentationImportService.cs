using System.Globalization;
using System.Text.Json;
using EW.Domain.Media;
using EW.Domain.Presentations;
using EW.Domain.Resources;

namespace EW.Data.Legacy;

public sealed class PresentationImportService(SqliteDatabase database)
{
    private readonly SqliteDatabase _database = database ?? throw new ArgumentNullException(nameof(database));

    public PresentationImportResult Import(LegacyPresentationBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var resourcesCreated = 0;
        var resourcesUpdated = 0;
        var presentationsCreated = 0;
        var presentationsUpdated = 0;
        var presentationIds = new List<PresentationId>(batch.Presentations.Count);
        var resourceIds = new Dictionary<long, ResourceId>(batch.Resources.Count);

        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        var mediaAssetIds = new Dictionary<string, MediaAssetId>(StringComparer.OrdinalIgnoreCase);
        foreach (var legacyResource in batch.Resources)
        {
            if (legacyResource.MediaHash is not { Length: > 0 } hash)
            {
                continue;
            }

            var mediaAssetId = MediaAssetRepository.FindIdByHash(connection, transaction, hash);
            if (mediaAssetId is not null)
            {
                mediaAssetIds[hash] = mediaAssetId.Value;
            }
        }

        foreach (var legacyResource in batch.Resources)
        {
            var resource = MapResource(batch, legacyResource, mediaAssetIds);
            var (id, created) = ResourceRepository.Upsert(connection, transaction, resource);
            resourceIds[legacyResource.RowId] = id;

            if (created)
            {
                resourcesCreated++;
            }
            else
            {
                resourcesUpdated++;
            }
        }

        foreach (var legacyPresentation in batch.Presentations)
        {
            var presentation = MapPresentation(batch, legacyPresentation, resourceIds);
            var (id, created) = PresentationRepository.Upsert(connection, transaction, presentation);
            presentationIds.Add(id);

            if (created)
            {
                presentationsCreated++;
            }
            else
            {
                presentationsUpdated++;
            }
        }

        transaction.Commit();
        return new PresentationImportResult(
            presentationsCreated,
            presentationsUpdated,
            resourcesCreated,
            resourcesUpdated,
            presentationIds);
    }

    private static Resource MapResource(
        LegacyPresentationBatch batch,
        LegacyResource legacyResource,
        Dictionary<string, MediaAssetId> mediaAssetIds)
    {
        MediaAssetId? mediaAssetId = null;
        if (legacyResource.MediaHash is { Length: > 0 } hash &&
            mediaAssetIds.TryGetValue(hash, out var value))
        {
            mediaAssetId = value;
        }

        return new Resource(
            ResourceId.New(),
            legacyResource.LegacyKey,
            legacyResource.ResourceType,
            legacyResource.TextRtf,
            legacyResource.TextPlain,
            legacyResource.WebUrl,
            legacyResource.ShapeData,
            mediaAssetId,
            BuildMetadata(batch.SourcePath, batch.SourceSha256, legacyResource.RawJson));
    }

    private static Presentation MapPresentation(
        LegacyPresentationBatch batch,
        LegacyPresentation legacyPresentation,
        IReadOnlyDictionary<long, ResourceId> resourceIds)
    {
        var slides = legacyPresentation.Slides
            .Select(slide => new Slide(
                SlideId.New(),
                slide.Position,
                slide.Notes,
                BuildMetadata(batch.SourcePath, batch.SourceSha256, slide.RawJson),
                slide.Elements.Select(element => MapElement(batch, element, resourceIds)).ToArray()))
            .ToArray();

        return new Presentation(
            PresentationId.New(),
            legacyPresentation.LegacyKey,
            legacyPresentation.Title,
            legacyPresentation.Author,
            ParseAspectRatio(legacyPresentation.AspectRatio),
            BuildMetadata(batch.SourcePath, batch.SourceSha256, legacyPresentation.RawJson),
            slides);
    }

    private static Element MapElement(
        LegacyPresentationBatch batch,
        LegacyElement legacyElement,
        IReadOnlyDictionary<long, ResourceId> resourceIds)
    {
        ResourceId? Find(long? legacyId) =>
            legacyId is { } value && resourceIds.TryGetValue(value, out var id) ? id : null;

        var background = Find(legacyElement.BackgroundResourceId);
        var foreground = Find(legacyElement.ForegroundResourceId);
        var shape = Find(legacyElement.ShapeResourceId);

        return new Element(
            ElementId.New(),
            legacyElement.Position,
            legacyElement.ElementType,
            legacyElement.ElementStyleType,
            legacyElement.X,
            legacyElement.Y,
            legacyElement.Width,
            legacyElement.Height,
            StyleJson: null,
            foreground ?? background,
            background,
            foreground,
            shape,
            legacyElement.InternalMute,
            legacyElement.FromMaster,
            BuildMetadata(batch.SourcePath, batch.SourceSha256, legacyElement.RawJson));
    }

    private static double? ParseAspectRatio(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var width) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var height) &&
            height != 0)
        {
            return width / height;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var ratio)
            ? ratio
            : null;
    }

    private static string BuildMetadata(string sourcePath, string sourceSha256, string rawJson)
    {
        var metadata = new
        {
            importVersion = 1,
            source = new
            {
                path = sourcePath,
                sha256 = sourceSha256,
            },
            row = JsonSerializer.Deserialize<JsonElement>(rawJson),
        };

        return JsonSerializer.Serialize(metadata);
    }
}

public sealed record PresentationImportResult(
    int PresentationsCreated,
    int PresentationsUpdated,
    int ResourcesCreated,
    int ResourcesUpdated,
    IReadOnlyList<PresentationId> PresentationIds);
