// Shared test fixture for the SQLite persistence adapters: a private temp-file database that builds real
// SqliteDatabase / SqliteMigrationRunner instances over it and, on disposal, clears the connection pool
// before deleting the directory so no lingering handle keeps the file (or its WAL sidecars) open on Windows.

using Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Infrastructure.Tests.Persistence;

internal sealed class SqliteTestDatabase : IDisposable
{
	public SqliteTestDatabase()
	{
		Directory = Path.Combine(Path.GetTempPath(), $"whisper-sqlite-{Guid.NewGuid():N}");
		System.IO.Directory.CreateDirectory(Directory);
		DatabasePath = Path.Combine(Directory, "whisper.db");
	}

	public string Directory { get; }

	public string DatabasePath { get; }

	public SqliteMigrationRunner NewRunner() => new(NullLogger<SqliteMigrationRunner>.Instance);

	public SqliteDatabase NewDatabase() =>
		new(Options.Create(new SqlitePersistenceOptions { DatabasePath = DatabasePath }), NewRunner());

	public SqliteConnection OpenRawConnection()
	{
		SqliteConnection connection = new(new SqliteConnectionStringBuilder
		{
			DataSource = DatabasePath,
			Mode = SqliteOpenMode.ReadWriteCreate,
		}.ToString());
		connection.Open();
		return connection;
	}

	public void Dispose()
	{
		SqliteConnection.ClearAllPools();
		if (System.IO.Directory.Exists(Directory))
		{
			System.IO.Directory.Delete(Directory, recursive: true);
		}
	}
}
