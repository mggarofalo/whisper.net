// Port for removing disfluencies (um, uh, ...) from transcribed text before delivery. The behavior
// lives in Logic.AudioManagement; this abstraction lets the handler depend on the capability.

namespace Application.Ports;

public interface IFillerWordCleaner
{
	/// <summary>
	/// Normalizes raw transcribed text: bracketed/parenthesized noise labels are always stripped, and
	/// spoken filler words are removed only when <paramref name="removeFillerWords"/> is set. Pure and
	/// idempotent — running it twice yields the same result.
	/// </summary>
	string Clean(string text, bool removeFillerWords = true);
}
