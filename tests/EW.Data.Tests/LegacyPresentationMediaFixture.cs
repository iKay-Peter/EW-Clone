using Microsoft.Data.Sqlite;

namespace EW.Data.Tests;

internal sealed class LegacyPresentationMediaFixture : IDisposable
{
    public LegacyPresentationMediaFixture(string fileHash)
    {
        SQLitePCL.Batteries_V2.Init();
        Root = Path.Combine(Path.GetTempPath(), "EWCloneTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        DatabasePath = Path.Combine(Root, "PresentationLayouts.db");

        using var connection = OpenDatabase(DatabasePath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE presentation (
                rowid INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL UNIQUE,
                presentation_uid TEXT,
                title TEXT COLLATE UTF8_U_CI,
                aspect_ratio TEXT,
                order_index INTEGER
            );

            CREATE TABLE slide (
                rowid INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL UNIQUE,
                presentation_id INTEGER NOT NULL,
                order_index INTEGER
            );

            CREATE TABLE element (
                rowid INTEGER PRIMARY KEY NOT NULL UNIQUE,
                slide_id INTEGER,
                element_type int8,
                element_style_type int8,
                order_index INTEGER,
                x double,
                y double,
                width double,
                height double,
                background_resource_id INTEGER,
                foreground_resource_id INTEGER,
                shape_resource_id INTEGER,
                internal_mute boolean,
                from_master boolean
            );

            CREATE TABLE resource (
                rowid INTEGER PRIMARY KEY NOT NULL UNIQUE,
                resource_uid TEXT,
                resource_type int8,
                title TEXT COLLATE UTF8_U_CI
            );

            CREATE TABLE file (
                rowid INTEGER PRIMARY KEY NOT NULL UNIQUE,
                file_hash TEXT,
                filename TEXT,
                shared_filename TEXT
            );

            CREATE TABLE resource_image (
                rowid INTEGER PRIMARY KEY NOT NULL UNIQUE,
                resource_id INTEGER,
                file_id INTEGER,
                original_filename TEXT,
                filesize int64,
                modified_date datetime,
                width INTEGER,
                height INTEGER
            );

            INSERT INTO presentation (presentation_uid, title, aspect_ratio, order_index)
            VALUES ('media-pres-1', 'Media Presentation', '16:9', 1);

            INSERT INTO slide (presentation_id, order_index) VALUES (1, 1);

            INSERT INTO element (
                slide_id, element_type, element_style_type, order_index, x, y,
                width, height, foreground_resource_id, internal_mute, from_master)
            VALUES (1, 1, 1, 1, 0, 0, 1920, 1080, 1, 0, 0);

            INSERT INTO resource (resource_uid, resource_type, title)
            VALUES ('media-resource-1', 1, 'Photo');

            INSERT INTO file (file_hash, filename, shared_filename)
            VALUES ($hash, 'photo.png', NULL);

            INSERT INTO resource_image (
                resource_id, file_id, original_filename, filesize, modified_date, width, height)
            VALUES (1, 1, '<images>photo.png', 11, '2026-10-27', 800, 600);
            """;
        command.Parameters.AddWithValue("$hash", fileHash);
        command.ExecuteNonQuery();
    }

    public string Root { get; }

    public string DatabasePath { get; }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    private static SqliteConnection OpenDatabase(string path)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = false,
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        connection.Open();
        LegacyCollation.EnsureRegistered(connection);
        return connection;
    }
}
