// Port for ending the app's own lifetime from an Application handler — today only so a staged update can
// be applied: Velopack waits for this process to exit before it swaps in the new version and relaunches.
// Implemented in Logic.AppManagement over the Generic Host's IHostApplicationLifetime, so shutdown is the
// same graceful path as the tray's Quit; faked in specs.

namespace Application.Ports;

public interface IAppLifetime
{
	/// <summary>Requests a graceful shutdown of the app.</summary>
	void RequestShutdown();
}
