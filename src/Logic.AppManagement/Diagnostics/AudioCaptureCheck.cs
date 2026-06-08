// Audio diagnostic (WHISPER-50): confirms a usable capture device is available, through the same
// IAudioDeviceEnumerator port the rest of the app records from — so the check reflects the real runtime
// path, not a bespoke probe. Fails when no capture device is present (nothing to record from); passes
// otherwise, naming how many devices were found and which one is the OS default.

using Application.Diagnostics;
using Application.Ports;
using Domain.Audio;

namespace Logic.AppManagement.Diagnostics;

public sealed class AudioCaptureCheck(IAudioDeviceEnumerator devices) : IDiagnosticCheck
{
	public string Name => "Audio";

	public ValueTask<DiagnosticResult> RunAsync(CancellationToken cancellationToken)
	{
		IReadOnlyList<AudioDevice> captureDevices = devices.GetCaptureDevices();

		if (captureDevices.Count == 0)
		{
			return ValueTask.FromResult(new DiagnosticResult(Name, DiagnosticStatus.Fail, "No capture (microphone) device is available."));
		}

		string? defaultId = devices.GetSystemDefaultId();
		AudioDevice? @default = captureDevices.FirstOrDefault(device => string.Equals(device.Id, defaultId, StringComparison.Ordinal));
		string defaultName = @default?.Name ?? "none";

		return ValueTask.FromResult(new DiagnosticResult(
			Name,
			DiagnosticStatus.Pass,
			$"{captureDevices.Count} capture device(s) available; default: {defaultName}."));
	}
}
