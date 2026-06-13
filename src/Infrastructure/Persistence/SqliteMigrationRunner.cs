// Brings a SQLite database up to the latest known schema version. The applied version is
// tracked with SQLite's built-in user_version PRAGMA, so the runner applies, in order, only the
// migrations newer than the database's current version: a fresh database runs every migration, an older
// one runs just the pending tail, and an up-to-date one runs nothing — making a second run a no-op
// (idempotent). Each migration executes inside a transaction so a failure leaves the version unchanged
// rather than a half-applied schema. The connection is switched to WAL (write-ahead logging) first — the
// durable journal mode that lets reads proceed concurrently with a writer.

using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

public sealed class SqliteMigrationRunner(ILogger<SqliteMigrationRunner> logger)
{
	// The schema version a fully-migrated database is at.
	public int LatestVersion => SchemaMigrations.LatestVersion;

	// Applies every pending migration to the open connection and returns how many ran (0 when the database
	// is already current). The connection is put into WAL mode before any migration.
	public int Migrate(SqliteConnection connection) => Migrate(connection, SchemaMigrations.LatestVersion);

	// Applies pending migrations only up to (and including) <paramref name="targetVersion"/>. Used in
	// production with the latest version; the bounded form lets tests stage a database at an older version.
	public int Migrate(SqliteConnection connection, int targetVersion)
	{
		EnableWriteAheadLogging(connection);

		int currentVersion = ReadUserVersion(connection);
		int applied = 0;

		foreach (SqliteMigration migration in SchemaMigrations.All)
		{
			if (migration.Version <= currentVersion || migration.Version > targetVersion)
			{
				continue;
			}

			using SqliteTransaction transaction = connection.BeginTransaction();

			using (SqliteCommand command = connection.CreateCommand())
			{
				command.Transaction = transaction;
				command.CommandText = migration.Sql;
				command.ExecuteNonQuery();
			}

			SetUserVersion(connection, transaction, migration.Version);
			transaction.Commit();

			applied++;
			logger.LogInformation("Applied SQLite migration to schema version {Version}.", migration.Version);
		}

		return applied;
	}

	private static void EnableWriteAheadLogging(SqliteConnection connection)
	{
		// PRAGMA journal_mode cannot run inside a transaction; it is set before any migration begins. The
		// WAL setting persists in the database file, so re-running it on an existing database is harmless.
		using SqliteCommand command = connection.CreateCommand();
		command.CommandText = "PRAGMA journal_mode=WAL;";
		command.ExecuteNonQuery();
	}

	private static int ReadUserVersion(SqliteConnection connection)
	{
		using SqliteCommand command = connection.CreateCommand();
		command.CommandText = "PRAGMA user_version;";
		return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
	}

	private static void SetUserVersion(SqliteConnection connection, SqliteTransaction transaction, int version)
	{
		// user_version cannot be parameterized; the value is an int the runner controls (never user input),
		// so interpolating it is safe. The change is transactional — it rolls back with the migration.
		using SqliteCommand command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = $"PRAGMA user_version={version.ToString(CultureInfo.InvariantCulture)};";
		command.ExecuteNonQuery();
	}
}
