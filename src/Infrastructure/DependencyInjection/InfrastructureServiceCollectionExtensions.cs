// Per-layer DI registration for Infrastructure — the adapters that implement Application ports
// (Whisper.net, NAudio, ONNX VAD, SendInput, persistence). This is the composition seam the Generic
// Host calls; the BDD specs deliberately do NOT call it, substituting the ports with fakes instead.
// Concrete adapters are registered here as later modules add them.

using Application.Delivery;
using Application.Ports;
using Infrastructure.Audio;
using Infrastructure.Gpu;
using Infrastructure.Hotkeys;
using Infrastructure.Lifecycle;
using Infrastructure.Models;
using Infrastructure.Persistence;
using Infrastructure.Rephrase;
using Infrastructure.Startup;
using Infrastructure.TextDelivery;
using Infrastructure.Transcription;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
	public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration? configuration = null)
	{
		// Microphone capture (WHISPER-7): the WASAPI source over the real NAudio device client.
		services.AddSingleton<IAudioCaptureClient, NAudioCaptureClient>();
		services.AddSingleton<IAudioSource, WasapiAudioSource>();

		// Audio feedback (WHISPER-21): plays a distinct synthesized tone per dictation cue. Constructing
		// it touches no device — playback happens fire-and-forget in Play and swallows any failure.
		services.AddSingleton<IAudioFeedback, AudioFeedbackPlayer>();

		// Voice-activity detection (WHISPER-31): the Silero adapter over the on-device ONNX session.
		// The session loads its model lazily, so resolving the port never requires the asset present.
		services.AddSingleton<IVadSession>(_ => new OnnxVadSession(
			Path.Combine(AppContext.BaseDirectory, "assets", "silero_vad.onnx")));
		services.AddSingleton<IVad, SileroVad>();

		// Device enumeration + default-change notification (WHISPER-13). Both create their underlying
		// NAudio enumerator lazily, so resolving the ports touches no audio hardware.
		services.AddSingleton<IAudioDeviceEnumerator, NAudioDeviceEnumerator>();
		services.AddSingleton<IDefaultDeviceWatcher, NAudioDefaultDeviceWatcher>();

		// GPU runtime detection (WHISPER-9): the raw Vulkan-loader probe the GPU contact point consults.
		// It resolves the loader without initializing a device, so it returns promptly and never hangs.
		services.AddSingleton<IGpuProbe, VulkanGpuProbe>();

		// Transcription (WHISPER-3): the Whisper.net adapter over an internal engine seam. The model is
		// loaded lazily on first transcription, so resolving the port touches no model file or native
		// library; the model path/language come from the bound WhisperOptions.
		services.AddOptions<WhisperOptions>();
		if (configuration is not null)
		{
			services.Configure<WhisperOptions>(configuration.GetSection(WhisperOptions.SectionName));
		}

		services.AddSingleton<IWhisperEngineFactory, WhisperNetEngineFactory>();
		services.AddSingleton<ITranscriber, WhisperTranscriber>();

		// Model lifecycle runtime (WHISPER-15): the native load/warmup/transcribe/release operations the
		// lifecycle policy (Logic.ModelManagement) drives, built on the Whisper.net engine seam above.
		services.AddSingleton<IModelRuntime, WhisperModelRuntime>();

		// Model registry cache + download (WHISPER-4). Cache detection is filesystem-only (no network).
		// The downloader fetches a missing model from Hugging Face — the one model-related egress — and
		// verifies it before moving it into the cache. No background fetch is wired: a download happens
		// only when explicitly requested. The cache directory defaults to a per-user folder when unset.
		services.AddOptions<ModelCacheOptions>();
		if (configuration is not null)
		{
			services.Configure<ModelCacheOptions>(configuration.GetSection(ModelCacheOptions.SectionName));
		}

		services.PostConfigure<ModelCacheOptions>(cacheOptions =>
		{
			if (string.IsNullOrWhiteSpace(cacheOptions.CacheDirectory))
			{
				cacheOptions.CacheDirectory = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
					"whisper.net",
					"models");
			}
		});

		services.AddSingleton<IModelCache, FileSystemModelCache>();
		services.AddSingleton<IModelDownloadSource>(_ => new HuggingFaceModelDownloadSource(new HttpClient()));
		services.AddSingleton<IModelDownloader, ModelDownloader>();

		// Text delivery: the two strategies and the factory that routes between them (WHISPER-2/5/8).
		// SendInputTextInjector types Unicode keystrokes over the Win32 SendInput seam (the universal path
		// that lands even in terminals that ignore paste); ClipboardTextInjector writes text, issues Ctrl+V,
		// and restores the prior clipboard unless a concurrent copy advanced the change count. Both are
		// registered as concrete types and selected per delivery by TextInjectorFactory. Resolving any of
		// them performs no I/O; they act only when Inject is called.
		services.AddSingleton<IKeyboardInput, Win32KeyboardInput>();
		services.AddSingleton<IClipboard, Win32Clipboard>();
		services.AddSingleton<SendInputTextInjector>();
		services.AddSingleton<ClipboardTextInjector>();
		services.AddSingleton<ITextInjectorFactory, TextInjectorFactory>();

		// UIPI / elevation detection (WHISPER-6): lets the delivery pipeline detect a higher-integrity
		// foreground window and surface that synthetic input would be dropped, instead of failing silently.
		services.AddSingleton<IForegroundIntegrityProbe, Win32ForegroundIntegrityProbe>();

		// Input permissions (WHISPER-51): onboarding checks that the OS allows synthetic input + the global
		// hook. On Windows these need no separate grant, so the probe reports them present.
		services.AddSingleton<IPermissionProbe, Permissions.InputPermissionProbe>();

		// Global hotkeys (WHISPER-10): the SharpHook event-loop hook behind the IHotkeyListener port,
		// pumped on its own dedicated thread. Singleton so the single OS hook lives for the app's run.
		services.AddSingleton<IGlobalKeyHook, SharpHookGlobalKeyHook>();
		services.AddSingleton<IHotkeyListener, EventLoopHotkeyListener>();

		// Persistence (WHISPER-11): a single SQLite database backs both the settings and history ports. The
		// migration runner brings the schema to the latest version on first use (WAL mode, idempotent); the
		// database file defaults to a per-user application-data path when not configured, so a fresh install
		// needs none. No Application or Logic code references SQLite — it lives entirely behind these ports.
		services.AddOptions<SqlitePersistenceOptions>();
		if (configuration is not null)
		{
			services.Configure<SqlitePersistenceOptions>(configuration.GetSection(SqlitePersistenceOptions.SectionName));
		}

		services.PostConfigure<SqlitePersistenceOptions>(persistenceOptions =>
		{
			if (string.IsNullOrWhiteSpace(persistenceOptions.DatabasePath))
			{
				persistenceOptions.DatabasePath = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
					"whisper.net",
					"whisper.db");
			}
		});

		services.AddSingleton<SqliteMigrationRunner>();
		services.AddSingleton<SqliteDatabase>();
		services.AddSingleton<ISettingsStore, SqliteSettingsStore>();
		services.AddSingleton<IHistoryStore, SqliteHistoryStore>();
		services.AddSingleton<IAuditLog, SqliteAuditLog>();

		// Opt-in localhost AI rephrase (WHISPER-40): the single disclosed transcript-bearing network seam.
		// Disabled by default; when enabled the endpoint must be loopback, enforced by a validator that
		// rejects a remote host rather than letting it be silently used.
		OptionsBuilder<OllamaRephraseOptions> rephraseOptions = services.AddOptions<OllamaRephraseOptions>();
		if (configuration is not null)
		{
			rephraseOptions.Bind(configuration.GetSection(OllamaRephraseOptions.SectionName));
		}

		services.AddSingleton<IValidateOptions<OllamaRephraseOptions>, OllamaRephraseOptionsValidator>();
		rephraseOptions.ValidateOnStart();
		services.AddSingleton<IRephraseClient>(serviceProvider => new OllamaRephraseClient(
			new HttpClient(),
			serviceProvider.GetRequiredService<IOptions<OllamaRephraseOptions>>(),
			serviceProvider.GetRequiredService<ILogger<OllamaRephraseClient>>()));

		// Run on login (WHISPER-32): the registry-backed launch-at-login registration under the current-user
		// Run key (no elevation). The key/value default to the standard Windows Run key when not configured.
		services.AddOptions<StartupRegistrationOptions>();
		if (configuration is not null)
		{
			services.Configure<StartupRegistrationOptions>(configuration.GetSection(StartupRegistrationOptions.SectionName));
		}

		// Constructed behind an OS guard: the registry adapter is Windows-only (the whole app is), and the
		// guard is what lets this portable net10.0 assembly compose it without a platform-compat warning.
		services.AddSingleton<IStartupRegistration>(serviceProvider =>
		{
			if (!OperatingSystem.IsWindows())
			{
				throw new PlatformNotSupportedException("Run-on-login registration requires Windows.");
			}

			return new RegistryStartupRegistration(serviceProvider.GetRequiredService<IOptions<StartupRegistrationOptions>>());
		});

		// Single-instance enforcement (WHISPER-25): a named Mutex (the lock) and a named EventWaitHandle
		// (cross-process activation), both in the current-user session namespace — no elevation. Built
		// behind an OS guard for the same reason as the registry adapter above (portable net10.0 target).
		services.AddSingleton<IInstanceLock>(_ =>
		{
			if (!OperatingSystem.IsWindows())
			{
				throw new PlatformNotSupportedException("Single-instance enforcement requires Windows.");
			}

			return new MutexInstanceLock(SingleInstanceMutexName);
		});

		services.AddSingleton<IInstanceSignal>(_ =>
		{
			if (!OperatingSystem.IsWindows())
			{
				throw new PlatformNotSupportedException("Single-instance activation requires Windows.");
			}

			return new EventWaitHandleInstanceSignal(SingleInstanceActivationName);
		});

		return services;
	}

	// Stable names for the single-instance primitives. No "Global\" prefix, so they live in the current
	// user's session namespace (per-user, no elevation).
	private const string SingleInstanceMutexName = "whisper-net-single-instance";
	private const string SingleInstanceActivationName = "whisper-net-single-instance-activate";
}
