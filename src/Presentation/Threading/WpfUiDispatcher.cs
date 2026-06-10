// The one production IUiDispatcher (WHISPER-90): wraps the WPF dispatcher captured at startup —
// never Application.Current, which is null while the application tears down — and no-ops once
// dispatcher shutdown has begun, so a late controller event during exit drops a UI refresh instead
// of throwing. Post maps to BeginInvoke (never blocks the caller; the audio thread raises per-frame
// level updates through it), InvokeAsync to the dispatcher's awaitable queue.

using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using Application.Ports;

namespace Presentation.Threading;

public sealed class WpfUiDispatcher(Dispatcher dispatcher) : IUiDispatcher
{
	public bool CheckAccess() => dispatcher.CheckAccess();

	public void Post(Action action)
	{
		if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
		{
			return;
		}

		dispatcher.BeginInvoke(DispatcherPriority.Normal, action);
	}

	public Task InvokeAsync(Action action)
	{
		if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
		{
			return Task.CompletedTask;
		}

		return dispatcher.InvokeAsync(action).Task;
	}
}
