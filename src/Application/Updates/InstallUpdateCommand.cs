// CQRS command to download a newer release and restart into it — sent only when the user clicks
// "Download and restart". Carries the version the user was offered and an optional percent-progress sink
// the view-model owns.

using Application.Interfaces;

namespace Application.Updates;

public sealed record InstallUpdateCommand(string Version, IProgress<int>? Progress = null)
	: ICommand<UpdateInstallResult>;
