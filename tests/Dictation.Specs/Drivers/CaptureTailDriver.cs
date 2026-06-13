// Drives the capture-tail scenarios. It owns HOW the post-release timing is exercised so
// the steps stay one-liners. The dictation half drives the REAL DictationOrchestrator over the real
// WasapiAudioSource with the fake device in deferred-stop mode — modeling NAudio's asynchronous stop,
// where the in-flight tail frames arrive AFTER the stop request — and elapses the post-release grace
// window on the scenario's manual clock, asserting at the transcriber port that the tail made it into
// the clip. The trimmer half drives the REAL ISilenceTrimmer from the same composition and asserts the
// sustained-silence contract: a short quiet tail is speech and survives; long dead air trims to a pad.

using Application.Ports;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Domain.Audio;
using Logic.AppManagement;
using Logic.AudioManagement;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class CaptureTailDriver(
	DictationOrchestrator orchestrator,
	ITranscriber transcriber,
	FakeAudioCaptureClient captureClient,
	ManualTimeProvider time,
	AudioBufferingOptions bufferingOptions,
	ISilenceTrimmer trimmer,
	SilenceTrimmerOptions trimmerOptions)
{
	// Distinct amplitudes for the spoken audio and the post-release tail, so the assertion can tell
	// exactly which frames reached the transcriber. Both are well above the silence threshold.
	private const float SpokenAmplitude = 0.5f;
	private const float TailAmplitude = 0.7f;

	private const int ClipSampleRate = 16_000;
	private const int SpeechMs = 200;

	private Task? _stopTask;
	private AudioClip? _transcribedClip;
	private AudioClip? _trimInput;
	private AudioClip? _trimResult;

	// --- the post-release capture half ---

	public void StartDictatingShortPhrase()
	{
		transcriber
			.TranscribeAsync(Arg.Do<AudioClip>(clip => _transcribedClip = clip), Arg.Any<CancellationToken>())
			.Returns(new TranscriptionResult("see you friday"));

		// The real NAudio stop is asynchronous: the device keeps delivering in-flight frames after the
		// stop request, until the scenario completes the stop.
		captureClient.DeferStopCompletion = true;

		orchestrator.Start();
		captureClient.ProduceFrame(DeviceFrame(SpokenAmplitude));
	}

	// The chord release: the stop begins, but completes only once the grace window elapses.
	public void ReleaseChord() => _stopTask = orchestrator.StopAsync();

	public void DeviceDeliversRemainingAudio()
	{
		captureClient.ProduceFrame(DeviceFrame(TailAmplitude));
		captureClient.CompleteStop();
	}

	public async Task GraceWindowElapses()
	{
		time.Advance(TimeSpan.FromMilliseconds(bufferingOptions.PostReleaseGraceMs));
		await _stopTask!;
	}

	public void AssertTranscribedClipContainsPostReleaseAudio()
	{
		_transcribedClip.Should().NotBeNull("the dictation must reach the transcriber");
		_transcribedClip!.Samples.Should().Contain(
			TailAmplitude,
			"the frames the device delivered after the stop request belong to the utterance");
	}

	// --- the trimmer half ---

	public void ClipEndsInQuietTrailingSpeech(int milliseconds) =>
		_trimInput = SpeechFollowedBy(0.005f, milliseconds);

	public void ClipEndsInDeadAir(int milliseconds) =>
		_trimInput = SpeechFollowedBy(0f, milliseconds);

	public void TrimTrailingSilence() => _trimResult = trimmer.Trim(_trimInput!);

	public void AssertQuietTrailingSpeechPreserved() =>
		_trimResult!.Samples.Should().Equal(
			_trimInput!.Samples,
			"a quiet tail shorter than the silence window is the soft end of speech, not dead air");

	public void AssertDeadAirTrimmedToPad() =>
		_trimResult!.Samples.Should().HaveCount(
			SamplesFor(SpeechMs) + SamplesFor(trimmerOptions.TrailingPadMs),
			"sustained dead air is trimmed down to a short pad beyond the last speech");

	// One buffer of interleaved stereo samples at the fake device's 48 kHz/2ch default, all at one
	// amplitude — after downmix + resample it reaches the clip at the same amplitude.
	private static float[] DeviceFrame(float amplitude)
	{
		float[] samples = new float[960];
		Array.Fill(samples, amplitude);
		return samples;
	}

	// A clip of clear speech followed by a tail at the given amplitude, in the normalized clip format.
	private static AudioClip SpeechFollowedBy(float tailAmplitude, int tailMs)
	{
		int speechSamples = SamplesFor(SpeechMs);
		int tailSamples = SamplesFor(tailMs);
		float[] samples = new float[speechSamples + tailSamples];
		Array.Fill(samples, SpokenAmplitude, 0, speechSamples);
		Array.Fill(samples, tailAmplitude, speechSamples, tailSamples);
		return new AudioClip(samples, ClipSampleRate);
	}

	private static int SamplesFor(int milliseconds) => milliseconds * ClipSampleRate / 1000;
}
