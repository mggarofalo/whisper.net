// Decides whether the app was launched to run diagnostics rather than the tray UI. The
// Presentation entry point passes its command-line arguments here; if any is the doctor/selftest flag,
// the app runs the checks, prints the report, and exits instead of going tray-resident. Kept as a pure
// helper (no WPF, no process state) so the arg-routing decision is unit-tested directly.

namespace Logic.AppManagement.Diagnostics;

public static class DoctorMode
{
	// The accepted spellings of the diagnostics flag. "--doctor" is the documented form; the others are
	// forgiving aliases a user or a bug-report script might reach for.
	private static readonly string[] Flags = ["--doctor", "/doctor", "--selftest", "/selftest"];

	/// <summary>True when the command-line arguments request the doctor / selftest diagnostics run.</summary>
	public static bool IsRequested(IReadOnlyList<string>? args)
	{
		if (args is null)
		{
			return false;
		}

		foreach (string arg in args)
		{
			foreach (string flag in Flags)
			{
				if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
		}

		return false;
	}
}
