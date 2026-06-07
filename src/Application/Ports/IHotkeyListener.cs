// Port for global hotkey detection. Implemented in Infrastructure by the SharpHook adapter (Module 5);
// faked in specs so push-to-talk press/release behavior can be raised deterministically.

using Domain.Settings;

namespace Application.Ports;

/// <summary>
/// Listens for a configured global hotkey and signals its press and release — the push-to-talk edges.
/// </summary>
/// <remarks>
/// Event-driven rather than request/response. <see cref="Pressed"/> and <see cref="Released"/> may be
/// raised on a background/OS thread; subscribers are responsible for marshaling to the thread they
/// need. <see cref="Start"/>/<see cref="Stop"/> are fast OS registrations.
/// </remarks>
public interface IHotkeyListener
{
	/// <summary>Raised when the configured hotkey is pressed (push-to-talk down).</summary>
	event EventHandler Pressed;

	/// <summary>Raised when the configured hotkey is released (push-to-talk up).</summary>
	event EventHandler Released;

	/// <summary>Begins listening for the given global hotkey.</summary>
	void Start(HotkeyBinding binding);

	/// <summary>Stops listening; no further events are raised until <see cref="Start"/> is called again.</summary>
	void Stop();
}
