// The UI-thread marshaling seam (WHISPER-90). View-models (and any logic that must touch UI-bound
// state) marshal through this port instead of hand-rolling calls against the WPF application's
// dispatcher, so they are unit-testable with a synchronous test dispatcher and null-safe at shutdown,
// where the live application object is already gone. The seam is
// deliberately non-blocking: there is no synchronous Invoke, so a high-frequency producer (the audio
// thread raising per-frame level updates) can never be blocked by the UI thread.

namespace Application.Ports;

public interface IUiDispatcher
{
	/// <summary>Whether the caller is already on the UI thread (the CheckAccess fast-path).</summary>
	bool CheckAccess();

	/// <summary>Queues <paramref name="action"/> onto the UI thread without blocking the caller.</summary>
	void Post(Action action);

	/// <summary>Queues <paramref name="action"/> onto the UI thread and returns a task that completes when it ran.</summary>
	Task InvokeAsync(Action action);
}
