// View-level smoke for the dictation overlay (WHISPER-102, WHISPER-96 pattern), on a dedicated STA
// thread. It guards the overlay's view glue: the compact content builds and completes its first bind
// against the real LevelOverlayViewModel with no data-binding trace error (a renamed/mistyped path fails),
// and its footprint stays a compact pill (AC4) — the feedback was added within the original bounds, not
// by growing it into a panel. The on-screen styling/colours are the manual remainder.

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Application.Ports;
using AwesomeAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Domain.Audio;
using Logic.AppManagement;
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
			"path the WPF-free specs cannot see (WHISPER-96 AC1)");

		// The footprint stays a compact pill (WHISPER-102 AC4): the feedback fits within the original bounds.
		// The width is now a deterministic 224 (WHISPER-124) so the SizeToContent window can't under-size and
		// clip the right edge; still a compact pill, not a panel.
		content.DesiredSize.Width.Should().BeInRange(160, 240, "the overlay keeps its compact width");
		content.DesiredSize.Height.Should().BeInRange(20, 48, "the overlay stays a compact pill, not a panel");
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
