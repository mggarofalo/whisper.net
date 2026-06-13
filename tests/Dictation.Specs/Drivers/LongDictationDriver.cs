// Drives the long-dictation scenarios. It owns HOW the soft limit is exercised so the
// steps stay one-liners: it shrinks the scenario's soft (and hard) limit through the scoped options
// holder, then drives the REAL DictationOrchestrator over the real WasapiAudioSource (fed by the fake
// capture client) and the real delivery pipeline, faking only the Infrastructure ports. The
// orchestrator is resolved lazily — AFTER the Given configured the limit — because it captures the
// buffering options at construction. Assertions are made at the ITranscriber port (the clip retains
// audio from before AND after the limit; the hard-limit auto-stop still delivers the clip) and on the
// scenario-scoped messenger (the near-limit / at-limit / hard-limit-stop signals), using distinct
// amplitudes so the clip's provenance is checkable sample by sample.

using Application.Dictation;
using Application.Ports;
using AwesomeAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Dictation.Specs.Support;
using Domain.Audio;
using Logic.AppManagement;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class LongDictationDriver
{
	// Distinct amplitudes for the audio spoken before and after the soft limit, so the assertion can
	// tell exactly which side of the limit each retained sample came from. Both are clearly speech.
	private const float PreLimitAmplitude = 0.5f;
	private const float PostLimitAmplitude = 0.7f;

	// One 960-sample interleaved stereo buffer at the fake device's 48 kHz/2ch default normalizes to
	// exactly 160 mono samples at 16 kHz — 10 ms of clip audio per produced frame.
	private const int FrameMs = 10;

	private readonly IServiceProvider _scope;
	private readonly ITranscriber _transcriber;
	private readonly FakeAudioCaptureClient _captureClient;
	private readonly ManualTimeProvider _time;
	private readonly ScenarioAudioBufferingOptions _bufferingOptions;

	private readonly List<DictationNearLimitMessage> _nearLimitMessages = [];
	private readonly List<DictationAtLimitMessage> _atLimitMessages = [];
	private readonly List<DictationHardLimitStopMessage> _hardLimitMessages = [];

	private DictationOrchestrator? _orchestrator;
	private AudioClip? _transcribedClip;
	private int _producedMs;

	public LongDictationDriver(
		IServiceProvider scope,
		ITranscriber transcriber,
		FakeAudioCaptureClient captureClient,
		IMessenger messenger,
		ManualTimeProvider time,
		ScenarioAudioBufferingOptions bufferingOptions)
	{
		_scope = scope;
		_transcriber = transcriber;
		_captureClient = captureClient;
		_time = time;
		_bufferingOptions = bufferingOptions;

		// Capture the soft-limit signals the orchestrator publishes; registered before the orchestrator
		// exists, so nothing can be missed.
		messenger.Register<LongDictationDriver, DictationNearLimitMessage>(
			this, static (driver, message) => driver._nearLimitMessages.Add(message));
		messenger.Register<LongDictationDriver, DictationAtLimitMessage>(
			this, static (driver, message) => driver._atLimitMessages.Add(message));
		messenger.Register<LongDictationDriver, DictationHardLimitStopMessage>(
			this, static (driver, message) => driver._hardLimitMessages.Add(message));
	}

	private int LimitMs => _bufferingOptions.Options.MaxDurationMs;

	private int HardLimitMs => _bufferingOptions.Options.HardMaxDurationMs;

	public void ConfigureSoftLimit(int milliseconds) =>
		_bufferingOptions.Options = _bufferingOptions.Options with { MaxDurationMs = milliseconds };

	public void ConfigureHardLimit(int milliseconds) =>
		_bufferingOptions.Options = _bufferingOptions.Options with { HardMaxDurationMs = milliseconds };

	// Speak up to exactly the soft limit, then keep speaking past it at the post-limit amplitude. The
	// old hard cap filled the buffer at exactly the limit and silently dropped everything after it, so
	// under the old behavior the clip would contain no post-limit sample at all.
	public void DictatePastTheSoftLimit()
	{
		StartDictating();
		ProduceAudio(LimitMs, PreLimitAmplitude);
		ProduceAudio(200, PostLimitAmplitude);
	}

	// Speak to just past the 80% near-limit threshold (with margin), staying well below the limit.
	public void DictateToTheNearLimitThreshold()
	{
		StartDictating();
		ProduceAudio((LimitMs * 8 / 10) + 100, PreLimitAmplitude);
	}

	// Continue the same recording: fill the remainder up to the limit, then keep speaking past it.
	public void KeepDictatingPastTheSoftLimit()
	{
		ProduceAudio(LimitMs - _producedMs, PreLimitAmplitude);
		ProduceAudio(200, PostLimitAmplitude);
	}

	// Speak straight through the hard ceiling without ever releasing: the orchestrator must stop the
	// dictation ITSELF at the hard limit (the normal stop path — stop and transcribe, never discard).
	// The device keeps producing for a moment afterwards, as a real microphone would.
	public void DictatePastTheHardLimit()
	{
		StartDictating();
		ProduceAudio(HardLimitMs, PreLimitAmplitude);
		ProduceAudio(200, PostLimitAmplitude);
	}

	// The release: stop, drain the post-release grace window on the manual clock, await delivery.
	public async Task StopDictating() =>
		await _orchestrator!.StopAndElapseGraceAsync(_time, _bufferingOptions.Options);

	// --- assertions ---

	public void AssertClipContainsPreLimitAudio()
	{
		_transcribedClip.Should().NotBeNull("the dictation must reach the transcriber");
		_transcribedClip!.Samples.Should().Contain(
			PreLimitAmplitude, "the audio spoken before the limit belongs to the utterance");
	}

	public void AssertClipContainsPostLimitAudio()
	{
		_transcribedClip.Should().NotBeNull("the dictation must reach the transcriber");
		_transcribedClip!.Samples.Should().Contain(
			PostLimitAmplitude,
			"the limit is soft: audio spoken after it must be retained, never silently dropped");
	}

	public void AssertNearLimitWarningPublished()
	{
		_nearLimitMessages.Should().ContainSingle(
			"approaching the limit must warn the user exactly once per recording");
		_nearLimitMessages[0].LimitMs.Should().Be(LimitMs);
		_atLimitMessages.Should().BeEmpty("the recording has not reached the limit yet");
	}

	public void AssertAtLimitSignalPublished()
	{
		_atLimitMessages.Should().ContainSingle(
			"reaching the limit must signal the user exactly once per recording");
		_atLimitMessages[0].LimitMs.Should().Be(LimitMs);
	}

	public async Task AssertAudioPastTheLimitIsRetained()
	{
		await StopDictating();
		AssertClipContainsPostLimitAudio();
	}

	// The hard-limit auto-stop began on the capture thread (fire-and-forget, like a hotkey release) and
	// is waiting out the post-release grace window on the manual clock; elapse it, then await the
	// pipeline's return to Idle — delivery completes just before that transition — via a stage hook,
	// since no Task handle exists for a stop the system initiated itself.
	public async Task AssertStoppedAndTranscribedAtTheHardLimit()
	{
		TaskCompletionSource idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
		_orchestrator!.StageChanged += (_, e) =>
		{
			if (e.Current == DictationStage.Idle)
			{
				idle.TrySetResult();
			}
		};
		if (_orchestrator.Stage == DictationStage.Idle)
		{
			idle.TrySetResult();
		}

		_time.Advance(TimeSpan.FromMilliseconds(_bufferingOptions.Options.PostReleaseGraceMs));
		await idle.Task.WaitAsync(TimeSpan.FromSeconds(10));

		_transcribedClip.Should().NotBeNull(
			"the hard-limit failsafe transcribes the recording instead of discarding it");
		_transcribedClip!.Samples.Should().Contain(
			PreLimitAmplitude, "everything recorded up to the hard limit reaches the transcriber");
	}

	public void AssertHardLimitStopSignalPublished()
	{
		_hardLimitMessages.Should().ContainSingle("the hard-limit stop is signalled exactly once");
		_hardLimitMessages[0].LimitMs.Should().Be(HardLimitMs);
	}

	// Resolve the REAL orchestrator from the scenario scope — only now, so it builds its capture
	// buffer from the soft limit the Given configured — and begin recording.
	private void StartDictating()
	{
		_transcriber
			.TranscribeAsync(Arg.Do<AudioClip>(clip => _transcribedClip = clip), Arg.Any<CancellationToken>())
			.Returns(new TranscriptionResult("the long passage"));

		_orchestrator = _scope.GetRequiredService<DictationOrchestrator>();
		_orchestrator.Start();
	}

	private void ProduceAudio(int milliseconds, float amplitude)
	{
		for (int producedMs = 0; producedMs < milliseconds; producedMs += FrameMs)
		{
			_captureClient.ProduceFrame(DeviceFrame(amplitude));
			_producedMs += FrameMs;
		}
	}

	private static float[] DeviceFrame(float amplitude)
	{
		float[] samples = new float[960];
		Array.Fill(samples, amplitude);
		return samples;
	}
}
