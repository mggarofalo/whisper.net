// GPU diagnostic (WHISPER-50): reports whether Vulkan acceleration is available, going through the single
// GPU contact point (IBackendSelector) so the verdict matches the backend transcription would actually
// select. Crucially this check never fails: a missing or broken GPU runtime is a supported, expected
// state, so it is reported as a Warn that the app will run on CPU — not a Fail. The selector already
// turns a throwing probe into a CPU selection, so the reason is always safe to surface verbatim.

using Application.Diagnostics;
using Application.Ports;
using Domain.Models;

namespace Logic.AppManagement.Diagnostics;

public sealed class GpuCheck(IBackendSelector backendSelector) : IDiagnosticCheck
{
	public string Name => "GPU";

	public async ValueTask<DiagnosticResult> RunAsync(CancellationToken cancellationToken)
	{
		BackendSelection selection = await backendSelector.SelectBackendAsync(cancellationToken);

		// GPU present -> Pass; CPU fallback -> Warn (usable, but the user should know it is not accelerated).
		DiagnosticStatus status = selection.IsGpu ? DiagnosticStatus.Pass : DiagnosticStatus.Warn;

		return new DiagnosticResult(Name, status, selection.Reason);
	}
}
