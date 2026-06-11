// The mini-recorder overlay window (WHISPER-26): a small, frameless, top-most, click-through-tolerant
// WPF window that appears while recording and shows a live microphone-level meter. It is pure view glue
// over the LevelOverlayViewModel: visibility and the meter value are DATA-BOUND (WHISPER-92) — no
// PropertyChanged subscription, no property-name switch — so a renamed view-model property refactors
// the nameof-based paths or fails loudly instead of silently freezing the overlay. Built in code (no
// XAML) so it stays a thin, self-contained view, verified by smoke rather than the specs.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Logic.AppManagement;

namespace Presentation.Overlay;

public sealed class LevelOverlay : IDisposable
{
	private readonly Window _window;

	public LevelOverlay(LevelOverlayViewModel viewModel)
	{
		ProgressBar meter = new()
		{
			Minimum = 0,
			Maximum = 1,
			Width = 180,
			Height = 8,
			Foreground = Brushes.LimeGreen,
		};
		meter.SetBinding(ProgressBar.ValueProperty, new Binding(nameof(LevelOverlayViewModel.Level)));

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
			Content = new Border
			{
				Background = new SolidColorBrush(Color.FromArgb(170, 16, 16, 16)),
				CornerRadius = new CornerRadius(8),
				Padding = new Thickness(14, 10, 14, 10),
				Child = meter,
			},
		};

		// Setting a Window's Visibility shows/hides it exactly like Show()/Hide(), so the overlay's whole
		// lifecycle is one binding.
		_window.SetBinding(UIElement.VisibilityProperty,
			new Binding(nameof(LevelOverlayViewModel.IsOverlayVisible)) { Converter = new BooleanToVisibilityConverter() });

		// Place the overlay bottom-center of the work area (WHISPER-100) each time it is shown and once its
		// size settles. The overlay is transient — shown only while recording — so resolving the work area
		// on every show keeps it correct after the work area changes (taskbar moved/resized) between
		// recordings; a change DURING an active recording is the manual remainder. The placement math is the
		// WPF-free OverlayPlacement in Logic; this resolves the work area and applies the result.
		_window.IsVisibleChanged += OnVisibleChanged;
		_window.SizeChanged += OnSizeChanged;
	}

	private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if (_window.IsVisible)
		{
			Reposition();
		}
	}

	private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Reposition();

	// Resolve the work area (of the monitor holding the focused window, falling back to the primary) and
	// apply the bottom-center placement. Sizes are read after layout, so ActualWidth/Height are final.
	private void Reposition()
	{
		if (_window.ActualWidth <= 0 || _window.ActualHeight <= 0)
		{
			return;
		}

		OverlayRect workArea = ForegroundMonitor.WorkArea(_window) ?? PrimaryWorkArea();
		(double left, double top) = OverlayPlacement.BottomCenter(workArea, _window.ActualWidth, _window.ActualHeight);
		_window.Left = left;
		_window.Top = top;
	}

	private static OverlayRect PrimaryWorkArea()
	{
		Rect workArea = SystemParameters.WorkArea;
		return new OverlayRect(workArea.Left, workArea.Top, workArea.Width, workArea.Height);
	}

	public void Dispose()
	{
		_window.IsVisibleChanged -= OnVisibleChanged;
		_window.SizeChanged -= OnSizeChanged;
		_window.Close();
	}
}
