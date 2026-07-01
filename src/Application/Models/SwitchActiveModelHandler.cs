// Handles SwitchActiveModelCommand: switches the model lifecycle to the requested model
// (releasing the currently loaded model and loading the new one) AND persists the choice as
// settings.ModelId. The persistence is essential: WhisperTranscriber resolves the model to
// load from settings.ModelId, so without saving it the user's selection would never reach transcription
// — dictation would keep loading the default model. The change is broadcast (like UpdateSettings) so the
// in-memory settings holder stays in sync and a graceful shutdown does not clobber the new value. The id
// has already passed the validator (a known catalog model) and the picker guarantees it is downloaded.

using Application.Interfaces;
using Application.Ports;
using Application.Settings;
using Domain.Settings;

namespace Application.Models;

public sealed class SwitchActiveModelHandler(
	IModelLifecycle lifecycle,
	ISettingsStore settingsStore,
	SettingsChangeChannel channel)
	: ICommandHandler<SwitchActiveModelCommand, Mediator.Unit>
{
	public async ValueTask<Mediator.Unit> Handle(SwitchActiveModelCommand command, CancellationToken cancellationToken)
	{
		// Switch the runtime lifecycle first: it loads (and warms) the model and drives the UI status. If
		// the load fails we never persist a model the transcriber could not load.
		await lifecycle.SwitchAsync(command.ModelId, cancellationToken);

		// Persist the selection so WhisperTranscriber (which loads settings.ModelId) and the next launch use
		// it. AppSettings is an immutable record with get-only members, so the updated copy is rebuilt
		// through its constructor (mirroring CompleteOnboardingHandler).
		AppSettings current = await settingsStore.LoadAsync(cancellationToken);
		if (!string.Equals(current.ModelId, command.ModelId, StringComparison.Ordinal))
		{
			// A model becoming active means first-run setup is effectively done, so mark it completed
			// — the launch flow then goes straight to the tray instead of re-prompting.
			AppSettings updated = new(
				command.ModelId,
				current.Hotkey,
				current.SilenceThresholdMs,
				current.FillerWordRemovalEnabled,
				current.CaptureDeviceId,
				current.AuditLogEnabled,
				setupCompleted: true,
					themePreference: current.ThemePreference,
					captureDeviceName: current.CaptureDeviceName,
					overlayMonitorDeviceName: current.OverlayMonitorDeviceName);

			await settingsStore.SaveAsync(updated, cancellationToken);

			// Publish the change on the instant-apply channel so running services apply it live and the
			// in-memory holder is kept current, so a graceful shutdown persists the new model rather than
			// overwriting it with a stale value.
			channel.Publish(updated);
		}

		return Mediator.Unit.Value;
	}
}
