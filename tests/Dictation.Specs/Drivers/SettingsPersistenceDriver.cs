// Drives the @WHISPER-43 settings-persistence scenarios. It owns HOW the settings lifecycle is
// exercised so the step definitions stay one-liners: it composes the REAL SettingsLifecycleService
// (Logic) over the REAL SQLite-backed SqliteSettingsStore (Infrastructure) pointed at a private temp
// database file — only the storage location is controlled, nothing is faked. "Launching" runs the
// hosted service's StartAsync (load into the shared holder); "shutting down" runs StopAsync (save the
// holder back to the store). A restart is a fresh holder + lifecycle (and a fresh SqliteDatabase, whose
// one-time init is per-instance) over the same file, so a value truly round-trips through the on-disk store.

using Application.Settings;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Domain.Settings;
using Infrastructure.Persistence;
using Logic.AppManagement.Lifecycle;
using Logic.AppManagement.Settings;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dictation.Specs.Drivers;

public sealed class SettingsPersistenceDriver : IDisposable
{
	// A representative changed value, distinct from AppSettings.Default, used to prove a restart reloads
	// exactly what was saved.
	private static readonly AppSettings Changed =
		new("small.en", HotkeyBinding.Parse("Ctrl+Shift+D"), silenceThresholdMs: 750, fillerWordRemovalEnabled: false);

	private readonly string _directory = Path.Combine(Path.GetTempPath(), $"whisper-settings-{Guid.NewGuid():N}");
	private readonly SettingsMapper _mapper = new();
	private readonly RecordingLogger<SqliteSettingsStore> _storeLogger = new();

	private SettingsHolder _holder = new();

	private string StorePath => Path.Combine(_directory, "whisper.db");

	// A fresh SqliteDatabase per call models a process restart: its one-time schema initialization runs
	// again (idempotently) against the same file, so whatever is loaded comes from disk, not memory.
	private SqliteDatabase NewDatabase() =>
		new(Options.Create(new SqlitePersistenceOptions { DatabasePath = StorePath }),
			new SqliteMigrationRunner(NullLogger<SqliteMigrationRunner>.Instance));

	private SqliteSettingsStore NewStore() => new(NewDatabase(), _mapper, _storeLogger);

	private SettingsLifecycleService NewLifecycle() =>
		new(NewStore(), _holder, NullLogger<SettingsLifecycleService>.Instance);

	public async Task ChangeASettingAndShutDownGracefully()
	{
		// First launch: load (defaults, creating the store), change a setting, then graceful shutdown saves.
		SettingsLifecycleService lifecycle = NewLifecycle();
		await lifecycle.StartAsync(CancellationToken.None);
		_holder.Current = Changed;
		await lifecycle.StopAsync(CancellationToken.None);
	}

	public void EnsureNoStoreExists()
	{
		// Clear pools first so no lingering handle keeps the database (or its WAL sidecars) open on Windows.
		SqliteConnection.ClearAllPools();
		foreach (string path in StoreFiles())
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
	}

	public Task WriteCorruptStore()
	{
		Directory.CreateDirectory(_directory);
		File.WriteAllText(StorePath, "this is not a valid sqlite database file");
		return Task.CompletedTask;
	}

	public async Task StartApplication()
	{
		// A fresh holder + lifecycle over the SAME file models a process restart: whatever is loaded comes
		// from the on-disk store, not from in-memory state.
		_holder = new SettingsHolder();
		await NewLifecycle().StartAsync(CancellationToken.None);
	}

	public void AssertChangedSettingLoaded() => _holder.Current.Should().Be(Changed);

	public void AssertDefaultSettingsLoaded() => _holder.Current.Should().Be(AppSettings.Default);

	public void AssertStoreCreated() => File.Exists(StorePath).Should().BeTrue("a first run must create the store");

	public void AssertRecoveryLogged() =>
		_storeLogger.Entries.Should().Contain(
			entry => entry.Message.Contains("recover", StringComparison.OrdinalIgnoreCase),
			"the store should log its recovery from a corrupt database");

	public void Dispose()
	{
		// Release pooled connections so the temp database file is not held open while the directory is deleted.
		SqliteConnection.ClearAllPools();
		if (Directory.Exists(_directory))
		{
			Directory.Delete(_directory, recursive: true);
		}
	}

	// The main database file plus the WAL sidecars SQLite creates in write-ahead-logging mode.
	private IEnumerable<string> StoreFiles() =>
		[StorePath, StorePath + "-wal", StorePath + "-shm"];
}
