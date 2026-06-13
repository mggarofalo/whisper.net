// Hotkey diagnostic: confirms the configured global hotkey can be registered. Registering a
// global hotkey means installing the low-level keyboard hook (SharpHook), which the OS only allows when
// the app has the required input permission — the same permission dictation delivery needs. So the check
// reads the configured chord from settings and asks the IPermissionProbe whether that hook can be
// installed. Fails when the permission is not granted (the hotkey could not be registered); passes
// otherwise, naming the chord. The probe is non-acquiring, so there is nothing to release afterwards.

using Application.Diagnostics;
using Application.Ports;
using Domain.Settings;

namespace Logic.AppManagement.Diagnostics;

public sealed class HotkeyCheck(ISettingsStore settings, IPermissionProbe permissions) : IDiagnosticCheck
{
	public string Name => "Hotkey";

	public async ValueTask<DiagnosticResult> RunAsync(CancellationToken cancellationToken)
	{
		AppSettings current = await settings.LoadAsync(cancellationToken);
		string chord = current.Hotkey.Chord;

		if (!permissions.HasRequiredInputPermissions())
		{
			return new DiagnosticResult(Name, DiagnosticStatus.Fail, $"The global keyboard hook required to register '{chord}' is not permitted.");
		}

		return new DiagnosticResult(Name, DiagnosticStatus.Pass, $"The global hotkey '{chord}' can be registered.");
	}
}
