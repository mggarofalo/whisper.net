// Drives the @WHISPER-129 model warm-up status scenarios. It proves the feature's core promise — that ONE
// app-wide event lights up every surface and a second one clears them all — by wiring the REAL
// LevelOverlayController and the REAL HomeViewModel to the SAME scenario-scoped messenger the warm-up
// service publishes on, then publishing the warm-up started/cleared signals exactly as the service does.
// (The service's own publishing is pinned by ModelWarmupHostedServiceTests; here we prove the consumers.)
// The thin WPF overlay/Home views that bind to these are Presentation glue verified by smoke.

using Application.Models;
using Application.Ports;
using AwesomeAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Dictation.Specs.Support;
using Logic.AppManagement;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class ModelWarmupStatusDriver : IDisposable
{
	private readonly IMessenger _messenger;
	private readonly LevelOverlayController _overlay;
	private readonly HomeViewModel _home;

	public ModelWarmupStatusDriver(IMessenger messenger, ManualTimeProvider time, IMediator mediator, IUiCollectionSynchronizer synchronizer)
	{
		_messenger = messenger;

		// Both surfaces listen on the SAME messenger the warm-up service publishes on. The overlay subscribes
		// in its constructor (always listening); the dashboard subscribes only while active (WHISPER-94), so
		// the Given opens it. A faked audio source / fresh state machine keep the overlay self-contained — this
		// scenario is about warm-up, not recording.
		_overlay = new LevelOverlayController(new RecordingStateMachine(), Substitute.For<IAudioSource>(), messenger, time);
		_home = new HomeViewModel(mediator, messenger, synchronizer);
	}

	// Enter the dashboard through the real activation lifecycle so it subscribes to the warm-up signal and
	// runs its activation refresh (served by the scenario's faked store/history), then await that refresh.
	public async Task OpenDashboard()
	{
		_home.OnNavigatedTo();
		await _home.RefreshCommand.ExecutionTask!;
	}

	// Publish the warm-up signals on the shared messenger, exactly as ModelWarmupHostedService does.
	public void BeginWarmup() => _messenger.Send(new ModelWarmupChangedMessage(true));

	public void CompleteWarmup() => _messenger.Send(new ModelWarmupChangedMessage(false));

	public void AssertOverlayShowsWarming()
	{
		_overlay.IsVisible.Should().BeTrue("the overlay pill appears while the model is warming up");
		_overlay.State.Should().Be(OverlayState.Warming);
	}

	public void AssertDashboardShowsWarming() =>
		_home.IsModelWarming.Should().BeTrue("the Home dashboard shows a warming status line while the model warms");

	public void AssertOverlayHidden() =>
		_overlay.IsVisible.Should().BeFalse("the cleared event hides the overlay app-wide");

	public void AssertDashboardNotWarming() =>
		_home.IsModelWarming.Should().BeFalse("the cleared event lifts the dashboard's warming status app-wide");

	public void Dispose() => _overlay.Dispose();
}
