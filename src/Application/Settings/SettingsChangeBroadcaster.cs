// A tiny in-process signal that the persisted settings changed (WHISPER-76). UpdateSettingsHandler
// raises it after a successful save; higher layers (Logic.AppManagement) subscribe to apply a change to
// running services WITHOUT a restart — e.g. rebinding the live hotkey matcher. It lives in Application
// (which the settings handler and the subscriber both reference) rather than using a Mediator
// notification, because the source-generated Mediator only scans the Application assembly for handlers,
// so a handler in a higher layer would never be discovered. Singleton, so publisher and subscribers
// share one instance.

using Domain.Settings;

namespace Application.Settings;

public sealed class SettingsChangeBroadcaster
{
	/// <summary>Raised, after a successful settings save, with the new settings. Subscribers apply them live.</summary>
	public event EventHandler<AppSettings>? Changed;

	public void Raise(AppSettings settings) => Changed?.Invoke(this, settings);
}
