using System.Reflection;
using AwesomeAssertions;
using Domain.Models;
using Infrastructure.Transcription;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Infrastructure.Tests.Transcription;

public sealed class NamedPipeInferenceWorkerProcessTests
{
	[Fact]
	public async Task Starts_the_real_child_process_exchanges_a_request_and_stops_it()
	{
		string workerPath = FindWorkerExecutable();
		InferenceWorkerOptions options = new()
		{
			ExecutablePath = workerPath,
			ConnectTimeout = TimeSpan.FromSeconds(10),
			ShutdownTimeout = TimeSpan.FromSeconds(3),
		};
		NamedPipeInferenceWorkerProcessFactory factory = new(
			Options.Create(options),
			NullLogger<NamedPipeInferenceWorkerProcessFactory>.Instance);
		await using IInferenceWorkerProcess worker = await factory.StartAsync(
			ComputeBackend.Cpu,
			1,
			CancellationToken.None);

		InferenceWorkerRequest request = new(
			Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.bin"),
			"en",
			DecodingOptions.Default,
			16_000,
			[0.1f]);
		Func<Task> act = async () => await worker.TranscribeAsync(request, CancellationToken.None);

		await act.Should().ThrowAsync<ModelNotFoundException>();
		worker.IsAlive.Should().BeTrue("a typed model error must not corrupt the worker channel");

		await worker.StopAsync(CancellationToken.None);
		worker.IsAlive.Should().BeFalse();
	}

	private static string FindWorkerExecutable()
	{
		string configuration = typeof(NamedPipeInferenceWorkerProcessTests).Assembly
			.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "Debug";
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Whisper.slnx")))
		{
			directory = directory.Parent;
		}

		return Path.Combine(
			directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root."),
			"src",
			"Inference.Worker",
			"bin",
			configuration,
			"net10.0",
			"Inference.Worker.exe");
	}
}
