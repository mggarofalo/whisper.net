// Configuration for the SQLite persistence store: where the single database file lives.
// Bound from the "Persistence" configuration section; when left unset, AddInfrastructure post-configures
// it to a per-user application-data path so a fresh install needs no configuration.

namespace Infrastructure.Persistence;

public sealed class SqlitePersistenceOptions
{
	public const string SectionName = "Persistence";

	// Absolute path to the SQLite database file. Defaulted by AddInfrastructure when not configured.
	public string DatabasePath { get; set; } = string.Empty;
}
