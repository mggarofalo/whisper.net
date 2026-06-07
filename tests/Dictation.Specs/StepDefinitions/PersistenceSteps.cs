// @WHISPER-11 — drives the SQLite persistence scenarios. Steps stay thin; the PersistenceDriver owns HOW
// the real migration runner and SQLite store are exercised against a private temp-file database.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class PersistenceSteps(PersistenceDriver driver)
{
	[Given(@"no database file exists")]
	public void GivenNoDatabaseFileExists() => driver.NoDatabaseFileExists();

	[Given(@"a database at an older schema version holding a transcript entry")]
	public void GivenADatabaseAtAnOlderVersionWithEntry() => driver.StageDatabaseAtOlderVersionWithEntry();

	[Given(@"a database already at the latest schema version")]
	public void GivenADatabaseAtTheLatestVersion() => driver.StageDatabaseAtLatestVersion();

	[Given(@"the database file is corrupt")]
	public void GivenTheDatabaseFileIsCorrupt() => driver.CorruptDatabaseFile();

	[When(@"the persistence store initializes")]
	[When(@"the persistence store initializes again")]
	public void WhenThePersistenceStoreInitializes() => driver.InitializePersistenceStore();

	[When(@"settings are loaded from the store")]
	public Task WhenSettingsAreLoaded() => driver.LoadSettingsFromStore();

	[Then(@"a database is created at the configured path")]
	public void ThenADatabaseIsCreated() => driver.AssertDatabaseCreated();

	[Then(@"its schema version equals the latest known version")]
	public void ThenSchemaVersionIsLatest() => driver.AssertSchemaAtLatestVersion();

	[Then(@"write-ahead logging is enabled")]
	public void ThenWriteAheadLoggingIsEnabled() => driver.AssertWriteAheadLoggingEnabled();

	[Then(@"the pending migrations are applied")]
	public void ThenPendingMigrationsApplied() => driver.AssertPendingMigrationsApplied();

	[Then(@"the existing transcript entry is preserved")]
	public void ThenExistingEntryPreserved() => driver.AssertSeededEntryPreserved();

	[Then(@"no migration runs")]
	public void ThenNoMigrationRuns() => driver.AssertNoMigrationRan();

	[Then(@"default settings are returned")]
	public void ThenDefaultSettingsReturned() => driver.AssertDefaultSettingsReturned();

	[Then(@"the store logs the recovery")]
	public void ThenTheStoreLogsTheRecovery() => driver.AssertRecoveryLogged();
}
