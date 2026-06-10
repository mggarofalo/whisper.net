// A scenario-scoped, mutable holder behind the AudioBufferingOptions resolution (the DeliveryOptions
// scoped-override pattern, adapted for an immutable record): a Given can replace the options BEFORE
// the orchestrator — which captures them at construction — is first resolved from the scope. The
// long-dictation scenarios (WHISPER-111) shrink the soft limit this way; every other scenario never
// touches the holder and sees the production defaults, exactly as the singleton registration did.

using Logic.AudioManagement;

namespace Dictation.Specs.Support;

public sealed class ScenarioAudioBufferingOptions
{
	public AudioBufferingOptions Options { get; set; } = new();
}
