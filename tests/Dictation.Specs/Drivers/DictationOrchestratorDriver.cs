// Drives the end-to-end orchestration scenarios. It owns HOW the pipeline is exercised so
// the steps stay one-liners: it drives the REAL DictationOrchestrator over the REAL WasapiAudioSource
// (fed by the fake capture client) and the REAL DeliverTranscriptionCommand pipeline through Mediator,
// substituting only the Infrastructure ports (transcriber, injectors). Assertions are made at those
// port boundaries plus the orchestrator's own pipeline stage and structured log.

using System;
using Application.Dictation;
using Application.Ports;
using AwesomeAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Dictation.Specs.Support;
using Domain.Audio;
using Logic.AppManagement;
using Logic.AudioManagement;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class DictationOrchestratorDriver(
	DictationOrchestrator orchestrator,
	ITranscriber transcriber,
	FakeAudioCaptureClient captureClient,
	FakeTextInjectorFactory injectors,
	RecordingLogger<DictationOrchestrator> logger,
	ManualTimeProvider time,
	AudioBufferingOptions bufferingOptions,
	RecordingUserNotifier notifier,
	IMessenger messenger)
{
	// The no-audio signals the orchestrator publishes for the overlay (WHISPER-144), counted from the first
	// driven dictation on.
	private int _noAudioSignals;
	private bool _subscribed;

	private void SubscribeToNoAudio()
	{
		if (_subscribed)
		{
			return;
		}

		_subscribed = true;
		messenger.Register<DictationNoAudioMessage>(this, (_, _) => _noAudioSignals++);
	}

	// One buffer of interleaved stereo samples at the fake device's 48 kHz/2ch default. Filled with
	// speech-level energy (not silence) so the captured clip survives the no-speech gate —
	// the trimmer collapses all-silence to empty and the pipeline would skip transcription. The exact
	// content is otherwise irrelevant while the transcriber is faked; it only has to flow through capture.
	private static float[] SpokenFrame()
	{
		float[] frame = new float[960];
		Array.Fill(frame, 0.1f);
		return frame;
	}

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

	// The full hands-free path: start, speak, stop — capture -> transcribe -> deliver -> idle. The stop
	// drains the post-release grace window on the scenario's manual clock.
	public async Task RunFullDictation()
	{
		SubscribeToNoAudio();
		orchestrator.Start();
		captureClient.ProduceFrame(SpokenFrame());
		await orchestrator.StopAndElapseGraceAsync(time, bufferingOptions);
	}

	// A recording with nothing but digital silence — what a tap-muted microphone delivers. The trimmer
	// collapses it to empty, so the pipeline skips transcription.
	public async Task RunSilentDictation()
	{
		SubscribeToNoAudio();
		orchestrator.Start();
		captureClient.ProduceFrame(new float[960]);
		await orchestrator.StopAndElapseGraceAsync(time, bufferingOptions);
	}

	// Stop while the faked transcriber throws, so the stop drives the pipeline into its error path.
	public async Task TranscriptionFails()
	{
		transcriber
			.TranscribeAsync(Arg.Any<AudioClip>(), Arg.Any<CancellationToken>())
			.Returns<TranscriptionResult>(_ => throw new InvalidOperationException("transcription failed"));

		await orchestrator.StopAndElapseGraceAsync(time, bufferingOptions);
	}

	// --- assertions ---

	public void AssertTranscribed() => transcriber.ReceivedCalls().Should().NotBeEmpty();

	// The default delivery strategy is Type, so a delivered phrase goes through the typing injector.
	public void AssertDelivered(string expected) => injectors.Typing.Received(1).Inject(expected);

	public void AssertNothingDelivered() => injectors.Typing.DidNotReceive().Inject(Arg.Any<string>());

	public void AssertFailureLogged() =>
		logger.Entries.Should().Contain(entry => entry.Level == LogLevel.Error);

	// Told through the overlay (WHISPER-144), not a tray balloon Windows may suppress.
	public void AssertNoAudioNotified()
	{
		_noAudioSignals.Should().Be(1, "the overlay is told the recording held no audio");
		notifier.Notifications.Should().BeEmpty("the no-audio message no longer goes through the tray balloon");
	}

	public void AssertNoAudioNotNotified() => _noAudioSignals.Should().Be(0);

	public void AssertNoAudioLogged() =>
		logger.Entries.Should().Contain(entry =>
			entry.Level == LogLevel.Warning && entry.Message.Contains("no audio", StringComparison.OrdinalIgnoreCase));

	public void AssertIdle() => orchestrator.Stage.Should().Be(DictationStage.Idle);
}
