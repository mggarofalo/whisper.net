// View-level smoke for the dictation overlay, on a dedicated STA
// thread. It guards the overlay's view glue: the compact content builds and completes its first bind
// against the real LevelOverlayViewModel with no data-binding trace error (a renamed/mistyped path fails),
// and its footprint stays a compact pill (AC4) — the feedback was added within the original bounds, not
// by growing it into a panel. The on-screen styling/colours are the manual remainder.

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Application.Display;
using Application.Models;
using Application.Ports;
using AwesomeAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Domain.Audio;
using Logic.AppManagement;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Presentation.Overlay;
using Xunit;

namespace Presentation.Smoke.Tests;

public sealed class OverlayViewSmokeTests
{
	[Fact]
	public void Overlay_content_builds_and_binds_without_errors_and_stays_compact() => StaThread.Run(() =>
	{
		using BindingErrorCollector bindingErrors = new();
		using LevelOverlayController controller = new(new RecordingStateMachine(), new StubAudioSource(), new WeakReferenceMessenger(), TimeProvider.System);
		using LevelOverlayViewModel viewModel = new(controller, new InlineDispatcher());

		FrameworkElement content = LevelOverlay.BuildContent();
		content.DataContext = viewModel;
		content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
		content.Arrange(new Rect(content.DesiredSize));
		content.UpdateLayout();
		FlushDispatcherQueue();

		bindingErrors.Errors.Should().BeEmpty(
			"the overlay must complete its first bind cleanly — a binding error here is a renamed/mistyped " +
			"path the WPF-free specs cannot see");

		// The footprint stays a compact pill: the feedback fits within the original bounds.
		// The width is now a deterministic 224 so the SizeToContent window can't under-size and
		// clip the right edge; still a compact pill, not a panel.
		content.DesiredSize.Width.Should().BeInRange(160, 240, "the overlay keeps its compact width");
		content.DesiredSize.Height.Should().BeInRange(20, 48, "the overlay stays a compact pill, not a panel");
	});

	// The durable-fix reproduction (WHISPER-139): construct the REAL overlay window and drive it through a
	// full realize -> show -> hide cycle. Constructing it realizes the HWND and first layout off-screen and
	// applies the native overlay styles; a warm-up signal then lights the pill and the bound view-model
	// (inline dispatcher) pushes it through to the window's Visibility. This guards the whole show path that
	// used to fail silently — a throw in the realize/interop path or a broken visibility binding fails here.
	// The on-screen coordinates stay the manual remainder, as with the content smoke above.
	[Fact]
	public void Overlay_window_realizes_and_toggles_visibility_through_a_real_show_cycle() => StaThread.Run(() =>
	{
		using BindingErrorCollector bindingErrors = new();
		WeakReferenceMessenger messenger = new();
		using LevelOverlayController controller = new(new RecordingStateMachine(), new StubAudioSource(), messenger, TimeProvider.System);
		using LevelOverlayViewModel viewModel = new(controller, new InlineDispatcher());

		// Realizes the window off-screen (HWND + first layout) and applies its overlay styles. Must not throw.
		IMonitorCatalog monitors = Substitute.For<IMonitorCatalog>();
		monitors.GetMonitors().Returns([new MonitorInfo("\\\\.\\DISPLAY1", "Primary display (1920 × 1080)", true, 0, 0, 1920, 1040)]);
		using LevelOverlay overlay = new(viewModel, monitors, NullLogger<LevelOverlay>.Instance);
		FlushDispatcherQueue();

		viewModel.IsOverlayVisible.Should().BeFalse("the overlay is hidden at rest");

		// A show signal (the app-wide warm-up, exactly as the warm-up service publishes it) must make the
		// overlay visible through the real Visibility binding on the realized window.
		messenger.Send(new ModelWarmupChangedMessage(true));
		FlushDispatcherQueue();
		viewModel.IsOverlayVisible.Should().BeTrue("a show signal makes the overlay visible");

		// Clearing the signal hides it again — a clean Visible -> Hidden toggle on the same live window.
		messenger.Send(new ModelWarmupChangedMessage(false));
		FlushDispatcherQueue();
		viewModel.IsOverlayVisible.Should().BeFalse("clearing the signal hides the overlay again");

		bindingErrors.Errors.Should().BeEmpty(
			"the overlay window must bind cleanly through a real realize/show/hide cycle");
	});

	private static void FlushDispatcherQueue()
	{
		DispatcherFrame frame = new();
		Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => frame.Continue = false);
		Dispatcher.PushFrame(frame);
	}

	// Minimal seams so the real view-model can be constructed without a capture device or live dispatcher.
	private sealed class StubAudioSource : IAudioSource
	{
		public CaptureFormat? Format => null;

		public event EventHandler<AudioFrameAvailableEventArgs>? FrameAvailable
		{
			add { }
			remove { }
		}

		public event EventHandler<AudioCaptureFailedEventArgs>? CaptureFailed
		{
			add { }
			remove { }
		}

		public void Start()
		{
		}

		public void Stop()
		{
		}
	}

	private sealed class InlineDispatcher : IUiDispatcher
	{
		public bool CheckAccess() => true;

		public void Post(Action action) => action();

		public Task InvokeAsync(Action action)
		{
			action();
			return Task.CompletedTask;
		}
	}
}
