// A synchronous, recording IUiDispatcher for the @WHISPER-90 scenarios and any spec that drives a
// view-model: it runs every queued action inline (deterministic — no real dispatcher loop) while
// counting how work arrived, so specs assert the marshaling contract (posted vs fast-path) without a
// live WPF Application. CheckAccess is settable: false simulates a caller on a background thread,
// true simulates already being on the UI thread (the fast-path).

using Application.Ports;

namespace Dictation.Specs.Support;

public sealed class RecordingUiDispatcher : IUiDispatcher
{
	/// <summary>What <see cref="CheckAccess"/> reports; false = the caller is off the UI thread.</summary>
	public bool IsOnUiThread { get; set; }

	public int PostCount { get; private set; }

	public int InvokeAsyncCount { get; private set; }

	public bool CheckAccess() => IsOnUiThread;

	public void Post(Action action)
	{
		PostCount++;
		action();
	}

	public Task InvokeAsync(Action action)
	{
		InvokeAsyncCount++;
		action();
		return Task.CompletedTask;
	}
}
