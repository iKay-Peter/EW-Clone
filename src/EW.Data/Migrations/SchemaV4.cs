namespace EW.Data.Migrations;

internal static class SchemaV4
{
    public const string Sql = """
        ALTER TABLE ServiceInterval ADD COLUMN LegacyId TEXT;

        CREATE UNIQUE INDEX UX_ServiceInterval_LegacyId
            ON ServiceInterval (LegacyId)
            WHERE LegacyId IS NOT NULL;
        """;
}
