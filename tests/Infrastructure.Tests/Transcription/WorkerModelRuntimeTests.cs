using Application.Ports;
using AwesomeAssertions;
using Domain.Audio;
using Domain.Models;
using Infrastructure.Transcription;
using NSubstitute;
using Xunit;

namespace Infrastructure.Tests.Transcription;

public sealed class WorkerModelRuntimeTests
{
	private readonly IInferenceWorkerSupervisor _supervisor = Substitute.For<IInferenceWorkerSupervisor>();

	public WorkerModelRuntimeTests() =>
		ConfigureSupervisor();

	private void ConfigureSupervisor()
	{
		_supervisor.HasLiveWorker.Returns(true);
		_supervisor.TranscribeAsync(Arg.Any<InferenceWorkerRequest>(), Arg.Any<CancellationToken>())
			.Returns(new TranscriptionResult("worker result"));
	}

	[Fact]
	public async Task Load_warmup_transcribe_and_release_stay_behind_the_worker_boundary()
	{
		ModelLoadRequest request = new(
			"base",
			"C:/cache/ggml-base.bin",
			ComputeBackend.Vulkan,
			ComputePrecision.Float16,
			"en");
		WorkerModelRuntime runtime = new(_supervisor);

		IModelHandle handle = await runtime.LoadAsync(request, CancellationToken.None);
		await handle.WarmUpAsync(CancellationToken.None);
		TranscriptionResult result = await handle.TranscribeAsync(new AudioClip([0.1f], 16_000), CancellationToken.None);
		await handle.DisposeAsync();

		result.Text.Should().Be("worker result");
		await _supervisor.Received(1).TranscribeAsync(
			Arg.Is<InferenceWorkerRequest>(item => item.Operation == InferenceOperation.Load),
			Arg.Any<CancellationToken>());
		await _supervisor.Received(1).TranscribeAsync(
			Arg.Is<InferenceWorkerRequest>(item => item.Operation == InferenceOperation.WarmUp),
			Arg.Any<CancellationToken>());
		await _supervisor.Received(1).TranscribeAsync(
			Arg.Is<InferenceWorkerRequest>(item => item.Operation == InferenceOperation.Transcribe),
			Arg.Any<CancellationToken>());
		await _supervisor.Received(1).TranscribeAsync(
			Arg.Is<InferenceWorkerRequest>(item => item.Operation == InferenceOperation.Unload),
			Arg.Any<CancellationToken>());
	}
}
