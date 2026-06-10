// The instant-apply live-propagation channel (WHISPER-78), generalizing/replacing the ad-hoc
// SettingsChangeBroadcaster (WHISPER-75). It publishes a committed, valid settings change as a typed
// SettingsChangedMessage over CommunityToolkit's IMessenger (a WeakReferenceMessenger in composition), so
// running services that registered weakly reconfigure live — within one message round-trip, no restart.
//
// Two publish modes:
//  - Publish: immediate, for a discrete committed change (a model switch, a device pick, a hotkey assign).
//  - PublishDebounced: coalesces a burst of noisy free-text/slider commits, delivering only the latest
//    after a quiet window, so a rapidly-edited free-text setting reconfigures the service once, not per
//    keystroke. The quiet window is driven by an injected TimeProvider so it is deterministic in tests.
//
// Only valid values reach this channel: callers publish after the change has passed validation and been
// persisted, so an invalid value never propagates to a live service.

using CommunityToolkit.Mvvm.Messaging;
using Domain.Settings;

namespace Application.Settings;

public sealed class SettingsChangeChannel(IMessenger messenger, TimeProvider time, TimeSpan debounceWindow) : IDisposable
{
	// Default quiet window for debounced free-text commits — long enough to coalesce a typing burst, short
	// enough to feel instant once the user pauses.
	public static readonly TimeSpan DefaultDebounceWindow = TimeSpan.FromMilliseconds(300);

	private readonly object _gate = new();
	private ITimer? _timer;
	private AppSettings? _pending;

	// Composition convenience: the default quiet window.
	public SettingsChangeChannel(IMessenger messenger, TimeProvider time)
		: this(messenger, time, DefaultDebounceWindow)
	{
	}

	// Deliver a committed change immediately so live services reconfigure within one message round-trip.
	public void Publish(AppSettings settings) => messenger.Send(new SettingsChangedMessage(settings));

	// Stage a noisy change; the latest staged value is delivered once the quiet window elapses with no
	// further staging. Each call restarts the window, so a burst collapses to a single reconfiguration.
	public void PublishDebounced(AppSettings settings)
	{
		lock (_gate)
		{
			_pending = settings;
			_timer?.Dispose();
			_timer = time.CreateTimer(_ => Flush(), state: null, debounceWindow, Timeout.InfiniteTimeSpan);
		}
	}

	public void Dispose()
	{
		lock (_gate)
		{
			_timer?.Dispose();
			_timer = null;
		}
	}

	// Deliver the latest staged value (if any) and clear the buffer. Runs off the timer when the window
	// elapses; the send happens outside the lock so a recipient's handler cannot deadlock the timer.
	private void Flush()
	{
		AppSettings? settings;
		lock (_gate)
		{
			settings = _pending;
			_pending = null;
		}

		if (settings is not null)
		{
			messenger.Send(new SettingsChangedMessage(settings));
		}
	}
}
