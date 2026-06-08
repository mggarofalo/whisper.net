// The default command matcher (WHISPER-35): it recognizes nothing, so every transcript falls through
// to normal text delivery. This keeps the command-mode hook inert until a real matcher (with a command
// catalogue and execution) is implemented, so wiring the hook changes no existing dictation behavior.

using Application.Commands;
using Application.Ports;

namespace Logic.AppManagement;

public sealed class NoOpCommandMatcher : ICommandMatcher
{
	public CommandMatch Match(string transcript) => CommandMatch.None;
}
