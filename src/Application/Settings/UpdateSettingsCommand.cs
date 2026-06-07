// CQRS command to change the settings. Carries the desired new settings as a DTO; returns Unit since
// it produces no value beyond success. Validation (FluentValidation in the pipeline) runs before the
// handler, so an invalid update never reaches persistence.

using Application.Interfaces;

namespace Application.Settings;

public sealed record UpdateSettingsCommand(AppSettingsDto Settings) : ICommand<Mediator.Unit>;
