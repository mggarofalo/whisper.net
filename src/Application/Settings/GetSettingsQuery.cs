// CQRS query to read the current settings. A read-only request (IQuery) carrying no data; the handler
// loads through the ISettingsStore port and projects to AppSettingsDto.

using Application.Interfaces;

namespace Application.Settings;

public sealed record GetSettingsQuery : IQuery<AppSettingsDto>;
