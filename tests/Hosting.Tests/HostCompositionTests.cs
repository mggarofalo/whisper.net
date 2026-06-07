// Verifies the Generic Host composition (WHISPER-57): a host built from the per-layer registration
// extensions resolves the Mediator that dispatches handlers, and Serilog's minimum level is honored
// from configuration (proving the layered config -> logging path). The host uses the exact same
// AddWhisperServices / AddSerilogLogging extensions the WPF app calls.

using Application.Ports;
using Infrastructure.Audio;
using Infrastructure.DependencyInjection;
using Infrastructure.Hotkeys;
using Infrastructure.Persistence;
using Logic.AppManagement.Lifecycle;
using Mediator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Hosting.Tests;

public sealed class HostCompositionTests
{
	private static IHost BuildHost(params KeyValuePair<string, string?>[] settings)
	{
		HostApplicationBuilder builder = Host.CreateApplicationBuilder();
		builder.Configuration.AddInMemoryCollection(settings);

		builder.Services.AddSerilogLogging(builder.Configuration);
		builder.Services.AddWhisperServices(builder.Configuration);

		return builder.Build();
	}

	[Fact]
	public void Resolves_the_mediator_wired_by_the_per_layer_extensions()
	{
		using IHost host = BuildHost();
		using IServiceScope scope = host.Services.CreateScope();

		Assert.NotNull(scope.ServiceProvider.GetService<IMediator>());
	}

	// WHISPER-7 AC6: the WASAPI capture port is registered by the Infrastructure layer and resolvable
	// from the production composition (constructing it must not require a real device).
	[Fact]
	public void Resolves_the_wasapi_audio_capture_port_from_infrastructure()
	{
		using IHost host = BuildHost();

		IAudioSource source = host.Services.GetRequiredService<IAudioSource>();

		Assert.IsType<WasapiAudioSource>(source);
	}

	// WHISPER-31: the Silero VAD port is registered by Infrastructure and resolvable from the
	// production composition (the ONNX model loads lazily, so resolution needs no asset present).
	[Fact]
	public void Resolves_the_silero_vad_port_from_infrastructure()
	{
		using IHost host = BuildHost();

		IVad vad = host.Services.GetRequiredService<IVad>();

		Assert.IsType<SileroVad>(vad);
	}

	// WHISPER-13 AC6: enumeration and the default-device notification client are wired through the
	// Infrastructure DI extension (both create their NAudio enumerator lazily, so resolution is safe).
	[Fact]
	public void Resolves_the_audio_device_enumeration_and_watcher_from_infrastructure()
	{
		using IHost host = BuildHost();

		Assert.NotNull(host.Services.GetService<IAudioDeviceEnumerator>());
		Assert.NotNull(host.Services.GetService<IDefaultDeviceWatcher>());
	}

	// WHISPER-10 AC2: the global hotkey listener is registered by Infrastructure and resolvable from the
	// production composition (constructing the SharpHook hook is deferred-native, so resolution is safe).
	[Fact]
	public void Resolves_the_global_hotkey_listener_from_infrastructure()
	{
		using IHost host = BuildHost();

		IHotkeyListener listener = host.Services.GetRequiredService<IHotkeyListener>();

		Assert.IsType<EventLoopHotkeyListener>(listener);
	}

	// WHISPER-12 AC1/AC3: the long-lived background components are registered as IHostedService so the
	// Generic Host owns their lifetime. The global hotkey listener is the first such component; resolving
	// the hosted-service set must surface it (constructing it installs no OS hook — Start does that).
	[Fact]
	public void Registers_the_hotkey_listener_as_a_hosted_service()
	{
		using IHost host = BuildHost();

		IEnumerable<IHostedService> hostedServices = host.Services.GetServices<IHostedService>();

		Assert.Contains(hostedServices, service => service is HotkeyListenerHostedService);
	}

	// WHISPER-11 AC1: the SQLite-backed settings and history stores are wired through the Infrastructure DI
	// extension (constructing them opens no database — the schema is initialized lazily on first use).
	[Fact]
	public void Resolves_the_sqlite_backed_persistence_ports_from_infrastructure()
	{
		using IHost host = BuildHost();

		Assert.IsType<SqliteSettingsStore>(host.Services.GetRequiredService<ISettingsStore>());
		Assert.IsType<SqliteHistoryStore>(host.Services.GetRequiredService<IHistoryStore>());
	}

	// WHISPER-11 AC2: the database file defaults to a per-user application-data path when not configured.
	[Fact]
	public void Defaults_the_database_path_to_the_per_user_app_data_location()
	{
		using IHost host = BuildHost();

		string databasePath = host.Services.GetRequiredService<IOptions<SqlitePersistenceOptions>>().Value.DatabasePath;

		string expected = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "whisper.net", "whisper.db");
		Assert.Equal(expected, databasePath);
	}

	[Fact]
	public void Honors_the_minimum_log_level_from_configuration()
	{
		using IHost host = BuildHost(new KeyValuePair<string, string?>("Serilog:MinimumLevel:Default", "Warning"));

		ILogger<HostCompositionTests> logger = host.Services.GetRequiredService<ILogger<HostCompositionTests>>();

		Assert.False(logger.IsEnabled(LogLevel.Information));
		Assert.True(logger.IsEnabled(LogLevel.Warning));
	}
}
