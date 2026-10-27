namespace EW.Data.Migrations;

internal static class SchemaV3
{
    public const string Sql = """
        ALTER TABLE Resource ADD COLUMN LegacyId TEXT;

        CREATE UNIQUE INDEX UX_Resource_LegacyId
            ON Resource (LegacyId)
            WHERE LegacyId IS NOT NULL;

        ALTER TABLE Element ADD COLUMN ElementStyleType INTEGER;
        ALTER TABLE Element ADD COLUMN BackgroundResourceId TEXT;
        ALTER TABLE Element ADD COLUMN ForegroundResourceId TEXT;
        ALTER TABLE Element ADD COLUMN ShapeResourceId TEXT;
        ALTER TABLE Element ADD COLUMN InternalMute INTEGER NOT NULL DEFAULT 0;
        ALTER TABLE Element ADD COLUMN FromMaster INTEGER NOT NULL DEFAULT 0;
        ALTER TABLE Element ADD COLUMN LegacyMetadata TEXT;
        """;
}
