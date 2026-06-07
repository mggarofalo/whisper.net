// Per-layer DI registration for Logic.AppManagement. This is the composition seam the Generic Host
// and the BDD specs call; it registers the real app-management behaviors so specs exercise them for
// real (only Infrastructure ports are faked).

using Application.Delivery;
using Application.Ports;
using Logic.AppManagement.Lifecycle;
using Logic.AppManagement.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Logic.AppManagement.DependencyInjection;

public static class AppManagementServiceCollectionExtensions
{
	public static IServiceCollection AddAppManagement(this IServiceCollection services, IConfiguration? configuration = null)
	{
		services.AddSingleton<IUsageStatsCalculator, UsageStatsCalculator>();

		// Delivery-strategy selection (WHISPER-8): pure override-vs-default precedence for each delivery.
		services.AddSingleton<IDeliveryStrategySelector, DeliveryStrategySelector>();

		// Hotkey activation (WHISPER-16): the stateful chord matcher that turns key edges into recording
		// start/stop requests for push-to-talk and toggle. Singleton so it holds the live chord state the
		// orchestration layer (M7) subscribes to and drives from the hotkey listener.
		services.AddSingleton<HotkeyActivationController>();

		// Recording state machine (WHISPER-22): the single authority over Idle/Recording/Transcribing and
		// the Esc cancel. Singleton so the tray/UI and orchestration share one observable state.
		services.AddSingleton<RecordingStateMachine>();

		// Hotkey capture + rebinding (WHISPER-30): the one-shot capture-next-key helper that rebinds the
		// activation controller atomically. Singleton so it shares the live controller it rebinds.
		services.AddSingleton<HotkeyCaptureService>();

		// Current settings (WHISPER-43): the in-memory holder loaded on startup and saved on shutdown.
		// Singleton so every consumer shares one live view of the settings.
		services.AddSingleton<SettingsHolder>();

		return services;
	}

	// The host-owned background components (WHISPER-12). Kept separate from AddAppManagement so the BDD
	// scenario container — which composes the inner layers but never runs a Generic Host — is not forced
	// to register hosted services; the production host (via AddWhisperServices) and the host-lifecycle
	// specs opt in explicitly.
	public static IServiceCollection AddAppManagementHostedServices(this IServiceCollection services)
	{
		// The global hotkey listener runs for the app's whole lifetime: start it on launch, stop it on
		// graceful shutdown. Registered as IHostedService so the Generic Host owns its lifecycle.
		services.AddHostedService<HotkeyListenerHostedService>();

		// Settings persistence (WHISPER-43): load the persisted settings into the holder on startup and
		// write them back on graceful shutdown, around the host lifetime.
		services.AddHostedService<SettingsLifecycleService>();

		return services;
	}
}
