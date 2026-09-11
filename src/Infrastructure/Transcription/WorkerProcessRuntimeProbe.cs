using System.Diagnostics;
using Application.Ports;
using Microsoft.Extensions.Options;

namespace Infrastructure.Transcription;

internal sealed class WorkerProcessRuntimeProbe(IOptions<InferenceWorkerOptions> options) : IWhisperRuntimeProbe
{
	private readonly InferenceWorkerOptions _options = options.Value;

	public WhisperRuntimeStatus Probe()
	{
		string executable = Path.GetFullPath(_options.ExecutablePath);
		if (!File.Exists(executable))
		{
			return new WhisperRuntimeStatus(false, $"Inference worker was not found at '{executable}'.");
		}

		using Process process = Process.Start(new ProcessStartInfo(executable, "--probe")
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			WindowStyle = ProcessWindowStyle.Hidden,
		}) ?? throw new InvalidOperationException("Windows did not start the inference worker probe.");

		if (!process.WaitForExit((int)_options.ConnectTimeout.TotalMilliseconds))
		{
			process.Kill(entireProcessTree: true);
			return new WhisperRuntimeStatus(false, "Inference worker probe timed out.");
		}

		return process.ExitCode == 0
			? new WhisperRuntimeStatus(true, "Inference worker and Whisper native runtime loaded.")
			: new WhisperRuntimeStatus(false, $"Inference worker native-runtime probe failed with exit code {process.ExitCode}.");
	}
}
