// Maps the dictation status to the tray's speech-bubble icon (idle grey, recording red, transcribing
// orange) so the icon is data-bound to the view-model's Status rather than refreshed by a
// property-name switch. The bubbles are pre-rendered .ico glyphs embedded as WPF resources and
// addressed by pack:// URI: H.NotifyIcon resolves the IconSource URI to a stream and hands it to
// System.Drawing.Icon, which parses only the ICO container — a PNG or in-memory bitmap throws there.
// Decoded icons are cached so repeated status changes don't re-load.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Domain.Recording;

namespace Presentation.Tray;

public sealed class RecordingStateToIconSourceConverter : IValueConverter
{
	private readonly Dictionary<string, ImageSource> _cache = [];

	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		string fileName = value switch
		{
			RecordingState.Recording => "tray-recording.ico",
			RecordingState.Transcribing => "tray-transcribing.ico",
			_ => "tray-idle.ico",
		};

		if (!_cache.TryGetValue(fileName, out ImageSource? icon))
		{
			icon = Load(fileName);
			_cache[fileName] = icon;
		}

		return icon;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException("The tray icon binding is one-way.");

	private static ImageSource Load(string fileName)
	{
		BitmapImage image = new();
		image.BeginInit();
		image.UriSource = new Uri($"pack://application:,,,/Resources/{fileName}");
		image.CacheOption = BitmapCacheOption.OnLoad; // decode now so the source is self-contained and freezable
		image.EndInit();
		image.Freeze();
		return image;
	}
}
