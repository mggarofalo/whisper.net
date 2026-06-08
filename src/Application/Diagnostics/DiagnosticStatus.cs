// The outcome of a single self-diagnostic check (WHISPER-50). Three levels so the doctor report can
// distinguish "working" from "degraded but usable" from "broken": Pass is healthy, Warn is a usable
// fallback the user should know about (e.g. running on CPU because no Vulkan GPU was found), and Fail
// is a subsystem the app needs that is currently unavailable. The numeric order is severity order, so
// the overall report status is simply the worst result.

namespace Application.Diagnostics;

public enum DiagnosticStatus
{
	Pass = 0,
	Warn = 1,
	Fail = 2,
}
