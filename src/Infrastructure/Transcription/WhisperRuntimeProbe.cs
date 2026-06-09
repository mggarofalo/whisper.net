// Probes the Whisper.net native runtime by actually attempting to load it (WHISPER-85). WhisperFactory
// loads the native library (resolved relative to AppContext.BaseDirectory) in its constructor, BEFORE the
// model is parsed — so we hand it a present-but-invalid probe file: if the native runtime is missing the
// loader throws a DllNotFound/"Native Library not found" we classify as unavailable; if it loads, the only
// failure is parsing the bogus model, which we treat as "available". The Vulkan->CPU order means a machine
// with no GPU still reports available via the CPU library. This is exactly the load path that silently
// failed in the packaged app, so the doctor now exercises it for real.

using Application.Ports;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace Infrastructure.Transcription;

internal sealed class WhisperRuntimeProbe : IWhisperRuntimeProbe
{
	public WhisperRuntimeStatus Probe()
	{
		RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Vulkan, RuntimeLibrary.Cpu];

		// A present-but-invalid model file: its existence forces WhisperFactory past any file-existence guard
		// into the native load, so a native-load failure is distinguishable from a missing-file error.
		string probePath = Path.Combine(Path.GetTempPath(), $"whisper-native-probe-{Guid.NewGuid():N}.bin");
		try
		{
			File.WriteAllBytes(probePath, new byte[16]);
			using WhisperFactory factory = WhisperFactory.FromPath(probePath);
			return new WhisperRuntimeStatus(true, "Whisper native runtime loaded.");
		}
		catch (Exception ex) when (IsNativeLoadFailure(ex))
		{
			return new WhisperRuntimeStatus(false, $"Whisper native runtime could not be loaded: {Root(ex).Message}");
		}
		catch (Exception)
		{
			// The native runtime loaded; the only failure was the intentionally invalid probe model.
			return new WhisperRuntimeStatus(true, "Whisper native runtime loaded.");
		}
		finally
		{
			try
			{
				File.Delete(probePath);
			}
			catch (IOException)
			{
				// A leftover probe file in TEMP is harmless; never let cleanup mask the probe result.
			}
		}
	}

	private static bool IsNativeLoadFailure(Exception ex)
	{
		for (Exception? e = ex; e is not null; e = e.InnerException)
		{
			if (e is DllNotFoundException)
			{
				return true;
			}

			if (e is FileNotFoundException file && file.Message.Contains("Native Library", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	private static Exception Root(Exception ex)
	{
		Exception current = ex;
		while (current.InnerException is not null)
		{
			current = current.InnerException;
		}

		return current;
	}
}
