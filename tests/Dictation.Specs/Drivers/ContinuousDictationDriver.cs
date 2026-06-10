// Drives the @WHISPER-28 continuous-dictation scenarios. It owns HOW the mode is exercised so the steps
// stay one-liners: it enters continuous mode and starts an active session on the REAL DictationOrchestrator
// (over the real WasapiAudioSource fed by the fake capture client and the real delivery pipeline), runs a
// full utterance, and asserts the orchestrator auto-restarts recording — or, on Esc, exits to idle without
// restarting. Only the Infrastructure ports (transcriber, injectors) are faked.

using Application.Ports;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Domain.Audio;
using Logic.AppManagement;
using Logic.AudioManagement;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class ContinuousDictationDriver(
	DictationOrchestrator orchestrator,
	ITranscriber transcriber,
	FakeAudioCaptureClient captureClient,
	FakeTextInjectorFactory injectors,
	ManualTimeProvider time,
	AudioBufferingOptions bufferingOptions)
{
	private static float[] SpokenFrame() => new float[960];

	// An active continuous-dictation session: the mode is on and the user is recording the first utterance.
	public void EnterActiveContinuousMode()
	{
		orchestrator.EnableContinuousMode();
		orchestrator.Start();
	}

	public async Task TranscribeAndDeliverOneUtterance()
	{
		transcriber
			.TranscribeAsync(Arg.Any<AudioClip>(), Arg.Any<CancellationToken>())
			.Returns(new TranscriptionResult("take a note"));

		captureClient.ProduceFrame(SpokenFrame());
		await orchestrator.StopAndElapseGraceAsync(time, bufferingOptions);
	}

	public void PressEscToExit() => orchestrator.ExitContinuousMode();

	// --- assertions ---

	public void AssertRecordingRestarted()
	{
		injectors.Typing.Received(1).Inject("take a note");
		orchestrator.Stage.Should().Be(DictationStage.Recording);
		orchestrator.ContinuousMode.Should().BeTrue();
	}

	public void AssertRecordingDidNotRestart() => orchestrator.Stage.Should().NotBe(DictationStage.Recording);

	public void AssertReturnedToIdle()
	{
		orchestrator.Stage.Should().Be(DictationStage.Idle);
		orchestrator.ContinuousMode.Should().BeFalse();
	}
}
