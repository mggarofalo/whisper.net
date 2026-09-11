using System.IO.Pipes;
using Domain.Models;

namespace Inference.Worker;

internal static class InferenceWorkerProgram
{
	public static async Task<int> RunAsync(string[] args)
	{
		if (args.Contains("--probe", StringComparer.Ordinal))
		{
			return NativeWhisperRuntime.Probe() ? 0 : 2;
		}

		string? pipeName = ValueAfter(args, "--pipe");
		string? backendValue = ValueAfter(args, "--backend");
		if (string.IsNullOrWhiteSpace(pipeName) || backendValue is not ("vulkan" or "cpu"))
		{
			return 64;
		}

		ComputeBackend backend = backendValue == "vulkan" ? ComputeBackend.Vulkan : ComputeBackend.Cpu;
		await using NamedPipeServerStream pipe = new(
			pipeName,
			PipeDirection.InOut,
			1,
			PipeTransmissionMode.Byte,
			PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

		await pipe.WaitForConnectionAsync().ConfigureAwait(false);
		await using NativeInferenceServer server = new(pipe, backend);
		return await server.RunAsync(CancellationToken.None).ConfigureAwait(false);
	}

	private static string? ValueAfter(string[] args, string name)
	{
		int index = Array.IndexOf(args, name);
		return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
	}
}
