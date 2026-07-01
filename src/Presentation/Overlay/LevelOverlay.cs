// The mini-recorder overlay window: a small, frameless, top-most, genuinely click-through WPF window that
// appears while recording/transcribing/warming and shows recording state, a live microphone-level meter,
// the elapsed time, and a near-cap warning. It is pure view glue over the LevelOverlayViewModel: the
// content is DATA-BOUND (no PropertyChanged subscription) so a renamed view-model property refactors the
// nameof paths or fails loudly instead of silently freezing the overlay.
//
// The window is rebuilt for reliability (WHISPER-139). Two WPF failure modes made the old overlay
// intermittently never appear: (1) it was created hidden and never Show()n, so the FIRST flip to Visible
// was a fragile lazy first-time show (transparent + SizeToContent + ShowActivated=false), and (2) with
// ShowInTaskbar=false WPF gives the window a hidden owner that may lack WS_EX_TOPMOST, so Topmost=true
// alone could leave it rendering BEHIND the focused app. The durable fix: realize the HWND and run a full
// layout pass ONCE off-screen at construction (so ActualWidth/Height are final and every later show is a
// plain Hidden->Visible toggle on a live window), apply explicit overlay extended styles once the HWND
// exists, and on every show reposition against the target monitor and force the window to the top of the
// Z-order via an explicit HWND_TOPMOST SetWindowPos. The whole path is logged so a single dictation on a
// user's machine reveals exactly what happened (visibility, thread, computed position, size, work area).

using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Application.Display;
using Application.Ports;
using Logic.AppManagement;
using Microsoft.Extensions.Logging;

namespace Presentation.Overlay;

public sealed class LevelOverlay : IDisposable
{
	private readonly Window _window;
	private readonly IMonitorCatalog _monitors;
	private readonly ILogger<LevelOverlay> _logger;
	private string? _targetDeviceName;
	private bool _realizing;
	private bool _disposed;

	public LevelOverlay(LevelOverlayViewModel viewModel, IMonitorCatalog monitors, ILogger<LevelOverlay> logger)
	{
		_monitors = monitors;
		_logger = logger;

		_window = new Window
		{
			// Frameless, transparent, top-most, and out of the taskbar so it reads as an overlay, not a window.
			WindowStyle = WindowStyle.None,
			AllowsTransparency = true,
			Background = Brushes.Transparent,
			Topmost = true,
			ShowInTaskbar = false,
			ShowActivated = false,
			ResizeMode = ResizeMode.NoResize,
			SizeToContent = SizeToContent.WidthAndHeight,
			// Positioned manually (see Reposition); parked off every screen until the first real show so the
			// one-time realize pass below never flashes on screen.
			WindowStartupLocation = WindowStartupLocation.Manual,
			Left = OffScreen,
			Top = OffScreen,
			// Click-through tolerant within WPF; OverlayWindowInterop adds OS-level click-through on top.
			Focusable = false,
			IsHitTestVisible = false,
			DataContext = viewModel,
			Content = BuildContent(),
		};

		// Apply the overlay's native extended styles the moment the HWND exists (genuine click-through,
		// no-activate, no Alt-Tab). SourceInitialized fires during the realize Show() below.
		_window.SourceInitialized += OnSourceInitialized;
		_window.IsVisibleChanged += OnVisibleChanged;
		_window.SizeChanged += OnSizeChanged;

		Realize();

		// Only now bind Visibility to the view-model. Starting hidden (IsOverlayVisible=false) it stays hidden;
		// each later flip to true is a robust Hidden->Visible toggle on an already-realized window, not a lazy
		// first-time show. Setting Visibility shows/hides the window exactly like Show()/Hide().
		_window.SetBinding(UIElement.VisibilityProperty,
			new Binding(nameof(LevelOverlayViewModel.IsOverlayVisible)) { Converter = new BooleanToVisibilityConverter() });
	}

	// Far outside any physical or virtual screen, so the one-time realize show is never visible.
	private const double OffScreen = -32000;

	// Realize the window once, off-screen: create the HWND and run a full layout pass so ActualWidth/Height
	// are final before the first real show. ShowActivated=false + the off-screen park keep this invisible and
	// non-focus-stealing. Defensive: a failure here must not strand startup — the Visibility binding set by the
	// caller still drives the overlay (falling back to the old lazy first-show), and it is logged.
	private void Realize()
	{
		try
		{
			// Guard the visibility/size events this pass raises: they must not reposition the parked window
			// on-screen or log a misleading "shown" — this show is purely to build the HWND and measure.
			_realizing = true;
			_window.Show();
			_window.UpdateLayout();
			_window.Hide();

			_logger.LogInformation(
				"Overlay window realized off-screen: size {Width}x{Height}, hwnd {Hwnd}.",
				_window.ActualWidth, _window.ActualHeight, Handle());
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Overlay window could not be realized up front; falling back to lazy first-show.");
		}
		finally
		{
			_realizing = false;
		}
	}

	// The overlay's compact content: a single pill — a state-coloured dot, the level meter, and the elapsed
	// time. Public + static so the smoke harness builds and measures the very same layout against a bound VM.
	public static FrameworkElement BuildContent()
	{
		Ellipse stateDot = new()
		{
			Width = 10,
			Height = 10,
			Margin = new Thickness(0, 0, 8, 0),
			VerticalAlignment = VerticalAlignment.Center,
		};
		stateDot.SetBinding(Shape.FillProperty,
			new Binding(nameof(LevelOverlayViewModel.State)) { Converter = new OverlayStateToBrushConverter() });
		stateDot.SetBinding(AutomationProperties.NameProperty,
			new Binding(nameof(LevelOverlayViewModel.State)) { Converter = new OverlayStateToNameConverter() });

		ProgressBar meter = new()
		{
			Minimum = 0,
			Maximum = 1,
			Width = 120,
			Height = 8,
			VerticalAlignment = VerticalAlignment.Center,
		};
		meter.SetBinding(ProgressBar.ValueProperty, new Binding(nameof(LevelOverlayViewModel.Level)));
		meter.SetBinding(Control.ForegroundProperty,
			new Binding(nameof(LevelOverlayViewModel.IsNearCap)) { Converter = new NearCapToBrushConverter() });
		// Hidden while the model is warming up — there is no input level yet, so the pill
		// collapses to the dot + the "Warming up…" label; shown for every other state.
		meter.SetBinding(UIElement.VisibilityProperty,
			new Binding(nameof(LevelOverlayViewModel.State)) { Converter = new MeterVisibilityConverter() });

		TextBlock elapsed = new()
		{
			Foreground = Brushes.White,
			FontSize = 12,
			Margin = new Thickness(8, 0, 0, 0),
			VerticalAlignment = VerticalAlignment.Center,
			// A fixed slot (not MinWidth) so the elapsed text never overflows and the layout width is
			// deterministic; centred so the time reads tidily and is never clipped.
			Width = 44,
			TextAlignment = TextAlignment.Center,
		};
		elapsed.SetBinding(TextBlock.TextProperty, new Binding(nameof(LevelOverlayViewModel.ElapsedText)));
		elapsed.SetBinding(UIElement.VisibilityProperty,
			new Binding(nameof(LevelOverlayViewModel.State)) { Converter = new RecordingToVisibilityConverter() });

		// The warming label: the on-screen "Warming up…" text the pill shows while the model
		// warms, so the cue is not colour-only. Hidden for every other state (the meter/elapsed show then), so
		// the Recording/Transcribing/Error layouts — and the smoke harness's compact-footprint check — are
		// unchanged. The dot's automation name already announces "Warming up the model" for screen readers.
		TextBlock warming = new()
		{
			Foreground = Brushes.White,
			FontSize = 12,
			VerticalAlignment = VerticalAlignment.Center,
			Text = "Warming up…",
		};
		warming.SetBinding(UIElement.VisibilityProperty,
			new Binding(nameof(LevelOverlayViewModel.State)) { Converter = new WarmingToVisibilityConverter() });

		// Centre the pill's content so it stays balanced when the elapsed time collapses (transcribing/error).
		StackPanel row = new()
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		row.Children.Add(stateDot);
		row.Children.Add(meter);
		row.Children.Add(elapsed);
		row.Children.Add(warming);

		return new Border
		{
			Background = new SolidColorBrush(Color.FromArgb(170, 16, 16, 16)),
			CornerRadius = new CornerRadius(8),
			Padding = new Thickness(14, 10, 14, 10),
			// A deterministic width so the SizeToContent window adopts a stable size and the rounded pill is
			// never cut off on the right: dot(10+8) + meter(120) + elapsed(8+44) + padding(28)
			// = 218; 224 leaves a little slack. Stays within the compact-pill footprint.
			Width = 224,
			Child = row,
		};
	}

	private void OnSourceInitialized(object? sender, EventArgs e)
	{
		nint hwnd = Handle();
		if (hwnd == nint.Zero)
		{
			return;
		}

		OverlayWindowInterop.MakeOverlayStyled(hwnd);
		_logger.LogInformation("Overlay window native styles applied (click-through, no-activate, tool-window).");
	}

	private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		// The off-screen realize pass toggles visibility to build the HWND; ignore it entirely so the parked
		// window is never moved on-screen or reported as shown.
		if (_realizing)
		{
			return;
		}

		if (!_window.IsVisible)
		{
			_logger.LogDebug("Overlay hidden.");
			return;
		}

		string target = Reposition();

		// Re-assert top-most on every show: this is the durable defence against the ShowInTaskbar hidden-owner
		// case where the overlay is visible but rendered behind the focused app.
		nint hwnd = Handle();
		if (hwnd != nint.Zero)
		{
			OverlayWindowInterop.BringToTopmost(hwnd);
		}

		_logger.LogInformation(
			"Overlay shown: placed at ({Left},{Top}), size {Width}x{Height}, target {Target}, hwnd {Hwnd}.",
			_window.Left, _window.Top, _window.ActualWidth, _window.ActualHeight, target, hwnd);
	}

	/// <summary>
	/// Point the overlay at a configured display (by device name; null = follow the primary). Called from the
	/// composition root on startup and whenever the setting changes. Re-places immediately if the overlay is
	/// currently visible so a change is reflected without waiting for the next dictation.
	/// </summary>
	public void SetTargetMonitor(string? deviceName)
	{
		if (string.Equals(_targetDeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		_targetDeviceName = deviceName;
		_logger.LogInformation("Overlay target monitor set to {Target}.", deviceName ?? "primary (default)");

		if (_realizing || !_window.IsVisible)
		{
			return;
		}

		_ = Reposition();
		nint hwnd = Handle();
		if (hwnd != nint.Zero)
		{
			OverlayWindowInterop.BringToTopmost(hwnd);
		}
	}

	private void OnSizeChanged(object sender, SizeChangedEventArgs e)
	{
		// The size settling (e.g. a DPI change while shown) can move the anchor point; keep it correct. Never
		// during the off-screen realize pass — that would drag the parked window on-screen.
		if (!_realizing && _window.IsVisible)
		{
			_ = Reposition();
		}
	}

	// Place the pill bottom-center of the configured monitor's work area. The monitor is resolved through the
	// catalog (device name from settings; null = primary), self-healing to the primary if the chosen display
	// is gone. The catalog's work areas are already in WPF's DIP coordinate space, so Window.Left/Top land the
	// window on-screen on any monitor. If the catalog is unavailable, fall back to the primary work area from
	// SystemParameters (also DIPs). Sizes are read after the realize layout pass, so ActualWidth/Height are
	// final even on the very first show. Returns a short description of the chosen target for the caller's log.
	private string Reposition()
	{
		if (_window.ActualWidth <= 0 || _window.ActualHeight <= 0)
		{
			_logger.LogWarning("Overlay reposition skipped: layout not settled (size {Width}x{Height}).",
				_window.ActualWidth, _window.ActualHeight);
			return "unsettled";
		}

		OverlayRect workArea;
		string target;

		MonitorInfo? chosen = OverlayPlacement.ChooseMonitor(_monitors.GetMonitors(), _targetDeviceName);
		if (chosen is not null)
		{
			workArea = new OverlayRect(chosen.WorkAreaLeft, chosen.WorkAreaTop, chosen.WorkAreaWidth, chosen.WorkAreaHeight);
			target = chosen.FriendlyName;
		}
		else
		{
			Rect area = SystemParameters.WorkArea;
			workArea = new OverlayRect(area.Left, area.Top, area.Width, area.Height);
			target = "primary work area (catalog unavailable)";
		}

		(double left, double top) = OverlayPlacement.BottomCenter(workArea, _window.ActualWidth, _window.ActualHeight);
		_window.Left = left;
		_window.Top = top;
		return target;
	}

	private nint Handle() => new WindowInteropHelper(_window).Handle;

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_window.SourceInitialized -= OnSourceInitialized;
		_window.IsVisibleChanged -= OnVisibleChanged;
		_window.SizeChanged -= OnSizeChanged;
		_window.Close();
	}
}
