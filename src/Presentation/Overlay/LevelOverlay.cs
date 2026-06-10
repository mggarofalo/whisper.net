// The mini-recorder overlay window (WHISPER-26): a small, frameless, top-most, click-through-tolerant
// WPF window that appears while recording and shows a live microphone-level meter. It is pure view glue
// over the LevelOverlayViewModel — it shows/hides as the view-model's visibility changes and moves the
// meter as the level changes — so, like the tray icon, it is verified by smoke rather than the specs.
// Built in code (no XAML) so it stays a thin, self-contained view.

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Logic.AppManagement;

namespace Presentation.Overlay;

public sealed class LevelOverlay : IDisposable
{
	private readonly LevelOverlayViewModel _viewModel;
	private readonly Window _window;
	private readonly ProgressBar _meter;

	public LevelOverlay(LevelOverlayViewModel viewModel)
	{
		_viewModel = viewModel;

		_meter = new ProgressBar
		{
			Minimum = 0,
			Maximum = 1,
			Width = 180,
			Height = 8,
			Value = viewModel.Level,
			Foreground = Brushes.LimeGreen,
		};

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
			WindowStartupLocation = WindowStartupLocation.CenterScreen,
			// Click-through tolerant: the overlay never steals focus or absorbs clicks from the app below.
			Focusable = false,
			IsHitTestVisible = false,
			Content = new Border
			{
				Background = new SolidColorBrush(Color.FromArgb(170, 16, 16, 16)),
				CornerRadius = new CornerRadius(8),
				Padding = new Thickness(14, 10, 14, 10),
				Child = _meter,
			},
		};

		_viewModel.PropertyChanged += OnViewModelPropertyChanged;
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(LevelOverlayViewModel.IsOverlayVisible):
				if (_viewModel.IsOverlayVisible)
				{
					_window.Show();
				}
				else
				{
					_window.Hide();
				}

				break;

			case nameof(LevelOverlayViewModel.Level):
				_meter.Value = _viewModel.Level;
				break;
		}
	}

	public void Dispose()
	{
		_viewModel.PropertyChanged -= OnViewModelPropertyChanged;
		_window.Close();
	}
}
