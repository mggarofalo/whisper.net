// A controllable IHostApplicationLifetime for specs that need to assert a graceful shutdown was
// requested (e.g. @WHISPER-18 "Quit"). StopApplication records the request and signals the stopping
// token, exactly as the real host lifetime would, without running a Generic Host.

using Microsoft.Extensions.Hosting;

namespace Dictation.Specs.Support;

public sealed class FakeApplicationLifetime : IHostApplicationLifetime, IDisposable
{
	private readonly CancellationTokenSource _started = new();
	private readonly CancellationTokenSource _stopping = new();
	private readonly CancellationTokenSource _stopped = new();

	public bool StopApplicationCalled { get; private set; }

	public CancellationToken ApplicationStarted => _started.Token;

	public CancellationToken ApplicationStopping => _stopping.Token;

	public CancellationToken ApplicationStopped => _stopped.Token;

	public void StopApplication()
	{
		StopApplicationCalled = true;
		_stopping.Cancel();
	}

	public void Dispose()
	{
		_started.Dispose();
		_stopping.Dispose();
		_stopped.Dispose();
	}
}
