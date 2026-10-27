using Microsoft.Data.Sqlite;

namespace EW.Data.Tests;

internal sealed class LegacyPresentationFixture : IDisposable
{
    public LegacyPresentationFixture()
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
                presentation_global_uid TEXT,
                title TEXT COLLATE UTF8_U_CI,
                author TEXT COLLATE UTF8_U_CI,
                aspect_ratio TEXT,
                order_index INTEGER,
                future_field TEXT
            );

            CREATE TABLE slide (
                rowid INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL UNIQUE,
                presentation_id INTEGER NOT NULL,
                title TEXT,
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
                from_master boolean,
                future_field TEXT
            );

            CREATE TABLE resource (
                rowid INTEGER PRIMARY KEY NOT NULL UNIQUE,
                resource_uid TEXT,
                resource_type int8,
                title TEXT COLLATE UTF8_U_CI,
                author TEXT COLLATE UTF8_U_CI,
                tags TEXT COLLATE UTF8_U_CI,
                future_field TEXT
            );

            CREATE TABLE resource_text (
                rowid INTEGER PRIMARY KEY NOT NULL UNIQUE,
                resource_id INTEGER,
                rtf RTF
            );

            INSERT INTO presentation (
                presentation_uid, presentation_global_uid, title, author,
                aspect_ratio, order_index, future_field)
            VALUES (
                'pres-uid-1', 'pres-global-1', 'Synthetic Presentation',
                'Test Author', '16:9', 1, 'preserve-presentation');

            INSERT INTO slide (presentation_id, title, order_index)
            VALUES (1, 'Slide One', 1), (1, 'Slide Two', 2);

            INSERT INTO element (
                slide_id, element_type, element_style_type, order_index,
                x, y, width, height, background_resource_id, foreground_resource_id,
                shape_resource_id, internal_mute, from_master, future_field)
            VALUES (
                1, 1, 2, 1, 10.5, 20.5, 300.0, 100.0, NULL, 1,
                NULL, 0, 1, 'preserve-element');

            INSERT INTO element (
                slide_id, element_type, element_style_type, order_index,
                x, y, width, height, background_resource_id, foreground_resource_id,
                shape_resource_id, internal_mute, from_master, future_field)
            VALUES (
                2, 2, NULL, 1, 0.0, 0.0, 1024.0, 768.0, NULL, NULL,
                NULL, 0, 0, 'second-element');

            INSERT INTO resource (
                resource_uid, resource_type, title, author, tags, future_field)
            VALUES ('res-uid-1', 3, 'Text Resource', 'Test Author', 'tag', 'preserve-resource');

            INSERT INTO resource_text (resource_id, rtf)
            VALUES (1, '{\rtf1\ansi Hello resource}');
            """;
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
