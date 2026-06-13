// The real IKeyboardInput: the single place that calls Win32 SendInput. It translates the layer's
// KeyEvent list into the INPUT array the OS expects — KEYEVENTF_UNICODE for character events (wScan
// carries the code unit, wVk is 0) and a virtual-key event otherwise (wVk carries the key) — then
// sends the whole batch in one call so the keystrokes arrive contiguously in the focused window. A
// short send (the OS dropped events, e.g. blocked by UIPI into an elevated window) surfaces as a
// Win32Exception rather than silently losing characters.

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Infrastructure.TextDelivery;

public sealed class Win32KeyboardInput : IKeyboardInput
{
	private const uint InputKeyboard = 1;
	private const uint KeyEventfKeyUp = 0x0002;
	private const uint KeyEventfUnicode = 0x0004;

	public void Send(IReadOnlyList<KeyEvent> events)
	{
		if (events.Count == 0)
		{
			return;
		}

		INPUT[] inputs = new INPUT[events.Count];
		for (int i = 0; i < events.Count; i++)
		{
			KeyEvent keyEvent = events[i];
			uint flags = keyEvent.Action == KeyAction.Up ? KeyEventfKeyUp : 0;

			KEYBDINPUT keyboard = keyEvent.IsUnicode
				? new KEYBDINPUT { wVk = 0, wScan = keyEvent.Code, dwFlags = flags | KeyEventfUnicode }
				: new KEYBDINPUT { wVk = keyEvent.Code, wScan = 0, dwFlags = flags };

			inputs[i] = new INPUT { type = InputKeyboard, U = new InputUnion { ki = keyboard } };
		}

		uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
		if (sent != inputs.Length)
		{
			throw new Win32Exception(Marshal.GetLastWin32Error(), "SendInput did not deliver all keystrokes.");
		}
	}

	// The marshaled size of INPUT, which is the cbSize SendInput requires. Exposed so a guard test pins it
	// to the platform-correct value (40 on x64, 28 on x86) — a wrong size makes SendInput reject the batch
	// with ERROR_INVALID_PARAMETER, which is exactly the bug this struct layout fixes.
	internal static int NativeInputSize => Marshal.SizeOf<INPUT>();

	[DllImport("user32.dll", SetLastError = true)]
	private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

	[StructLayout(LayoutKind.Sequential)]
	private struct INPUT
	{
		public uint type;
		public InputUnion U;
	}

	// The native INPUT union must be sized for its LARGEST member (MOUSEINPUT, 32 bytes on x64), not just
	// KEYBDINPUT (24) — otherwise Marshal.SizeOf<INPUT> is 32 instead of the 40 Windows expects, and the
	// resulting cbSize mismatch makes SendInput fail with error 87. mi is unused but pads the
	// union to the correct size; both members overlap at offset 0 exactly as the Win32 union does.
	[StructLayout(LayoutKind.Explicit)]
	private struct InputUnion
	{
		[FieldOffset(0)]
		public MOUSEINPUT mi;

		[FieldOffset(0)]
		public KEYBDINPUT ki;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct KEYBDINPUT
	{
		public ushort wVk;
		public ushort wScan;
		public uint dwFlags;
		public uint time;
		public nuint dwExtraInfo;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct MOUSEINPUT
	{
		public int dx;
		public int dy;
		public uint mouseData;
		public uint dwFlags;
		public uint time;
		public nuint dwExtraInfo;
	}
}
