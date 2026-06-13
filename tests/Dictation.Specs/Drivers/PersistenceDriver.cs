// Drives the SQLite persistence scenarios against a private temp-file database. Unlike most
// drivers, persistence is the Infrastructure seam itself, so this composes the REAL SqliteMigrationRunner,
// SqliteDatabase, and SqliteSettingsStore directly against the file rather than going through the faked
// ports — that is the whole point of the issue. It stages a database at a chosen schema version (the
// runner's bounded Migrate overload), runs the full migration, and inspects the resulting user_version,
// journal mode, applied count, and preserved data.

using System.Globalization;
using Application.Settings;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Domain.History;
using Domain.Settings;
using Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dictation.Specs.Drivers;

public sealed class PersistenceDriver : IDisposable
{
	private static readonly Guid SeededEntryId = Guid.Parse("11111111-1111-1111-1111-111111111111");

	private readonly string _directory = Path.Combine(Path.GetTempPath(), $"whisper-persistence-{Guid.NewGuid():N}");
	private readonly SqliteMigrationRunner _runner = new(NullLogger<SqliteMigrationRunner>.Instance);
	private readonly RecordingLogger<SqliteSettingsStore> _storeLogger = new();

	private int _appliedCount;
	private int _schemaVersionAfter;
	private bool _walEnabled;
	private bool _seededEntryPreserved;
	private AppSettings? _loadedSettings;

	private string DatabasePath => Path.Combine(_directory, "whisper.db");

	private string ConnectionString => new SqliteConnectionStringBuilder
	{
		DataSource = DatabasePath,
		Mode = SqliteOpenMode.ReadWriteCreate,
	}.ToString();

	private SqliteConnection OpenRawConnection()
	{
		Directory.CreateDirectory(_directory);
		SqliteConnection connection = new(ConnectionString);
		connection.Open();
		return connection;
	}

	// --- Given ---------------------------------------------------------------------------------------

	public void NoDatabaseFileExists()
	{
		SqliteConnection.ClearAllPools();
		if (Directory.Exists(_directory))
		{
			Directory.Delete(_directory, recursive: true);
		}
	}

	public void StageDatabaseAtOlderVersionWithEntry()
	{
		using SqliteConnection connection = OpenRawConnection();

		// Stage the database at v1 only (history table), so a later full migration has a real pending tail.
		_runner.Migrate(connection, targetVersion: 1);

		TranscriptEntry entry = new(SeededEntryId, "seeded transcription", new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero));
		using SqliteCommand insert = connection.CreateCommand();
		insert.CommandText =
			"""
			INSERT INTO history (id, text, created_at, created_ticks, word_count)
			VALUES ($id, $text, $created_at, $created_ticks, $word_count);
			""";
		insert.Parameters.AddWithValue("$id", entry.Id.ToString());
		insert.Parameters.AddWithValue("$text", entry.Text);
		insert.Parameters.AddWithValue("$created_at", entry.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
		insert.Parameters.AddWithValue("$created_ticks", entry.CreatedAt.UtcTicks);
		insert.Parameters.AddWithValue("$word_count", entry.WordCount);
		insert.ExecuteNonQuery();
	}

	public void StageDatabaseAtLatestVersion()
	{
		using SqliteConnection connection = OpenRawConnection();
		_runner.Migrate(connection);
	}

	public void CorruptDatabaseFile()
	{
		Directory.CreateDirectory(_directory);
		File.WriteAllText(DatabasePath, "this is not a valid sqlite database file");
	}

	// --- When ----------------------------------------------------------------------------------------

	public void InitializePersistenceStore()
	{
		using SqliteConnection connection = OpenRawConnection();
		_appliedCount = _runner.Migrate(connection);
		_schemaVersionAfter = ReadUserVersion(connection);
		_walEnabled = ReadJournalMode(connection).Equals("wal", StringComparison.OrdinalIgnoreCase);
		_seededEntryPreserved = HistoryContains(connection, SeededEntryId);
	}

	public async Task LoadSettingsFromStore()
	{
		SqliteDatabase database = new(
			Options.Create(new SqlitePersistenceOptions { DatabasePath = DatabasePath }),
			new SqliteMigrationRunner(NullLogger<SqliteMigrationRunner>.Instance));

		SqliteSettingsStore store = new(database, new SettingsMapper(), _storeLogger);
		_loadedSettings = await store.LoadAsync(CancellationToken.None);
	}

	// --- Then ----------------------------------------------------------------------------------------

	public void AssertDatabaseCreated() =>
		File.Exists(DatabasePath).Should().BeTrue("initializing the store must create the database file");

	public void AssertSchemaAtLatestVersion() =>
		_schemaVersionAfter.Should().Be(_runner.LatestVersion);

	public void AssertWriteAheadLoggingEnabled() =>
		_walEnabled.Should().BeTrue("connections must use WAL mode");

	public void AssertPendingMigrationsApplied() =>
		_appliedCount.Should().BeGreaterThan(0, "an older database must have pending migrations applied");

	public void AssertSeededEntryPreserved() =>
		_seededEntryPreserved.Should().BeTrue("migrating forward must not lose existing user data");

	public void AssertNoMigrationRan() =>
		_appliedCount.Should().Be(0, "an up-to-date database must not run any migration");

	public void AssertDefaultSettingsReturned() =>
		_loadedSettings.Should().Be(AppSettings.Default);

	public void AssertRecoveryLogged() =>
		_storeLogger.Entries.Should().Contain(
			entry => entry.Message.Contains("recover", StringComparison.OrdinalIgnoreCase),
			"the store should log its recovery from a corrupt database");

	// --- Helpers -------------------------------------------------------------------------------------

	private static int ReadUserVersion(SqliteConnection connection)
	{
		using SqliteCommand command = connection.CreateCommand();
		command.CommandText = "PRAGMA user_version;";
		return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
	}

	private static string ReadJournalMode(SqliteConnection connection)
	{
		using SqliteCommand command = connection.CreateCommand();
		command.CommandText = "PRAGMA journal_mode;";
		return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ?? string.Empty;
	}

	private static bool HistoryContains(SqliteConnection connection, Guid id)
	{
		using SqliteCommand command = connection.CreateCommand();
		command.CommandText = "SELECT COUNT(*) FROM history WHERE id = $id;";
		command.Parameters.AddWithValue("$id", id.ToString());
		return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
	}

	public void Dispose()
	{
		SqliteConnection.ClearAllPools();
		if (Directory.Exists(_directory))
		{
			Directory.Delete(_directory, recursive: true);
		}
	}
}
