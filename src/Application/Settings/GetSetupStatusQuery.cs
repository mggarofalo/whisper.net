// CQRS query asked at launch to decide whether to show the settings window for first-run
// setup or go straight to the tray. A read-only request carrying no data; the handler reads settings and
// the local model cache to decide whether the app is configured.

using Application.Interfaces;

namespace Application.Settings;

public sealed record GetSetupStatusQuery : IQuery<SetupStatus>;
