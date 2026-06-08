// Port for matching a transcribed utterance against the user's defined voice commands (WHISPER-35).
// The dictation delivery pipeline consults it after transcription and before text delivery: a match
// routes the transcript to the command branch, no match lets it fall through to normal typing. This is
// the hook + abstraction only — a real command catalogue and execution engine live behind a future
// implementation; the default (NoOpCommandMatcher) always returns "no match", so dictation behaves
// exactly as before until commands are implemented.

using Application.Commands;

namespace Application.Ports;

public interface ICommandMatcher
{
	/// <summary>Matches <paramref name="transcript"/> against the user's commands, matched or not.</summary>
	CommandMatch Match(string transcript);
}
