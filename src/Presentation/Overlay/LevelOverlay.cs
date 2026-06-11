// The mini-recorder overlay window (WHISPER-26; feedback in WHISPER-102): a small, frameless, top-most,
// click-through-tolerant WPF window that appears while recording/transcribing and shows recording state, a
// live microphone-level meter, the elapsed time, and a near-cap warning. It is pure view glue over the
// LevelOverlayViewModel: everything is DATA-BOUND (WHISPER-92) — no PropertyChanged subscription, no
// property-name switch — so a renamed view-model property refactors the nameof-based paths or fails loudly
// instead of silently freezing the overlay. Built in code (no XAML) so it stays a thin, self-contained
// view; its compact content is built by the shared BuildContent factory so the smoke harness can construct
// and measure the exact same layout (WHISPER-102 AC4/AC5). The window is positioned bottom-center of the
// work area (WHISPER-100).

using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using Logic.AppManagement;

namespace Presentation.Overlay;

public sealed class LevelOverlay : IDisposable
{
	private readonly Window _window;

	public LevelOverlay(LevelOverlayViewModel viewModel)
	{
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
			// Positioned manually, bottom-center of the work area (WHISPER-100); see Reposition.
			WindowStartupLocation = WindowStartupLocation.Manual,
			// Click-through tolerant: the overlay never steals focus or absorbs clicks from the app below.
			Focusable = false,
			IsHitTestVisible = false,
			DataContext = viewModel,
			Content = BuildContent(),
		};

		// Setting a Window's Visibility shows/hides it exactly like Show()/Hide(), so the overlay's whole
		// lifecycle is one binding.
		_window.SetBinding(UIElement.VisibilityProperty,
			new Binding(nameof(LevelOverlayViewModel.IsOverlayVisible)) { Converter = new BooleanToVisibilityConverter() });

		// Place the overlay bottom-center of the work area (WHISPER-100) each time it is shown and once its
		// size settles. The overlay is transient — shown only while a dictation runs — so resolving the work
		// area on every show keeps it correct after the work area changes (taskbar moved/resized) between
		// recordings; a change DURING an active recording is the manual remainder. The placement math is the
		// WPF-free OverlayPlacement in Logic; this resolves the work area and applies the result.
		_window.IsVisibleChanged += OnVisibleChanged;
		_window.SizeChanged += OnSizeChanged;
	}

	// The overlay's compact content (WHISPER-102): a single pill — a state-coloured dot, the level meter,
	// and the elapsed time — kept within the original footprint. Public + static so the smoke harness builds
	// and measures the very same layout against a bound view-model.
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

		TextBlock elapsed = new()
		{
			Foreground = Brushes.White,
			FontSize = 12,
			Margin = new Thickness(8, 0, 0, 0),
			VerticalAlignment = VerticalAlignment.Center,
			MinWidth = 32,
		};
		elapsed.SetBinding(TextBlock.TextProperty, new Binding(nameof(LevelOverlayViewModel.ElapsedText)));
		elapsed.SetBinding(UIElement.VisibilityProperty,
			new Binding(nameof(LevelOverlayViewModel.State)) { Converter = new RecordingToVisibilityConverter() });

		StackPanel row = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
		row.Children.Add(stateDot);
		row.Children.Add(meter);
		row.Children.Add(elapsed);

		return new Border
		{
			Background = new SolidColorBrush(Color.FromArgb(170, 16, 16, 16)),
			CornerRadius = new CornerRadius(8),
			Padding = new Thickness(14, 10, 14, 10),
			Child = row,
		};
	}

	private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if (_window.IsVisible)
		{
			Reposition();
		}
	}

	private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Reposition();

	// Apply the bottom-center placement against the primary work area (WHISPER-117). SystemParameters.WorkArea
	// is already in DIPs, so the window lands on-screen regardless of the display scale — unlike the earlier
	// physical-pixel monitor probe (WHISPER-100), which mis-scaled on a non-100% display and pushed the
	// overlay off-screen. Sizes are read after layout, so ActualWidth/Height are final. (Placing on the
	// focused window's monitor on multi-monitor setups is a future enhancement that needs per-monitor DPI.)
	private void Reposition()
	{
		if (_window.ActualWidth <= 0 || _window.ActualHeight <= 0)
		{
			return;
		}

		Rect area = SystemParameters.WorkArea;
		OverlayRect workArea = new(area.Left, area.Top, area.Width, area.Height);
		(double left, double top) = OverlayPlacement.BottomCenter(workArea, _window.ActualWidth, _window.ActualHeight);
		_window.Left = left;
		_window.Top = top;
	}

	public void Dispose()
	{
		_window.IsVisibleChanged -= OnVisibleChanged;
		_window.SizeChanged -= OnSizeChanged;
		_window.Close();
	}
}
