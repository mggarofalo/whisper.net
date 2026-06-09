// Whisper native-runtime diagnostic (WHISPER-85): reports whether the Whisper.net native library actually
// loads, via the IWhisperRuntimeProbe port. Unlike the model-cache check (is a model file present?), this
// probes the thing that silently broke in the packaged app — the native runtime WhisperFactory needs
// before it can read any model. A missing native runtime means NO transcription is possible, so it is a
// hard Fail, not a Warn; the detail carries the underlying loader message for the bug report.

using Application.Diagnostics;
using Application.Ports;

namespace Logic.AppManagement.Diagnostics;

public sealed class WhisperRuntimeCheck(IWhisperRuntimeProbe probe) : IDiagnosticCheck
{
	public string Name => "Whisper";

	public ValueTask<DiagnosticResult> RunAsync(CancellationToken cancellationToken)
	{
		WhisperRuntimeStatus status = probe.Probe();

		DiagnosticResult result = new(
			Name,
			status.IsAvailable ? DiagnosticStatus.Pass : DiagnosticStatus.Fail,
			status.Detail);

		return ValueTask.FromResult(result);
	}
}
