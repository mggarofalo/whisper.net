// Enumerates the display monitors currently attached. A port because listing monitors (and their work
// areas and DPI) is an OS/display concern that only the WPF composition root can satisfy on Windows; it
// is implemented in Presentation and registered there, exactly like IUiDispatcher. Two callers share it:
// the overlay resolves the configured target monitor to place itself on, and the General settings picker
// lists the choices. Snapshot semantics — each call reflects the monitors present at that moment.

using Application.Display;

namespace Application.Ports;

public interface IMonitorCatalog
{
	/// <summary>The monitors currently attached, primary first; empty only if enumeration is unavailable.</summary>
	IReadOnlyList<MonitorInfo> GetMonitors();
}
