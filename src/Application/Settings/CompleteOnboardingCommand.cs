// CQRS command to mark first-run onboarding as complete: the onboarding flow dispatches it
// when the user finishes setup. The handler flips the persisted SetupCompleted flag so subsequent
// launches skip onboarding. Returns Unit.

using Application.Interfaces;

namespace Application.Settings;

public sealed record CompleteOnboardingCommand : ICommand<Mediator.Unit>;
