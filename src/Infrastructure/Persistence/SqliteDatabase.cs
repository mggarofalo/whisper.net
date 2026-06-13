// The single owner of the SQLite connection string and one-time schema initialization. It
// hands out open connections to the store adapters, running the migration runner once — lazily and
// thread-safely — on first use, so the schema is present before the first read or write (in practice "at
// startup", since the stores are touched as the host starts). Connection pooling (on by default for a
// file data source) keeps the per-call opens cheap. If initialization fails (e.g. the file is not a valid
// database), the failure surfaces to the calling store, which degrades safely rather than crashing the host.

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Infrastructure.Persistence;

public sealed class SqliteDatabase
{
	private readonly string _databasePath;
	private readonly string _connectionString;
	private readonly SqliteMigrationRunner _runner;
	private readonly Lock _initializationGate = new();
	private bool _initialized;

	public SqliteDatabase(IOptions<SqlitePersistenceOptions> options, SqliteMigrationRunner runner)
	{
		_databasePath = options.Value.DatabasePath;
		_connectionString = new SqliteConnectionStringBuilder
		{
			DataSource = _databasePath,
			Mode = SqliteOpenMode.ReadWriteCreate,
		}.ToString();
		_runner = runner;
	}

	// Opens a fresh connection to the database, ensuring the schema has been migrated to the latest version
	// first. The caller owns disposal of the returned connection.
	public async ValueTask<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
	{
		EnsureInitialized();

		SqliteConnection connection = new(_connectionString);
		await connection.OpenAsync(cancellationToken);
		return connection;
	}

	private void EnsureInitialized()
	{
		lock (_initializationGate)
		{
			if (_initialized)
			{
				return;
			}

			string? directory = Path.GetDirectoryName(_databasePath);
			if (!string.IsNullOrEmpty(directory))
			{
				Directory.CreateDirectory(directory);
			}

			using SqliteConnection connection = new(_connectionString);
			connection.Open();
			_runner.Migrate(connection);

			_initialized = true;
		}
	}
}
