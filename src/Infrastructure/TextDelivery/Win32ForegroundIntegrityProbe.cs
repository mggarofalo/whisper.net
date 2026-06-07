// The real IForegroundIntegrityProbe: reads the integrity level of the foreground window's owning
// process and compares it to our own. It is deliberately defensive — any failure resolves to Unknown
// (which never blocks delivery) rather than throwing, except that being denied access to the foreground
// process is itself strong evidence it is higher-integrity, so that maps to Higher. The pure relative
// comparison is factored out (Compare) and unit-tested; the surrounding Win32 plumbing is the I/O
// boundary, verified by smoke. This is the single place that reads process tokens.

using System.ComponentModel;
using System.Runtime.InteropServices;
using Application.Ports;

namespace Infrastructure.TextDelivery;

public sealed class Win32ForegroundIntegrityProbe : IForegroundIntegrityProbe
{
	private const uint ProcessQueryLimitedInformation = 0x1000;
	private const uint TokenQuery = 0x0008;
	private const int TokenIntegrityLevel = 25;
	private const int ErrorInsufficientBuffer = 122;
	private const int ErrorAccessDenied = 5;

	public ForegroundIntegrity CompareForegroundToCurrent()
	{
		try
		{
			nint window = GetForegroundWindow();
			if (window == nint.Zero)
			{
				return ForegroundIntegrity.Unknown;
			}

			_ = GetWindowThreadProcessId(window, out uint processId);
			if (processId == 0)
			{
				return ForegroundIntegrity.Unknown;
			}

			if (!TryGetCurrentIntegrityRid(out uint currentRid))
			{
				return ForegroundIntegrity.Unknown;
			}

			return TryGetForegroundIntegrityRid(processId, out uint foregroundRid, out bool accessDenied)
				? Compare(foregroundRid, currentRid)
				// Can't even read the foreground process: if Windows denied us, it is almost certainly
				// running at a higher integrity than we are — exactly the UIPI case we want to surface.
				: accessDenied ? ForegroundIntegrity.Higher : ForegroundIntegrity.Unknown;
		}
		catch (Win32Exception)
		{
			return ForegroundIntegrity.Unknown;
		}
	}

	/// <summary>
	/// Relative comparison of two Windows integrity RIDs (e.g. SECURITY_MANDATORY_MEDIUM_RID 0x2000 vs
	/// HIGH 0x3000). <see cref="ForegroundIntegrity.Higher"/> means the foreground window outranks us and
	/// synthetic input would be dropped by UIPI. Exposed for unit testing the pure comparison.
	/// </summary>
	public static ForegroundIntegrity Compare(uint foregroundRid, uint currentRid) =>
		foregroundRid > currentRid ? ForegroundIntegrity.Higher
		: foregroundRid < currentRid ? ForegroundIntegrity.Lower
		: ForegroundIntegrity.Same;

	private static bool TryGetCurrentIntegrityRid(out uint rid)
	{
		rid = 0;
		if (!OpenProcessToken(GetCurrentProcess(), TokenQuery, out nint token))
		{
			return false;
		}

		try
		{
			return TryReadIntegrityRid(token, out rid);
		}
		finally
		{
			CloseHandle(token);
		}
	}

	private static bool TryGetForegroundIntegrityRid(uint processId, out uint rid, out bool accessDenied)
	{
		rid = 0;
		accessDenied = false;

		nint process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
		if (process == nint.Zero)
		{
			accessDenied = Marshal.GetLastWin32Error() == ErrorAccessDenied;
			return false;
		}

		try
		{
			if (!OpenProcessToken(process, TokenQuery, out nint token))
			{
				accessDenied = Marshal.GetLastWin32Error() == ErrorAccessDenied;
				return false;
			}

			try
			{
				return TryReadIntegrityRid(token, out rid);
			}
			finally
			{
				CloseHandle(token);
			}
		}
		finally
		{
			CloseHandle(process);
		}
	}

	// Reads TokenIntegrityLevel and extracts the RID (the SID's last sub-authority).
	private static bool TryReadIntegrityRid(nint token, out uint rid)
	{
		rid = 0;

		GetTokenInformation(token, TokenIntegrityLevel, nint.Zero, 0, out uint needed);
		if (needed == 0 && Marshal.GetLastWin32Error() != ErrorInsufficientBuffer)
		{
			return false;
		}

		nint buffer = Marshal.AllocHGlobal((int)needed);
		try
		{
			if (!GetTokenInformation(token, TokenIntegrityLevel, buffer, needed, out _))
			{
				return false;
			}

			// TOKEN_MANDATORY_LABEL begins with a SID_AND_ATTRIBUTES whose first field is the SID pointer.
			nint sid = Marshal.ReadIntPtr(buffer);
			if (sid == nint.Zero)
			{
				return false;
			}

			nint countPtr = GetSidSubAuthorityCount(sid);
			byte count = Marshal.ReadByte(countPtr);
			if (count == 0)
			{
				return false;
			}

			nint ridPtr = GetSidSubAuthority(sid, (uint)(count - 1));
			rid = (uint)Marshal.ReadInt32(ridPtr);
			return true;
		}
		finally
		{
			Marshal.FreeHGlobal(buffer);
		}
	}

	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();

	[DllImport("user32.dll", SetLastError = true)]
	private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

	[DllImport("kernel32.dll")]
	private static extern nint GetCurrentProcess();

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern nint OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

	[DllImport("advapi32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool OpenProcessToken(nint processHandle, uint desiredAccess, out nint tokenHandle);

	[DllImport("advapi32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetTokenInformation(nint tokenHandle, int tokenInformationClass, nint tokenInformation, uint tokenInformationLength, out uint returnLength);

	[DllImport("advapi32.dll")]
	private static extern nint GetSidSubAuthorityCount(nint sid);

	[DllImport("advapi32.dll")]
	private static extern nint GetSidSubAuthority(nint sid, uint subAuthority);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool CloseHandle(nint handle);
}
