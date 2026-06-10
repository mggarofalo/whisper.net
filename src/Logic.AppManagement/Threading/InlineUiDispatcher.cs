// The headless default for the IUiDispatcher seam (WHISPER-95): runs work inline on the calling
// thread. Registered as the TryAdd fallback so compositions without a UI thread — the doctor mode
// host, the host-lifecycle specs — still resolve consumers of the seam (e.g. TrayUserNotifier); the
// WPF composition root registers the real WpfUiDispatcher after it, which wins at resolution.

using Application.Ports;

namespace Logic.AppManagement.Threading;

public sealed class InlineUiDispatcher : IUiDispatcher
{
	public bool CheckAccess() => true;

	public void Post(Action action) => action();

	public Task InvokeAsync(Action action)
	{
		action();
		return Task.CompletedTask;
	}
}
