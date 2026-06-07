// Port for low-level global keyboard observation. Implemented in Infrastructure by the SharpHook
// adapter (Module 5) running on its own dedicated thread; faked in specs so key edges can be raised
// deterministically. Deliberately dumb: it reports every key-down/key-up as a domain key plus the
// live modifier set and does NOT know about bindings, push-to-talk, or toggles. Chord matching and
// activation policy live in Logic.AppManagement, which consumes this stream — that separation is what
// lets one binding pipeline serve every activation mode.

using Domain.Input;

namespace Application.Ports;

/// <summary>
/// Observes the global keyboard, raising <see cref="KeyDown"/>/<see cref="KeyUp"/> for every key edge
/// with the pressed key and the modifiers held at that moment, regardless of which window has focus.
/// </summary>
/// <remarks>
/// Event-driven rather than request/response. Events may be raised on a background/OS thread;
/// subscribers are responsible for marshaling to the thread they need. <see cref="Start"/> begins
/// observation without blocking the caller; <see cref="Stop"/> ends it.
/// </remarks>
public interface IHotkeyListener
{
	/// <summary>Raised when a key is pressed, carrying the key and the modifiers held including it.</summary>
	event EventHandler<KeyboardKeyEventArgs> KeyDown;

	/// <summary>Raised when a key is released, carrying the key and the modifiers still held after it.</summary>
	event EventHandler<KeyboardKeyEventArgs> KeyUp;

	/// <summary>Begins observing the global keyboard. Calling it while already running is a no-op.</summary>
	void Start();

	/// <summary>Stops observing; no further events are raised until <see cref="Start"/> is called again.</summary>
	void Stop();
}
