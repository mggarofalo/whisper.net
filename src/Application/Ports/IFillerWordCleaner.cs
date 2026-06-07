// Port for removing disfluencies (um, uh, ...) from transcribed text before delivery. The behavior
// lives in Logic.AudioManagement; this abstraction lets the handler depend on the capability.

namespace Application.Ports;

public interface IFillerWordCleaner
{
	string Clean(string text);
}
