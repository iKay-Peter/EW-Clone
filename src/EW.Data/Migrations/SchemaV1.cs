namespace EW.Data.Migrations;

internal static class SchemaV1
{
    public const string Sql = """
        CREATE TABLE Profile (
            Id TEXT PRIMARY KEY,
            Name TEXT NOT NULL,
            CreatedUtc TEXT NOT NULL,
            LibraryRoot TEXT,
            SchemaVersion INTEGER NOT NULL
        );

        CREATE TABLE Song (
            Id TEXT PRIMARY KEY,
            LegacyId TEXT,
            Title TEXT NOT NULL,
            Author TEXT,
            Copyright TEXT,
            Tags TEXT,
            SearchText TEXT,
            LegacyMetadata TEXT,
            CreatedUtc TEXT NOT NULL,
            ModifiedUtc TEXT NOT NULL
        );

        CREATE INDEX IX_Song_Title ON Song (Title);

        CREATE TABLE SongSlide (
            Id TEXT PRIMARY KEY,
            SongId TEXT NOT NULL REFERENCES Song (Id) ON DELETE CASCADE,
            Position INTEGER NOT NULL,
            SectionLabel TEXT,
            ContentRtf TEXT NOT NULL,
            PlainText TEXT NOT NULL,
            UNIQUE (SongId, Position)
        );

        CREATE TABLE MediaAsset (
            Id TEXT PRIMARY KEY,
            LegacyId TEXT,
            Path TEXT NOT NULL,
            Hash TEXT,
            MediaType INTEGER NOT NULL,
            Width INTEGER,
            Height INTEGER,
            DurationMs INTEGER,
            TrimStartMs INTEGER,
            TrimEndMs INTEGER,
            PosterPath TEXT,
            AlphaMode INTEGER,
            Status INTEGER NOT NULL,
            ErrorCode TEXT,
            ErrorMessage TEXT,
            LegacyMetadata TEXT,
            CreatedUtc TEXT NOT NULL,
            ModifiedUtc TEXT NOT NULL
        );

        CREATE INDEX IX_MediaAsset_Path ON MediaAsset (Path);
        CREATE INDEX IX_MediaAsset_Hash ON MediaAsset (Hash);

        CREATE TABLE Theme (
            Id TEXT PRIMARY KEY,
            LegacyId TEXT,
            Name TEXT NOT NULL,
            SourcePath TEXT,
            StyleJson TEXT,
            LegacyMetadata TEXT,
            CreatedUtc TEXT NOT NULL,
            ModifiedUtc TEXT NOT NULL
        );

        CREATE TABLE Presentation (
            Id TEXT PRIMARY KEY,
            LegacyId TEXT,
            Title TEXT NOT NULL,
            Author TEXT,
            AspectRatio REAL,
            ThemeId TEXT REFERENCES Theme (Id) ON DELETE SET NULL,
            LegacyMetadata TEXT,
            CreatedUtc TEXT NOT NULL,
            ModifiedUtc TEXT NOT NULL
        );

        CREATE INDEX IX_Presentation_Title ON Presentation (Title);

        CREATE TABLE Slide (
            Id TEXT PRIMARY KEY,
            PresentationId TEXT NOT NULL REFERENCES Presentation (Id) ON DELETE CASCADE,
            Position INTEGER NOT NULL,
            ThemeId TEXT REFERENCES Theme (Id) ON DELETE SET NULL,
            Notes TEXT,
            LegacyMetadata TEXT,
            UNIQUE (PresentationId, Position)
        );

        CREATE TABLE Resource (
            Id TEXT PRIMARY KEY,
            ResourceType INTEGER NOT NULL,
            TextRtf TEXT,
            TextPlain TEXT,
            MediaAssetId TEXT REFERENCES MediaAsset (Id) ON DELETE SET NULL,
            WebUrl TEXT,
            ShapeData BLOB,
            LegacyMetadata TEXT
        );

        CREATE TABLE Element (
            Id TEXT PRIMARY KEY,
            SlideId TEXT NOT NULL REFERENCES Slide (Id) ON DELETE CASCADE,
            Position INTEGER NOT NULL,
            ElementType INTEGER NOT NULL,
            X REAL NOT NULL,
            Y REAL NOT NULL,
            Width REAL NOT NULL,
            Height REAL NOT NULL,
            StyleJson TEXT,
            ResourceId TEXT REFERENCES Resource (Id) ON DELETE SET NULL,
            UNIQUE (SlideId, Position)
        );

        CREATE TABLE Schedule (
            Id TEXT PRIMARY KEY,
            LegacyId TEXT,
            Name TEXT NOT NULL,
            ServiceDate TEXT,
            CreatedUtc TEXT NOT NULL,
            ModifiedUtc TEXT NOT NULL
        );

        CREATE TABLE ScheduleItem (
            Id TEXT PRIMARY KEY,
            ScheduleId TEXT NOT NULL REFERENCES Schedule (Id) ON DELETE CASCADE,
            Position INTEGER NOT NULL,
            ItemType INTEGER NOT NULL,
            ItemId TEXT,
            StateJson TEXT,
            UNIQUE (ScheduleId, Position)
        );

        CREATE TABLE BibleTranslation (
            Id TEXT PRIMARY KEY,
            Code TEXT NOT NULL UNIQUE,
            Name TEXT NOT NULL,
            Language TEXT,
            SourcePath TEXT,
            License TEXT,
            LegacyMetadata TEXT
        );

        CREATE TABLE BibleVerse (
            Id TEXT PRIMARY KEY,
            TranslationId TEXT NOT NULL REFERENCES BibleTranslation (Id) ON DELETE CASCADE,
            Book INTEGER NOT NULL,
            Chapter INTEGER NOT NULL,
            Verse INTEGER NOT NULL,
            Text TEXT NOT NULL,
            UNIQUE (TranslationId, Book, Chapter, Verse)
        );

        CREATE TABLE OutputProfile (
            Id TEXT PRIMARY KEY,
            Name TEXT NOT NULL,
            MonitorDeviceId TEXT,
            BoundsJson TEXT,
            OutputKind INTEGER NOT NULL,
            MarginsJson TEXT
        );

        CREATE TABLE ServiceInterval (
            Id TEXT PRIMARY KEY,
            Name TEXT,
            StartUtc TEXT,
            EndUtc TEXT,
            IsFixed INTEGER NOT NULL,
            LegacyMetadata TEXT
        );

        CREATE TABLE Alert (
            Id TEXT PRIMARY KEY,
            Name TEXT NOT NULL,
            Message TEXT,
            DurationMs INTEGER,
            IsEnabled INTEGER NOT NULL
        );

        CREATE TABLE TimerToken (
            Id TEXT PRIMARY KEY,
            Name TEXT NOT NULL,
            TokenType INTEGER NOT NULL,
            Format TEXT,
            IntervalMs INTEGER,
            ValueMs INTEGER,
            Overrun INTEGER NOT NULL
        );

        CREATE TABLE Setting (
            Key TEXT PRIMARY KEY,
            Value TEXT,
            ValueType TEXT,
            LegacyMetadata TEXT
        );

        CREATE TABLE LegacyImport (
            Id TEXT PRIMARY KEY,
            SourcePath TEXT NOT NULL,
            SourceHash TEXT NOT NULL,
            Status INTEGER NOT NULL,
            WarningsJson TEXT,
            RawSidecarJson TEXT,
            ImportedUtc TEXT NOT NULL,
            UNIQUE (SourcePath, SourceHash)
        );

        CREATE VIRTUAL TABLE SongFts USING fts5(
            SongId UNINDEXED,
            Title,
            Author,
            Tags,
            Lyrics,
            tokenize = 'unicode61'
        );

        CREATE VIRTUAL TABLE MediaFts USING fts5(
            MediaAssetId UNINDEXED,
            Title,
            Tags,
            tokenize = 'unicode61'
        );

        CREATE VIRTUAL TABLE PresentationFts USING fts5(
            PresentationId UNINDEXED,
            Title,
            Author,
            Tags,
            tokenize = 'unicode61'
        );

        CREATE VIRTUAL TABLE ThemeFts USING fts5(
            ThemeId UNINDEXED,
            Name,
            Tags,
            tokenize = 'unicode61'
        );

        CREATE VIRTUAL TABLE BibleFts USING fts5(
            TranslationId UNINDEXED,
            Reference,
            Text,
            tokenize = 'unicode61'
        );
        """;
}
