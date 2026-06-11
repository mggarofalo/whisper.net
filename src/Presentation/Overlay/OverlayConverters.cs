// View glue for the dictation overlay feedback (WHISPER-102): maps the WPF-free view-model state to the
// brushes / visibility the code-built overlay binds. Bindings (not PropertyChanged subscriptions) keep the
// view declarative (WHISPER-92); these converters are the small translation layer between the Logic enum /
// flags and WPF visuals.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Logic.AppManagement;

namespace Presentation.Overlay;

// The state dot's colour: green while recording, blue while transcribing, red on error.
public sealed class OverlayStateToBrushConverter : IValueConverter
{
	private static readonly Brush Recording = Frozen(Color.FromRgb(0x4C, 0xAF, 0x50));
	private static readonly Brush Transcribing = Frozen(Color.FromRgb(0x42, 0xA5, 0xF5));
	private static readonly Brush Error = Frozen(Color.FromRgb(0xE5, 0x39, 0x35));

	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		value is OverlayState state
			? state switch
			{
				OverlayState.Transcribing => Transcribing,
				OverlayState.Error => Error,
				_ => Recording,
			}
			: Recording;

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();

	private static Brush Frozen(Color color)
	{
		SolidColorBrush brush = new(color);
		brush.Freeze();
		return brush;
	}
}

// The meter's colour: amber once the recording nears the duration cap (WHISPER-111), green otherwise.
public sealed class NearCapToBrushConverter : IValueConverter
{
	private static readonly Brush Normal = Frozen(Colors.LimeGreen);
	private static readonly Brush Warning = Frozen(Color.FromRgb(0xFF, 0xB3, 0x00));

	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		value is true ? Warning : Normal;

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();

	private static Brush Frozen(Color color)
	{
		SolidColorBrush brush = new(color);
		brush.Freeze();
		return brush;
	}
}

// Elapsed time is shown only while recording (it is the recording's running duration).
public sealed class RecordingToVisibilityConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		value is OverlayState.Recording ? Visibility.Visible : Visibility.Collapsed;

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}

// A spoken/automation label for the overlay's current state, so the cue is not colour-only.
public sealed class OverlayStateToNameConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		value is OverlayState state
			? state switch
			{
				OverlayState.Transcribing => "Transcribing",
				OverlayState.Error => "Dictation error",
				_ => "Recording",
			}
			: "Recording";

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
