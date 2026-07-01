// Handles CompleteOnboardingCommand: loads the current settings, persists them with
// SetupCompleted set, so the next launch reads a completed setup and skips onboarding. AppSettings is a
// value object with no settable members, so the completed copy is rebuilt through its constructor rather
// than a `with` expression.

using Application.Interfaces;
using Application.Ports;
using Domain.Settings;

namespace Application.Settings;

public sealed class CompleteOnboardingHandler(ISettingsStore store)
	: ICommandHandler<CompleteOnboardingCommand, Mediator.Unit>
{
	public async ValueTask<Mediator.Unit> Handle(CompleteOnboardingCommand command, CancellationToken cancellationToken)
	{
		AppSettings current = await store.LoadAsync(cancellationToken);

		AppSettings completed = new(
			current.ModelId,
			current.Hotkey,
			current.SilenceThresholdMs,
			current.FillerWordRemovalEnabled,
			current.CaptureDeviceId,
			current.AuditLogEnabled,
			setupCompleted: true,
			themePreference: current.ThemePreference,
			captureDeviceName: current.CaptureDeviceName,
			overlayMonitorDeviceName: current.OverlayMonitorDeviceName);

		await store.SaveAsync(completed, cancellationToken);
		return Mediator.Unit.Value;
	}
}
