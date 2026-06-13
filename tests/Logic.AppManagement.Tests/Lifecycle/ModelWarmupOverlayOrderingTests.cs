// Regression for WHISPER-131: the warm-up hosted service broadcasts ModelWarmupChangedMessage(true) during
// host start, but the overlay controller — a lazily-resolved singleton — was not constructed until the overlay
// window was created AFTER _host.Start(). Subscribing after the broadcast misses it (the WeakReferenceMessenger
// does not replay), so the warming pill never appeared. These pin the ordering contract App now upholds by
// resolving the controller before the host starts: a controller subscribed BEFORE warm-up starts shows the
// warming pill; one subscribed AFTER the broadcast misses it. The warm-up is gated open so the "warming" window
// (true sent, not yet cleared) stays observable while the assertions run.

using Application.Ports;
using Application.Settings;
using AwesomeAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Domain.Audio;
using Domain.Settings;
using Logic.AppManagement;
using Logic.AppManagement.Lifecycle;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests.Lifecycle;

public sealed class ModelWarmupOverlayOrderingTests
{
	private readonly ISettingsStore _store = Substitute.For<ISettingsStore>();
	private readonly IMessenger _messenger = new WeakReferenceMessenger();
	private readonly GatedTranscriber _transcriber = new();

	public ModelWarmupOverlayOrderingTests() =>
		_store.LoadAsync(Arg.Any<CancellationToken>()).Returns(AppSettings.Default);

	private ModelWarmupHostedService NewWarmupService() =>
		new(_transcriber, _store, _messenger, NullLogger<ModelWarmupHostedService>.Instance);

	// The controller depends only on a state machine, an audio source, the shared messenger, and a clock —
	// no WPF — so it can be exercised here exactly as App composes it.
	private LevelOverlayController NewController() =>
		new(new RecordingStateMachine(), Substitute.For<IAudioSource>(), _messenger, TimeProvider.System);

	[Fact]
	public async Task A_controller_subscribed_before_warm_up_starts_shows_the_warming_pill()
	{
		// Alive (subscribed) before the host starts the warm-up — the ordering App guarantees by resolving the
		// controller before _host.Start().
		using LevelOverlayController controller = NewController();
		ModelWarmupHostedService warmup = NewWarmupService();

		await warmup.StartAsync(TestContext.Current.CancellationToken);
		await _transcriber.PreloadEntered.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

		controller.IsVisible.Should().BeTrue("the warming broadcast reaches a controller that is already subscribed");
		controller.State.Should().Be(OverlayState.Warming);

		_transcriber.Release();
		await warmup.StopAsync(TestContext.Current.CancellationToken);
	}

	[Fact]
	public async Task A_controller_resolved_after_warm_up_has_started_misses_the_warming_pill()
	{
		// Reproduces the defect: the controller is constructed only AFTER the warm-up already announced it
		// started (as it was, lazily, after _host.Start()). The signal is gone, so the pill stays hidden even
		// though the model is still warming — which is exactly why the fix resolves the controller before Start.
		ModelWarmupHostedService warmup = NewWarmupService();
		await warmup.StartAsync(TestContext.Current.CancellationToken);
		await _transcriber.PreloadEntered.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

		using LevelOverlayController controller = NewController(); // subscribes too late

		controller.IsVisible.Should().BeFalse("a controller subscribed after the broadcast misses it — the messenger does not replay");
		controller.State.Should().NotBe(OverlayState.Warming);

		_transcriber.Release();
		await warmup.StopAsync(TestContext.Current.CancellationToken);
	}

	// An ITranscriber whose warm-up (PreloadAsync) parks until released, so a test can hold the warm-up in the
	// window where it has announced it started (broadcast true) but has not yet cleared — the state the overlay
	// pill reflects. TranscribeAsync is unused on this path.
	private sealed class GatedTranscriber : ITranscriber
	{
		private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

		/// <summary>Completes once warm-up has entered PreloadAsync (so the started broadcast has already fired).</summary>
		public Task PreloadEntered => _entered.Task;

		/// <summary>Lets the parked warm-up finish.</summary>
		public void Release() => _release.TrySetResult();

		public async ValueTask PreloadAsync(CancellationToken cancellationToken)
		{
			_entered.TrySetResult();
			await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
		}

		public ValueTask<TranscriptionResult> TranscribeAsync(AudioClip clip, CancellationToken cancellationToken) =>
			throw new NotSupportedException();
	}
}
