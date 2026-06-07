// How transcribed text is placed into the focused field. Both paths land the same characters; they
// differ in mechanism and trade-offs, so the user (and the pipeline, per delivery) can choose between
// them. Lives in Domain so every layer — the command override, the selector, and the adapters — speaks
// the same vocabulary.

namespace Domain.Settings;

public enum DeliveryStrategy
{
	/// <summary>Type the text as Unicode keystrokes (SendInput). The universal path; works in terminals.</summary>
	Type,

	/// <summary>Place the text on the clipboard and paste it (Ctrl+V). Better for very long text.</summary>
	Paste,
}
