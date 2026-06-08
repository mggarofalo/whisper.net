// Drives the @WHISPER-14 end-to-end orchestration scenarios. It owns HOW the pipeline is exercised so
// the steps stay one-liners: it drives the REAL DictationOrchestrator over the REAL WasapiAudioSource
// (fed by the fake capture client) and the REAL DeliverTranscriptionCommand pipeline through Mediator,
// substituting only the Infrastructure ports (transcriber, injectors). Assertions are made at those
// port boundaries plus the orchestrator's own pipeline stage and structured log.

using Application.Ports;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Domain.Audio;
using Logic.AppManagement;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class DictationOrchestratorDriver(
	DictationOrchestrator orchestrator,
	ITranscriber transcriber,
	FakeAudioCaptureClient captureClient,
	FakeTextInjectorFactory injectors,
	RecordingLogger<DictationOrchestrator> logger)
{
	// One buffer of (silent) interleaved stereo samples at the fake device's 48 kHz/2ch default — its
	// content is irrelevant while the transcriber is faked; it only has to flow through capture.
	private static float[] SpokenFrame() => new float[960];

	public void ModelWillTranscribeTo(string text) =>
		transcriber
			.TranscribeAsync(Arg.Any<AudioClip>(), Arg.Any<CancellationToken>())
			.Returns(new TranscriptionResult(text));

	// Enter Recording with some audio captured, leaving the pipeline mid-flight for the failure scenario.
	public void StartRecording()
	{
		orchestrator.Start();
		captureClient.ProduceFrame(SpokenFrame());
	}

	// The full hands-free path: start, speak, stop — capture -> transcribe -> deliver -> idle.
	public async Task RunFullDictation()
	{
		orchestrator.Start();
		captureClient.ProduceFrame(SpokenFrame());
		await orchestrator.StopAsync();
	}

	// Stop while the faked transcriber throws, so the stop drives the pipeline into its error path.
	public async Task TranscriptionFails()
	{
		transcriber
			.TranscribeAsync(Arg.Any<AudioClip>(), Arg.Any<CancellationToken>())
			.Returns<TranscriptionResult>(_ => throw new InvalidOperationException("transcription failed"));

		await orchestrator.StopAsync();
	}

	// --- assertions ---

	public void AssertTranscribed() => transcriber.ReceivedCalls().Should().NotBeEmpty();

	// The default delivery strategy is Type, so a delivered phrase goes through the typing injector.
	public void AssertDelivered(string expected) => injectors.Typing.Received(1).Inject(expected);

	public void AssertNothingDelivered() => injectors.Typing.DidNotReceive().Inject(Arg.Any<string>());

	public void AssertFailureLogged() =>
		logger.Entries.Should().Contain(entry => entry.Level == LogLevel.Error);

	public void AssertIdle() => orchestrator.Stage.Should().Be(DictationStage.Idle);
}
