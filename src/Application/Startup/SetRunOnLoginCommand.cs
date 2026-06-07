// CQRS command to enable or disable launching the app at user login (WHISPER-32). Carries the desired
// state; returns Unit since it produces no value beyond success. The view-model toggle (settings UI)
// sends this command; the handler applies it through the IStartupRegistration port.

using Application.Interfaces;

namespace Application.Startup;

public sealed record SetRunOnLoginCommand(bool Enabled) : ICommand<Mediator.Unit>;
