// A WinExe has no console of its own, so when the app is launched with --doctor (WHISPER-50) from a
// terminal its diagnostic report would have nowhere to go. This helper attaches to the parent process's
// console (the terminal that launched it) for the duration of a write, so the report appears inline where
// the user ran the command, then detaches. When there is no parent console (e.g. a double-click) the
// attach simply fails and the write is a no-op — the doctor flag is a command-line affordance.

using System.Runtime.InteropServices;

namespace Presentation.Diagnostics;

internal static class ConsoleOutput
{
	private const int AttachParentProcess = -1;

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool AttachConsole(int processId);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool FreeConsole();

	/// <summary>Writes the text to the parent terminal's console (if any), followed by a newline.</summary>
	public static void WriteLine(string text)
	{
		bool attached = AttachConsole(AttachParentProcess);
		try
		{
			Console.Out.WriteLine(text);
			Console.Out.Flush();
		}
		finally
		{
			if (attached)
			{
				FreeConsole();
			}
		}
	}
}
