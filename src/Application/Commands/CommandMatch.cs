// The result of matching a transcript against the user's voice commands. Either the
// transcript matched a command — carrying the matched command's identifier so the pipeline can route
// it — or it did not, in which case the transcript falls through to normal text delivery. This is the
// scaffolding seam only: it says *whether* a command was recognized, not how to parse or execute one
// (those are tracked separately).

namespace Application.Commands;

public sealed record CommandMatch(bool IsMatch, string? Command)
{
	/// <summary>The transcript matched no command; deliver it as text.</summary>
	public static readonly CommandMatch None = new(IsMatch: false, Command: null);

	/// <summary>The transcript matched the named command; route it to the command branch.</summary>
	public static CommandMatch For(string command) => new(IsMatch: true, Command: command);
}
