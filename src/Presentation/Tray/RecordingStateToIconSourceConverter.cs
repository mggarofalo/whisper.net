// Maps the dictation status to the tray's coloured-dot icon (idle grey, recording red, transcribing
// orange) so the icon is data-bound to the view-model's Status (WHISPER-92) rather than refreshed by a
// property-name switch. The dot is drawn by H.NotifyIcon, so no icon asset ships with the app.

using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Domain.Recording;
using H.NotifyIcon;

namespace Presentation.Tray;

public sealed class RecordingStateToIconSourceConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => new GeneratedIconSource
	{
		Text = "●",
		Foreground = new SolidColorBrush(value switch
		{
			RecordingState.Recording => Colors.Red,
			RecordingState.Transcribing => Colors.Orange,
			_ => Colors.Gray,
		}),
	};

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException("The tray icon binding is one-way.");
}
