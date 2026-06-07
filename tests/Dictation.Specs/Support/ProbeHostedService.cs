// A no-op hosted service whose only job is to record that the Generic Host started and stopped it,
// via the shared LifecycleProbe. Lets the @WHISPER-12 scenarios prove the host drives an arbitrary
// IHostedService through its lifecycle, independent of any production component.

using Microsoft.Extensions.Hosting;

namespace Dictation.Specs.Support;

public sealed class ProbeHostedService(LifecycleProbe probe) : IHostedService
{
	public Task StartAsync(CancellationToken cancellationToken)
	{
		probe.Started = true;
		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		probe.Stopped = true;
		return Task.CompletedTask;
	}
}
