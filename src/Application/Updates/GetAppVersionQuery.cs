// CQRS query for the running app version, formatted for display (version plus short commit hash).

using Application.Interfaces;

namespace Application.Updates;

public sealed record GetAppVersionQuery : IQuery<string>;
