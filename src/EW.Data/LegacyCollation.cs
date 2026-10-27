using Microsoft.Data.Sqlite;

namespace EW.Data;

public static class LegacyCollation
{
    public const string Name = "UTF8_U_CI";

    public static void EnsureRegistered(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        connection.CreateCollation(
            Name,
            static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left, right));
    }
}
