// Inner TDD loop for the WHISPER-43 file-backed settings store: it persists settings as JSON and reads
// them back (round-trip), yields defaults and creates the store on a first run with no file, and
// recovers to defaults — logging the recovery rather than throwing — when the file is corrupt. Driven
// against a real temp directory; the SettingsMapper is the real generated mapper, never mocked.

using Application.Settings;
using AwesomeAssertions;
using Domain.Settings;
using Infrastructure.Settings;
using Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Infrastructure.Tests.Settings;

public sealed class FileSettingsStoreTests : IDisposable
{
	private readonly string _directory = Path.Combine(Path.GetTempPath(), $"whisper-settings-{Guid.NewGuid():N}");
	private readonly string _path;
	private readonly SettingsMapper _mapper = new();

	public FileSettingsStoreTests() => _path = Path.Combine(_directory, "settings.json");

	private FileSettingsStore NewStore(ILogger<FileSettingsStore>? logger = null) =>
		new(Options.Create(new SettingsStoreOptions { FilePath = _path }), _mapper, logger ?? NullLogger<FileSettingsStore>.Instance);

	[Fact]
	public async Task Returns_defaults_and_creates_the_store_on_first_run()
	{
		FileSettingsStore store = NewStore();

		AppSettings loaded = await store.LoadAsync(CancellationToken.None);

		loaded.Should().Be(AppSettings.Default);
		File.Exists(_path).Should().BeTrue("a first run must create the store");
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
	public async Task Recovers_to_defaults_and_logs_when_the_store_is_corrupt()
	{
		Directory.CreateDirectory(_directory);
		File.WriteAllText(_path, "{ this is not valid settings json ]");
		RecordingLogger<FileSettingsStore> logger = new();

		AppSettings loaded = await NewStore(logger).LoadAsync(CancellationToken.None);

		loaded.Should().Be(AppSettings.Default);
		logger.Entries.Should().Contain(
			entry => entry.Level == LogLevel.Warning && entry.Message.Contains("recover", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task Recovers_to_defaults_when_stored_values_are_invalid()
	{
		// Syntactically valid JSON but a value the domain rejects (empty model id): treated as corrupt.
		Directory.CreateDirectory(_directory);
		File.WriteAllText(_path, """{ "ModelId": "", "Hotkey": "Ctrl+Win", "SilenceThresholdMs": 500, "FillerWordRemovalEnabled": true, "CaptureDeviceId": "default" }""");

		AppSettings loaded = await NewStore().LoadAsync(CancellationToken.None);

		loaded.Should().Be(AppSettings.Default);
	}

	public void Dispose()
	{
		if (Directory.Exists(_directory))
		{
			Directory.Delete(_directory, recursive: true);
		}
	}
}
