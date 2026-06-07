// The ordered, forward-only schema migrations for the SQLite store (WHISPER-11). Each migration's
// Version is the schema version (SQLite's built-in user_version PRAGMA) the database is at AFTER it is
// applied; the runner applies, in order, every migration whose Version exceeds the database's current
// user_version. Migrations are additive and are never edited once shipped — a new schema change is a new
// entry with the next Version. Splitting the initial schema across two migrations is deliberate: it gives
// the runner a real ordered sequence to exercise (fresh install runs both; an older database created
// before v2 runs only the pending tail).

namespace Infrastructure.Persistence;

internal sealed record SqliteMigration(int Version, string Sql);

internal static class SchemaMigrations
{
	public static IReadOnlyList<SqliteMigration> All { get; } =
	[
		// v1: the transcription history table. created_at keeps the original DateTimeOffset as a round-trip
		// ("O") string for lossless reload; created_ticks (UtcTicks) gives correct chronological ordering and
		// range filtering regardless of each entry's offset, indexed for newest-first reads.
		new(1, """
			CREATE TABLE history (
				id            TEXT    NOT NULL PRIMARY KEY,
				text          TEXT    NOT NULL,
				created_at    TEXT    NOT NULL,
				created_ticks INTEGER NOT NULL,
				word_count    INTEGER NOT NULL
			);
			CREATE INDEX ix_history_created_ticks ON history (created_ticks DESC);
			"""),

		// v2: the settings document table — a single row (id = 0) holding the settings DTO as JSON, so the
		// stored shape stays free of domain construction rules.
		new(2, """
			CREATE TABLE settings (
				id       INTEGER NOT NULL PRIMARY KEY CHECK (id = 0),
				document TEXT    NOT NULL
			);
			"""),
	];

	public static int LatestVersion => All[^1].Version;
}
