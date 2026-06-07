// The real IClipboard: the single place that touches the Win32 clipboard. Reads and writes CF_UNICODETEXT
// and exposes the system change count (GetClipboardSequenceNumber) the paste path uses to avoid clobbering
// concurrently-copied content. Like Win32KeyboardInput this is the I/O boundary — its restore-guard
// consumer (ClipboardTextInjector) is what carries the tested behavior; this adapter is verified by smoke.

using System.ComponentModel;
using System.Runtime.InteropServices;
using Application.Ports;

namespace Infrastructure.TextDelivery;

public sealed class Win32Clipboard : IClipboard
{
	private const uint CfUnicodeText = 13;
	private const uint GmemMoveable = 0x0002;

	public uint GetChangeCount() => GetClipboardSequenceNumber();

	public string? GetText()
	{
		if (!IsClipboardFormatAvailable(CfUnicodeText))
		{
			return null;
		}

		if (!OpenClipboard(nint.Zero))
		{
			throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open the clipboard to read text.");
		}

		try
		{
			nint handle = GetClipboardData(CfUnicodeText);
			if (handle == nint.Zero)
			{
				return null;
			}

			nint pointer = GlobalLock(handle);
			if (pointer == nint.Zero)
			{
				return null;
			}

			try
			{
				return Marshal.PtrToStringUni(pointer);
			}
			finally
			{
				GlobalUnlock(handle);
			}
		}
		finally
		{
			CloseClipboard();
		}
	}

	public void SetText(string text)
	{
		if (!OpenClipboard(nint.Zero))
		{
			throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open the clipboard to set text.");
		}

		try
		{
			EmptyClipboard();

			// Moveable global memory holding the null-terminated UTF-16 string the clipboard takes ownership of.
			int byteCount = (text.Length + 1) * sizeof(char);
			nint block = GlobalAlloc(GmemMoveable, (nuint)byteCount);
			if (block == nint.Zero)
			{
				throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not allocate clipboard memory.");
			}

			nint target = GlobalLock(block);
			if (target == nint.Zero)
			{
				GlobalFree(block);
				throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not lock clipboard memory.");
			}

			try
			{
				Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
				Marshal.WriteInt16(target, text.Length * sizeof(char), 0);
			}
			finally
			{
				GlobalUnlock(block);
			}

			if (SetClipboardData(CfUnicodeText, block) == nint.Zero)
			{
				// Ownership did not transfer to the system, so we must free the block ourselves.
				GlobalFree(block);
				throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not set clipboard text.");
			}

			// On success the system owns the memory; freeing it here would corrupt the clipboard.
		}
		finally
		{
			CloseClipboard();
		}
	}

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool OpenClipboard(nint hWndNewOwner);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool CloseClipboard();

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool EmptyClipboard();

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool IsClipboardFormatAvailable(uint format);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern nint GetClipboardData(uint format);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern nint SetClipboardData(uint format, nint hMem);

	[DllImport("user32.dll")]
	private static extern uint GetClipboardSequenceNumber();

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern nint GlobalAlloc(uint uFlags, nuint dwBytes);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern nint GlobalFree(nint hMem);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern nint GlobalLock(nint hMem);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GlobalUnlock(nint hMem);
}
