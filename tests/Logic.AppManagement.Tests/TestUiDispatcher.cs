// A synchronous IUiDispatcher for unit tests (WHISPER-90): runs queued work inline — no dispatcher
// loop, no live WPF Application — while recording how it arrived, so tests assert the marshaling
// contract (posted vs CheckAccess fast-path) deterministically.

using Application.Ports;

namespace Logic.AppManagement.Tests;

public sealed class TestUiDispatcher : IUiDispatcher
{
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
