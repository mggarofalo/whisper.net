// The one way a shell section commits a single-field settings change. UpdateSettingsCommand
// replaces the WHOLE settings record, so a section that edits a snapshot it read earlier would write back
// every other field as it was at that moment — silently reverting a model switched, a device picked, or a
// hotkey assigned elsewhere since (WHISPER-142: changing the theme after switching models reverted the
// persisted model, so Home and Model disagreed about the active model). This re-reads the current settings
// immediately before the write and applies only the caller's change, so a commit never clobbers a field
// it does not own.

using Application.Settings;
using Mediator;

namespace Logic.AppManagement.Shell;

internal static class SettingsWriter
{
	/// <summary>Re-read the current settings, apply <paramref name="change"/>, persist, and return what was saved.</summary>
	public static async Task<AppSettingsDto> UpdateAsync(
		IMediator mediator,
		Func<AppSettingsDto, AppSettingsDto> change,
		CancellationToken cancellationToken = default)
	{
		AppSettingsDto current = await mediator.Send(new GetSettingsQuery(), cancellationToken);
		AppSettingsDto updated = change(current);
		await mediator.Send(new UpdateSettingsCommand(updated), cancellationToken);
		return updated;
	}
}
