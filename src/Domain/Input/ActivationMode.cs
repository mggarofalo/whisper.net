// How a hotkey binding drives recording. Push-to-talk and toggle are the two activation styles the
// app supports; the same chord-matching pipeline serves both, so this enum only selects the edge
// policy (hold/release vs press/press), never a separate matching path. Lives in Domain because the
// activation logic (Logic.AppManagement) and the settings that configure it share the vocabulary.

namespace Domain.Input;

public enum ActivationMode
{
	/// <summary>Record while the chord is held; stop when it is released.</summary>
	PushToTalk,

	/// <summary>A full press starts recording; the next full press stops it.</summary>
	Toggle,
}
