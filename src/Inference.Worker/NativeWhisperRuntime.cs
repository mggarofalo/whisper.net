using Whisper.net;
using Whisper.net.LibraryLoader;

namespace Inference.Worker;

internal static class NativeWhisperRuntime
{
	public static bool Probe()
	{
		RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Vulkan, RuntimeLibrary.Cpu];
		string probePath = Path.Combine(Path.GetTempPath(), $"whisper-native-probe-{Guid.NewGuid():N}.bin");
		try
		{
			File.WriteAllBytes(probePath, new byte[16]);
			using WhisperFactory factory = WhisperFactory.FromPath(probePath);
			return true;
		}
		catch (Exception ex) when (IsNativeLoadFailure(ex))
		{
			return false;
		}
		catch (Exception)
		{
			// The runtime loaded and rejected the intentionally invalid model.
			return true;
		}
		finally
		{
			try
			{
				File.Delete(probePath);
			}
			catch (IOException)
			{
				// Harmless probe residue must not change the diagnostic result.
			}
		}
	}

	private static bool IsNativeLoadFailure(Exception exception)
	{
		for (Exception? current = exception; current is not null; current = current.InnerException)
		{
			if (current is DllNotFoundException ||
				current is FileNotFoundException file &&
				file.Message.Contains("Native Library", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}
}
