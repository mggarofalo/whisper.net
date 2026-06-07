// Drives the @WHISPER-43 settings-persistence scenarios. It owns HOW the settings lifecycle is
// exercised so the step definitions stay one-liners: it composes the REAL SettingsLifecycleService
// (Logic) over the REAL file-backed FileSettingsStore (Infrastructure) pointed at a private temp
// directory — only the filesystem location is controlled, nothing is faked. "Launching" runs the
// hosted service's StartAsync (load into the shared holder); "shutting down" runs StopAsync (save the
// holder back to disk). A restart is a fresh holder + lifecycle over the same directory, so a value
// truly round-trips through the on-disk store.

using Application.Settings;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Domain.Settings;
using Infrastructure.Settings;
using Logic.AppManagement.Lifecycle;
using Logic.AppManagement.Settings;
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
	private readonly RecordingLogger<FileSettingsStore> _storeLogger = new();

	private SettingsHolder _holder = new();

	private string StorePath => Path.Combine(_directory, "settings.json");

	private FileSettingsStore NewStore() =>
		new(Options.Create(new SettingsStoreOptions { FilePath = StorePath }), _mapper, _storeLogger);

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
		if (File.Exists(StorePath))
		{
			File.Delete(StorePath);
		}
	}

	public Task WriteCorruptStore()
	{
		Directory.CreateDirectory(_directory);
		File.WriteAllText(StorePath, "{ this is not valid settings json ]");
		return Task.CompletedTask;
	}

	public async Task StartApplication()
	{
		// A fresh holder + lifecycle over the SAME directory models a process restart: whatever is loaded
		// comes from the on-disk store, not from in-memory state.
		_holder = new SettingsHolder();
		await NewLifecycle().StartAsync(CancellationToken.None);
	}

	public void AssertChangedSettingLoaded() => _holder.Current.Should().Be(Changed);

	public void AssertDefaultSettingsLoaded() => _holder.Current.Should().Be(AppSettings.Default);

	public void AssertStoreCreated() => File.Exists(StorePath).Should().BeTrue("a first run must create the store");

	public void AssertRecoveryLogged() =>
		_storeLogger.Entries.Should().Contain(
			entry => entry.Message.Contains("recover", StringComparison.OrdinalIgnoreCase),
			"the store should log its recovery from a corrupt file");

	public void Dispose()
	{
		if (Directory.Exists(_directory))
		{
			Directory.Delete(_directory, recursive: true);
		}
	}
}
