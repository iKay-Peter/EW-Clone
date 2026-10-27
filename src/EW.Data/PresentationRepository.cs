using System.Globalization;
using EW.Domain.Presentations;
using EW.Domain.Resources;
using Microsoft.Data.Sqlite;

namespace EW.Data;

public sealed class PresentationRepository(SqliteDatabase database)
{
    private readonly SqliteDatabase _database = database ?? throw new ArgumentNullException(nameof(database));

    public Presentation? Get(PresentationId id)
    {
        using var connection = _database.OpenConnection();
        return Get(connection, transaction: null, id);
    }

    public IReadOnlyList<Presentation> List()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id FROM Presentation ORDER BY Title COLLATE NOCASE;";

        var ids = new List<PresentationId>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                ids.Add(new PresentationId(Guid.Parse(reader.GetString(0))));
            }
        }

        return ids.Select(id => Get(connection, transaction: null, id))
            .OfType<Presentation>()
            .ToArray();
    }

    internal static PresentationId? FindIdByLegacyId(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string legacyId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Id FROM Presentation WHERE LegacyId = $legacyId LIMIT 1;";
        command.Parameters.AddWithValue("$legacyId", legacyId);

        var value = command.ExecuteScalar();
        return value is string id ? new PresentationId(Guid.Parse(id)) : null;
    }

    internal static (PresentationId Id, bool Created) Upsert(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Presentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);

        var existingId = presentation.LegacyId is null
            ? null
            : FindIdByLegacyId(connection, transaction, presentation.LegacyId);

        var id = existingId ?? presentation.Id;
        var created = existingId is null;
        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = created
                ? """
                    INSERT INTO Presentation (
                        Id, LegacyId, Title, Author, AspectRatio, ThemeId,
                        LegacyMetadata, CreatedUtc, ModifiedUtc)
                    VALUES (
                        $id, $legacyId, $title, $author, $aspectRatio, NULL,
                        $legacyMetadata, $createdUtc, $modifiedUtc);
                    """
                : """
                    UPDATE Presentation
                    SET Title = $title,
                        Author = $author,
                        AspectRatio = $aspectRatio,
                        LegacyMetadata = $legacyMetadata,
                        ModifiedUtc = $modifiedUtc
                    WHERE Id = $id;
                    """;
            command.Parameters.AddWithValue("$id", id.ToString());
            command.Parameters.AddWithValue("$legacyId", (object?)presentation.LegacyId ?? DBNull.Value);
            command.Parameters.AddWithValue("$title", presentation.Title);
            command.Parameters.AddWithValue("$author", (object?)presentation.Author ?? DBNull.Value);
            command.Parameters.AddWithValue(
                "$aspectRatio",
                (object?)presentation.AspectRatio ?? DBNull.Value);
            command.Parameters.AddWithValue(
                "$legacyMetadata",
                (object?)presentation.LegacyMetadata ?? DBNull.Value);
            command.Parameters.AddWithValue("$createdUtc", now);
            command.Parameters.AddWithValue("$modifiedUtc", now);
            command.ExecuteNonQuery();
        }

        if (!created)
        {
            using var deleteSlides = connection.CreateCommand();
            deleteSlides.Transaction = transaction;
            deleteSlides.CommandText = "DELETE FROM Slide WHERE PresentationId = $id;";
            deleteSlides.Parameters.AddWithValue("$id", id.ToString());
            deleteSlides.ExecuteNonQuery();
        }

        foreach (var slide in presentation.Slides)
        {
            InsertSlide(connection, transaction, id, slide);
        }

        using (var deleteSearch = connection.CreateCommand())
        {
            deleteSearch.Transaction = transaction;
            deleteSearch.CommandText = "DELETE FROM PresentationFts WHERE PresentationId = $id;";
            deleteSearch.Parameters.AddWithValue("$id", id.ToString());
            deleteSearch.ExecuteNonQuery();
        }

        using (var search = connection.CreateCommand())
        {
            search.Transaction = transaction;
            search.CommandText = """
                INSERT INTO PresentationFts (PresentationId, Title, Author, Tags)
                VALUES ($id, $title, $author, NULL);
                """;
            search.Parameters.AddWithValue("$id", id.ToString());
            search.Parameters.AddWithValue("$title", presentation.Title);
            search.Parameters.AddWithValue("$author", (object?)presentation.Author ?? DBNull.Value);
            search.ExecuteNonQuery();
        }

        return (id, created);
    }

    private static Presentation? Get(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        PresentationId id)
    {
        PresentationId presentationId;
        string? legacyId;
        string title;
        string? author;
        double? aspectRatio;
        string? legacyMetadata;

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                SELECT Id, LegacyId, Title, Author, AspectRatio, LegacyMetadata
                FROM Presentation
                WHERE Id = $id;
                """;
            command.Parameters.AddWithValue("$id", id.ToString());

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            presentationId = new PresentationId(Guid.Parse(reader.GetString(0)));
            legacyId = reader.IsDBNull(1) ? null : reader.GetString(1);
            title = reader.GetString(2);
            author = reader.IsDBNull(3) ? null : reader.GetString(3);
            aspectRatio = reader.IsDBNull(4) ? null : reader.GetDouble(4);
            legacyMetadata = reader.IsDBNull(5) ? null : reader.GetString(5);
        }

        return new Presentation(
            presentationId,
            legacyId,
            title,
            author,
            aspectRatio,
            legacyMetadata,
            ReadSlides(connection, transaction, presentationId));
    }

    private static List<Slide> ReadSlides(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        PresentationId presentationId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT Id, Position, Notes, LegacyMetadata
            FROM Slide
            WHERE PresentationId = $presentationId
            ORDER BY Position;
            """;
        command.Parameters.AddWithValue("$presentationId", presentationId.ToString());

        var slideRows = new List<(SlideId Id, int Position, string? Notes, string? LegacyMetadata)>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                slideRows.Add((
                    new SlideId(Guid.Parse(reader.GetString(0))),
                    reader.GetInt32(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3)));
            }
        }

        return slideRows
            .Select(row => new Slide(
                row.Id,
                row.Position,
                row.Notes,
                row.LegacyMetadata,
                ReadElements(connection, transaction, row.Id)))
            .ToList();
    }

    private static List<Element> ReadElements(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        SlideId slideId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT Id, Position, ElementType, ElementStyleType, X, Y, Width, Height,
                   StyleJson, ResourceId, BackgroundResourceId, ForegroundResourceId,
                   ShapeResourceId, InternalMute, FromMaster, LegacyMetadata
            FROM Element
            WHERE SlideId = $slideId
            ORDER BY Position;
            """;
        command.Parameters.AddWithValue("$slideId", slideId.ToString());

        var elements = new List<Element>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            elements.Add(new Element(
                new ElementId(Guid.Parse(reader.GetString(0))),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.GetDouble(4),
                reader.GetDouble(5),
                reader.GetDouble(6),
                reader.GetDouble(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                ParseResourceId(reader, 9),
                ParseResourceId(reader, 10),
                ParseResourceId(reader, 11),
                ParseResourceId(reader, 12),
                reader.GetInt64(13) != 0,
                reader.GetInt64(14) != 0,
                reader.IsDBNull(15) ? null : reader.GetString(15)));
        }

        return elements;
    }

    private static void InsertSlide(
        SqliteConnection connection,
        SqliteTransaction transaction,
        PresentationId presentationId,
        Slide slide)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Slide (Id, PresentationId, Position, ThemeId, Notes, LegacyMetadata)
            VALUES ($id, $presentationId, $position, NULL, $notes, $legacyMetadata);
            """;
        command.Parameters.AddWithValue("$id", slide.Id.ToString());
        command.Parameters.AddWithValue("$presentationId", presentationId.ToString());
        command.Parameters.AddWithValue("$position", slide.Position);
        command.Parameters.AddWithValue("$notes", (object?)slide.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$legacyMetadata",
            (object?)slide.LegacyMetadata ?? DBNull.Value);
        command.ExecuteNonQuery();

        foreach (var element in slide.Elements)
        {
            InsertElement(connection, transaction, slide.Id, element);
        }
    }

    private static void InsertElement(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SlideId slideId,
        Element element)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Element (
                Id, SlideId, Position, ElementType, X, Y, Width, Height, StyleJson,
                ResourceId, ElementStyleType, BackgroundResourceId, ForegroundResourceId,
                ShapeResourceId, InternalMute, FromMaster, LegacyMetadata)
            VALUES (
                $id, $slideId, $position, $elementType, $x, $y, $width, $height, $styleJson,
                $resourceId, $elementStyleType, $backgroundResourceId, $foregroundResourceId,
                $shapeResourceId, $internalMute, $fromMaster, $legacyMetadata);
            """;
        command.Parameters.AddWithValue("$id", element.Id.ToString());
        command.Parameters.AddWithValue("$slideId", slideId.ToString());
        command.Parameters.AddWithValue("$position", element.Position);
        command.Parameters.AddWithValue("$elementType", element.ElementType);
        command.Parameters.AddWithValue("$x", element.X);
        command.Parameters.AddWithValue("$y", element.Y);
        command.Parameters.AddWithValue("$width", element.Width);
        command.Parameters.AddWithValue("$height", element.Height);
        command.Parameters.AddWithValue("$styleJson", (object?)element.StyleJson ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$resourceId",
            element.ResourceId is { } resourceId ? resourceId.ToString() : DBNull.Value);
        command.Parameters.AddWithValue(
            "$elementStyleType",
            (object?)element.ElementStyleType ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$backgroundResourceId",
            element.BackgroundResourceId is { } backgroundId ? backgroundId.ToString() : DBNull.Value);
        command.Parameters.AddWithValue(
            "$foregroundResourceId",
            element.ForegroundResourceId is { } foregroundId ? foregroundId.ToString() : DBNull.Value);
        command.Parameters.AddWithValue(
            "$shapeResourceId",
            element.ShapeResourceId is { } shapeId ? shapeId.ToString() : DBNull.Value);
        command.Parameters.AddWithValue("$internalMute", element.InternalMute ? 1 : 0);
        command.Parameters.AddWithValue("$fromMaster", element.FromMaster ? 1 : 0);
        command.Parameters.AddWithValue(
            "$legacyMetadata",
            (object?)element.LegacyMetadata ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    private static ResourceId? ParseResourceId(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetString(ordinal);
        return Guid.TryParse(value, out var id) ? new ResourceId(id) : null;
    }
}
