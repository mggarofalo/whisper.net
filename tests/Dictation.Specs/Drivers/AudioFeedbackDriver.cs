// Drives the @WHISPER-21 audio-feedback scenarios. It owns HOW the pipeline is taken to each event so
// the steps stay one-liners: it toggles the feedback on/off switch, drives the REAL DictationOrchestrator
// to the requested transition (over the real audio + delivery composition), and asserts at the faked
// IAudioFeedback port which cue — if any — was played.

using Application.Configuration;
using Application.Ports;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Domain.Audio;
using Domain.Feedback;
using Logic.AppManagement;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class AudioFeedbackDriver(
	DictationOrchestrator orchestrator,
	IAudioFeedback feedback,
	AudioFeedbackOptions options,
	ITranscriber transcriber,
	FakeAudioCaptureClient captureClient)
{
	public void EnableFeedback() => options.Enabled = true;

	public void DisableFeedback() => options.Enabled = false;

	public async Task ReachEvent(string @event)
	{
		transcriber
			.TranscribeAsync(Arg.Any<AudioClip>(), Arg.Any<CancellationToken>())
			.Returns(new TranscriptionResult("hello world"));

		// Recording-started is reached by starting; the later cues need a full capture -> deliver cycle.
		orchestrator.Start();
		if (ToSound(@event) != FeedbackSound.RecordingStarted)
		{
			captureClient.ProduceFrame(new float[960]);
			await orchestrator.StopAsync();
		}
	}

	public void AssertSoundPlayed(string @event) => feedback.Received(1).Play(ToSound(@event));

	public void AssertNoSoundPlayed() => feedback.DidNotReceive().Play(Arg.Any<FeedbackSound>());

	private static FeedbackSound ToSound(string @event) => @event switch
	{
		"recording started" => FeedbackSound.RecordingStarted,
		"recording stopped" => FeedbackSound.RecordingStopped,
		"transcription complete" => FeedbackSound.TranscriptionComplete,
		_ => throw new ArgumentOutOfRangeException(nameof(@event), @event, "Unknown feedback event."),
	};
}
