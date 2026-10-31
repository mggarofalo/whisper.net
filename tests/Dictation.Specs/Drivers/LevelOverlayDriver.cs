// Drives the level-overlay scenarios. It owns HOW the overlay is exercised so the steps stay
// one-liners: it builds the REAL LevelOverlayController over a real RecordingStateMachine and a faked
// audio source, drives recording start/stop on the state machine (as the orchestrator would), raises an
// audio frame to simulate speech, and asserts the controller's visibility and level. The controller is
// the unit-testable, WPF-free view-model logic the thin overlay binds to.

using Application.Dictation;
using Application.Models;
using Application.Ports;
using AwesomeAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Dictation.Specs.Support;
using Domain.Audio;
using Logic.AppManagement;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class LevelOverlayDriver : IDisposable
{
	private readonly RecordingStateMachine _stateMachine = new();
	private readonly IAudioSource _audioSource = Substitute.For<IAudioSource>();
	private readonly IMessenger _messenger;
	private readonly ManualTimeProvider _time;
	private readonly LevelOverlayController _controller;

	// The scenario-scoped messenger and manual clock are the same seams the orchestrator publishes the
	// soft-limit and failure signals on and times the recording with, so driving them here
	// exercises the real overlay feedback path.
	// How many times the controller asked the window to present itself, and the count just before the most
	// recent driven action — so a step can assert that THAT action (not an earlier one) raised a request.
	private int _presentRequests;
	private int _presentRequestsBeforeLastAction;

	private OverlayScreenState _screenState;

	public LevelOverlayDriver(IMessenger messenger, ManualTimeProvider time)
	{
		_messenger = messenger;
		_time = time;
		_controller = new LevelOverlayController(_stateMachine, _audioSource, messenger, time);
		_controller.PresentRequested += (_, _) => _presentRequests++;
	}

	// Recording starts and a frame of speech-level audio flows, as it would during a real capture.
	public void StartRecording()
	{
		_stateMachine.RequestStart();
		EmitInputFrame();
	}

	public void StopRecording() => _stateMachine.RequestStop();

	// Start recording WITHOUT emitting a frame, so the perceptual-scale scenarios drive the meter purely with
	// the sustained audio they specify.
	public void BeginRecording()
	{
		_presentRequestsBeforeLastAction = _presentRequests;
		_stateMachine.RequestStart();
	}

	// The warm-up service's signal, exactly as it publishes it: the pill shows "Warming up…".
	public void ShowWarmingPill()
	{
		_messenger.Send(new ModelWarmupChangedMessage(true));
		_controller.State.Should().Be(OverlayState.Warming);
		_controller.IsVisible.Should().BeTrue("the warming pill is already up before the dictation starts");
	}

	public void FinishRecording()
	{
		_stateMachine.RequestStop();
		_stateMachine.CompleteTranscription();
		_controller.IsVisible.Should().BeFalse("the overlay returns to rest between dictations");
	}

	// Feed enough constant-amplitude frames that the exponential smoothing converges to the per-frame
	// perceptual level, so the resulting band can be asserted regardless of the smoothing factor.
	public void ReceiveSustainedAudio(float amplitude)
	{
		for (int i = 0; i < 60; i++)
		{
			EmitFrameAt(amplitude);
		}
	}

	private void EmitInputFrame() => EmitFrameAt(0.6f);

	private void EmitFrameAt(float amplitude)
	{
		float[] samples = new float[480];
		Array.Fill(samples, amplitude);
		_audioSource.FrameAvailable += Raise.EventWith(
			new AudioFrameAvailableEventArgs(samples, new CaptureFormat(16_000, 1, 32, AudioSampleFormat.IeeeFloat)));
	}

	// --- assertions ---

	public void AssertOverlayVisible() => _controller.IsVisible.Should().BeTrue();

	public void AssertOverlayHidden() => _controller.IsVisible.Should().BeFalse();

	public void AssertReflectsInputLevel() => _controller.Level.Should().BeGreaterThan(0);

	// Perceptual-scale bands.
	public void AssertMeterMidRange() =>
		_controller.Level.Should().BeInRange(0.40, 0.70, "normal-volume speech should fill the meter to mid-range");

	public void AssertMeterNearZero() =>
		_controller.Level.Should().BeLessThanOrEqualTo(0.02, "silence sits at or near zero");

	public void AssertMeterApproachesFullWithoutPegging()
	{
		_controller.Level.Should().BeGreaterThan(0.70, "loud speech approaches full scale");
		_controller.Level.Should().BeLessThan(1.0, "without constantly pegging");
	}

	// --- feedback: state, elapsed, near-cap, error ---

	public void CompleteTranscription() => _stateMachine.CompleteTranscription();

	public void AdvanceSeconds(int seconds) => _time.Advance(TimeSpan.FromSeconds(seconds));

	// Publish the soft-limit signal on the shared messenger, exactly as the orchestrator does.
	public void PublishNearLimit() => _messenger.Send(new DictationNearLimitMessage(8_000, 10_000));

	// Publish the failure signal on the shared messenger, as the orchestrator does on a failure.
	public void PublishFailure()
	{
		_presentRequestsBeforeLastAction = _presentRequests;
		_messenger.Send(new DictationFailedMessage());
	}

	public void AssertPresentRequestedByLastAction() =>
		_presentRequests.Should().BeGreaterThan(_presentRequestsBeforeLastAction,
			"taking over the pill must explicitly re-present the overlay, even when it was already visible");

	public void AssertPresentRequestCount(int expected) => _presentRequests.Should().Be(expected);

	// --- the window's on-screen read-back, judged by the pure verdict the overlay logs ---

	public void ReadBackScreenState(string condition)
	{
		OverlayScreenState onScreen = new(Visible: true, Cloaked: false, OnCurrentDesktop: true, Topmost: true, CoveredBy: null);
		_screenState = condition switch
		{
			"fully on screen" => onScreen,
			"not visible" => onScreen with { Visible = false },
			"cloaked" => onScreen with { Cloaked = true },
			"on another virtual desktop" => onScreen with { OnCurrentDesktop = false },
			"not top-most" => onScreen with { Topmost = false },
			_ when condition.StartsWith("covered by ", StringComparison.Ordinal) =>
				onScreen with { CoveredBy = condition["covered by ".Length..].Trim('"') },
			_ => throw new ArgumentOutOfRangeException(nameof(condition), condition, "Unknown overlay read-back."),
		};
	}

	public void AssertJudged(bool onScreen) => _screenState.IsOnScreen.Should().Be(onScreen);

	public void AssertScreenCheckReason(string reason) => _screenState.Reason.Should().Be(reason);

	public void AssertState(OverlayState state) => _controller.State.Should().Be(state);

	public void AssertNearCap() => _controller.NearCap.Should().BeTrue("the overlay warns before any audio is dropped");

	public void AssertElapsedAtLeast(int seconds) =>
		_controller.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(seconds));

	public void AssertVisible() => _controller.IsVisible.Should().BeTrue();

	public void Dispose() => _controller.Dispose();
}
