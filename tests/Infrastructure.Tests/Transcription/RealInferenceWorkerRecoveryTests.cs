using System.Diagnostics;
using System.Reflection;
using Application.Ports;
using AwesomeAssertions;
using Domain.Models;
using Infrastructure.Transcription;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Infrastructure.Tests.Transcription;

public sealed class RealInferenceWorkerRecoveryTests
{
	[Fact]
	[Trait("Category", "slow")]
	public async Task A_real_worker_can_be_killed_and_rebuilt_on_cpu_with_the_same_model()
	{
		string? modelPath = Environment.GetEnvironmentVariable("WHISPER_REAL_MODEL");
		if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
		{
			return; // Opt-in real-device smoke; clean clones do not carry a 500 MB+ model.
		}

		IBackendSelector selector = Substitute.For<IBackendSelector>();
		selector.SelectBackendAsync(Arg.Any<CancellationToken>())
			.Returns(
				new BackendSelection(ComputeBackend.Vulkan, "GPU"),
				new BackendSelection(ComputeBackend.Cpu, "GPU unavailable"));
		NamedPipeInferenceWorkerProcessFactory factory = new(
			Options.Create(new InferenceWorkerOptions { ExecutablePath = FindWorkerExecutable() }),
			NullLogger<NamedPipeInferenceWorkerProcessFactory>.Instance);
		CapturingWorkerFactory capturingFactory = new(factory);
		await using InferenceWorkerSupervisor supervisor = new(
			capturingFactory,
			selector,
			NullLogger<InferenceWorkerSupervisor>.Instance);
		InferenceWorkerRequest warmup = new(
			modelPath,
			"en",
			DecodingOptions.Default,
			16_000,
			new float[1_600],
			InferenceOperation.WarmUp);

		await supervisor.TranscribeAsync(warmup, TestContext.Current.CancellationToken);
		IInferenceWorkerProcess first = capturingFactory.Workers.Single();
		int firstProcessId = first.ProcessId;
		using (Process process = Process.GetProcessById(firstProcessId))
		{
			process.Kill(entireProcessTree: true);
			await process.WaitForExitAsync(TestContext.Current.CancellationToken);
		}

		await supervisor.TranscribeAsync(warmup, TestContext.Current.CancellationToken);

		capturingFactory.Workers.Should().HaveCount(2);
		capturingFactory.Workers[1].Generation.Should().Be(2);
		capturingFactory.Workers[1].Backend.Should().Be(ComputeBackend.Cpu);
		capturingFactory.Workers[1].ProcessId.Should().NotBe(firstProcessId);
	}

	private static string FindWorkerExecutable()
	{
		string configuration = typeof(RealInferenceWorkerRecoveryTests).Assembly
			.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "Debug";
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Whisper.slnx")))
		{
			directory = directory.Parent;
		}

		return Path.Combine(
			directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root."),
			"src", "Inference.Worker", "bin", configuration, "net10.0", "Inference.Worker.exe");
	}

	private sealed class CapturingWorkerFactory(IInferenceWorkerProcessFactory inner) : IInferenceWorkerProcessFactory
	{
		public List<IInferenceWorkerProcess> Workers { get; } = [];

		public async ValueTask<IInferenceWorkerProcess> StartAsync(
			ComputeBackend backend,
			int generation,
			CancellationToken cancellationToken)
		{
			IInferenceWorkerProcess worker = await inner.StartAsync(backend, generation, cancellationToken);
			Workers.Add(worker);
			return worker;
		}
	}
}
