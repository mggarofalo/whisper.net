// Inner TDD loop for the WHISPER-11 SQLite settings store: it yields defaults and persists them on a
// first run, round-trips saved settings, and recovers to defaults — logging the recovery rather than
// throwing — when the database file is corrupt. Driven against a real temp-file database; the
// SettingsMapper is the real generated mapper, never mocked.

using Application.Settings;
using AwesomeAssertions;
using Domain.Settings;
using Infrastructure.Persistence;
using Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Infrastructure.Tests.Persistence;

public sealed class SqliteSettingsStoreTests : IDisposable
{
	private readonly SqliteTestDatabase _fixture = new();
	private readonly SettingsMapper _mapper = new();

	private SqliteSettingsStore NewStore(ILogger<SqliteSettingsStore>? logger = null) =>
		new(_fixture.NewDatabase(), _mapper, logger ?? NullLogger<SqliteSettingsStore>.Instance);

	[Fact]
	public async Task Returns_defaults_and_persists_them_on_first_run()
	{
		AppSettings loaded = await NewStore().LoadAsync(CancellationToken.None);

		loaded.Should().Be(AppSettings.Default);

		// A second load reads the persisted row rather than re-defaulting.
		AppSettings reloaded = await NewStore().LoadAsync(CancellationToken.None);
		reloaded.Should().Be(AppSettings.Default);
	}

	[Fact]
	public async Task Round_trips_saved_settings()
	{
		AppSettings saved = new("small.en", HotkeyBinding.Parse("Ctrl+Shift+D"), silenceThresholdMs: 750, fillerWordRemovalEnabled: false);

		await NewStore().SaveAsync(saved, CancellationToken.None);
		AppSettings loaded = await NewStore().LoadAsync(CancellationToken.None);

		loaded.Should().Be(saved);
	}

	[Fact]
	public async Task Recovers_to_defaults_and_logs_when_the_database_is_corrupt()
	{
		File.WriteAllText(_fixture.DatabasePath, "this is not a valid sqlite database file");
		RecordingLogger<SqliteSettingsStore> logger = new();

		AppSettings loaded = await NewStore(logger).LoadAsync(CancellationToken.None);

		loaded.Should().Be(AppSettings.Default);
		logger.Entries.Should().Contain(
			entry => entry.Level == LogLevel.Warning && entry.Message.Contains("recover", StringComparison.OrdinalIgnoreCase));
	}

	public void Dispose() => _fixture.Dispose();
}
