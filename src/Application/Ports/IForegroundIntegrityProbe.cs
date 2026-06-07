// Port for detecting whether the focused window belongs to a higher-integrity (e.g. elevated) process
// than ours. Windows UIPI silently drops synthetic input from a lower-integrity process into a
// higher-integrity window, so the delivery pipeline consults this before typing and surfaces the
// limitation instead of appearing to "do nothing". Implemented in Infrastructure over Win32; faked in
// specs. No Win32 types cross this boundary — only the relative result below.

namespace Application.Ports;

/// <summary>The focused window's process integrity level relative to our own process.</summary>
public enum ForegroundIntegrity
{
	/// <summary>The focused window runs at the same integrity level as us — delivery proceeds normally.</summary>
	Same,

	/// <summary>The focused window runs at a higher integrity level — synthetic input would be dropped by UIPI.</summary>
	Higher,

	/// <summary>The focused window runs at a lower integrity level than us.</summary>
	Lower,

	/// <summary>The relative integrity could not be determined; callers should not block on uncertainty.</summary>
	Unknown,
}

/// <summary>
/// Compares the foreground window's process integrity to the current process, so delivery can detect
/// the UIPI case where typing into an elevated window would be silently dropped.
/// </summary>
public interface IForegroundIntegrityProbe
{
	/// <summary>Returns the foreground window's integrity level relative to this process.</summary>
	ForegroundIntegrity CompareForegroundToCurrent();
}
