namespace EW.Data.Migrations;

internal static class SchemaV2
{
    public const string Sql = """
        CREATE UNIQUE INDEX UX_Song_LegacyId
            ON Song (LegacyId)
            WHERE LegacyId IS NOT NULL;

        CREATE UNIQUE INDEX UX_MediaAsset_LegacyId
            ON MediaAsset (LegacyId)
            WHERE LegacyId IS NOT NULL;

        CREATE UNIQUE INDEX UX_Theme_LegacyId
            ON Theme (LegacyId)
            WHERE LegacyId IS NOT NULL;

        CREATE UNIQUE INDEX UX_Presentation_LegacyId
            ON Presentation (LegacyId)
            WHERE LegacyId IS NOT NULL;

        CREATE UNIQUE INDEX UX_Schedule_LegacyId
            ON Schedule (LegacyId)
            WHERE LegacyId IS NOT NULL;
        """;
}
