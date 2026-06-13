// The real audio-feedback player: plays a short, distinct tone for each dictation cue.
// The tones are synthesized in-process with NAudio's SignalGenerator, so they ship with the app and
// need no on-disk asset or absolute path to resolve. Playback is fire-and-forget on a background thread
// and any failure (e.g. no output device) is logged and swallowed, so feedback can never block or break
// the dictation flow. Like the capture client, this device glue is validated by manual smoke rather than
// the headless specs, which fake IAudioFeedback.

using Application.Ports;
using Domain.Feedback;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Infrastructure.Audio;

internal sealed class AudioFeedbackPlayer(ILogger<AudioFeedbackPlayer> logger) : IAudioFeedback
{
	// Fire-and-forget: render the cue on a background thread so the caller (the dictation pipeline) is
	// never blocked, and never let an exception escape onto that thread.
	public void Play(FeedbackSound sound) => _ = Task.Run(() => Render(sound));

	private void Render(FeedbackSound sound)
	{
		try
		{
			(double frequency, int milliseconds) = sound switch
			{
				FeedbackSound.RecordingStarted => (880.0, 110),
				FeedbackSound.RecordingStopped => (660.0, 110),
				FeedbackSound.TranscriptionComplete => (1175.0, 140),
				_ => (440.0, 100),
			};

			ISampleProvider tone = new SignalGenerator(44_100, 1)
			{
				Gain = 0.2,
				Frequency = frequency,
				Type = SignalGeneratorType.Sin,
			}.Take(TimeSpan.FromMilliseconds(milliseconds));

			using WasapiOut output = new();
			using ManualResetEventSlim finished = new(initialState: false);
			output.PlaybackStopped += (_, _) => finished.Set();
			output.Init(tone.ToWaveProvider());
			output.Play();
			finished.Wait(TimeSpan.FromSeconds(2));
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Audio feedback playback for {Sound} failed; ignored.", sound);
		}
	}
}
