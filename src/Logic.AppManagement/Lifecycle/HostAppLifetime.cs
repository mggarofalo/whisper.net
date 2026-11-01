// Implements the IAppLifetime port over the Generic Host's IHostApplicationLifetime: a shutdown request
// takes the same graceful path as the tray's Quit (hosted services stop, settings are saved, the WPF app
// closes), which is what a staged update needs before Velopack can apply it.

using Application.Ports;
using Microsoft.Extensions.Hosting;

namespace Logic.AppManagement.Lifecycle;

public sealed class HostAppLifetime(IHostApplicationLifetime lifetime) : IAppLifetime
{
	public void RequestShutdown() => lifetime.StopApplication();
}
