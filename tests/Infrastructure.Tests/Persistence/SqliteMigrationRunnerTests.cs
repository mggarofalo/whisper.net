// Inner TDD loop for the WHISPER-11 migration runner: a fresh database runs every migration to the latest
// schema version with WAL enabled, an older database is migrated forward (pending tail only), and an
// up-to-date database is a no-op. Driven against a real temp-file database; the runner is the real one.

using System.Globalization;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Infrastructure.Tests.Persistence;

public sealed class SqliteMigrationRunnerTests : IDisposable
{
	private readonly SqliteTestDatabase _fixture = new();

	[Fact]
	public void Migrates_a_fresh_database_to_the_latest_version_with_wal()
	{
		using SqliteConnection connection = _fixture.OpenRawConnection();

		int applied = _fixture.NewRunner().Migrate(connection);

		applied.Should().BeGreaterThan(0);
		ReadUserVersion(connection).Should().Be(_fixture.NewRunner().LatestVersion);
		ReadJournalMode(connection).Should().Be("wal");
		TableExists(connection, "history").Should().BeTrue();
		TableExists(connection, "settings").Should().BeTrue();
	}

	[Fact]
	public void Migrates_an_older_database_forward_applying_only_the_pending_tail()
	{
		using SqliteConnection connection = _fixture.OpenRawConnection();

		int latest = _fixture.NewRunner().LatestVersion;

		int firstWave = _fixture.NewRunner().Migrate(connection, targetVersion: 1);
		firstWave.Should().Be(1, "only the v1 migration is applied at target version 1");
		ReadUserVersion(connection).Should().Be(1);
		TableExists(connection, "settings").Should().BeFalse("the settings table arrives in a later migration");

		int secondWave = _fixture.NewRunner().Migrate(connection);

		secondWave.Should().Be(latest - 1, "every migration past v1 runs as the pending tail");
		ReadUserVersion(connection).Should().Be(latest);
		TableExists(connection, "settings").Should().BeTrue();
	}

	[Fact]
	public void Re_running_against_an_up_to_date_database_is_a_no_op()
	{
		using SqliteConnection connection = _fixture.OpenRawConnection();
		_fixture.NewRunner().Migrate(connection);
		int versionAfterFirst = ReadUserVersion(connection);

		int applied = _fixture.NewRunner().Migrate(connection);

		applied.Should().Be(0);
		ReadUserVersion(connection).Should().Be(versionAfterFirst);
	}

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
		return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture)!.ToLowerInvariant();
	}

	private static bool TableExists(SqliteConnection connection, string table)
	{
		using SqliteCommand command = connection.CreateCommand();
		command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
		command.Parameters.AddWithValue("$name", table);
		return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
	}

	public void Dispose() => _fixture.Dispose();
}
