// CQRS query for a user-initiated update check — sent only when the user clicks "Check for updates", so
// the network request it makes is always the user's explicit action.

using Application.Interfaces;

namespace Application.Updates;

public sealed record CheckForUpdateQuery : IQuery<UpdateCheckResult>;
