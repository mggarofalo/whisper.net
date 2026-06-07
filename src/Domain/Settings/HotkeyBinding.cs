// The global hotkey that triggers dictation, modeled as an immutable value object. Bindings are
// parsed and normalized to a canonical chord (modifiers in a fixed order, then the key) so that
// equivalent combinations compare equal — "Win+Ctrl" and "Ctrl+Win" are the same binding. An empty
// or key-less binding is rejected at construction, which is the invariant the settings validator
// relies on.

namespace Domain.Settings;

public sealed record HotkeyBinding
{
	// Canonical modifier ordering; the normalized chord always lists modifiers in this order.
	private static readonly string[] ModifierOrder = ["Ctrl", "Shift", "Alt", "Win"];

	// The canonical text form, e.g. "Ctrl+Shift+A". Equality is over this single normalized value.
	public string Chord { get; }

	private HotkeyBinding(string chord) => Chord = chord;

	// Parses a free-form chord ("ctrl+win", "F13", "Shift + Alt + Space") into a normalized binding.
	// A chord may be pure modifiers (the push-to-talk "Ctrl+Win"), a single key ("F13"), or a mix;
	// the only rule is that it cannot be empty.
	public static HotkeyBinding Parse(string raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			throw new DomainException("Hotkey binding must not be empty.");
		}

		string[] tokens = raw.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		if (tokens.Length == 0)
		{
			throw new DomainException("Hotkey binding must not be empty.");
		}

		// Modifiers are emitted first in canonical order; any remaining keys follow alphabetically.
		// Both sets dedupe, so equivalent chords ("win+ctrl" / "ctrl+win") normalize identically.
		SortedSet<int> modifiers = [];
		SortedSet<string> keys = [];

		foreach (string token in tokens)
		{
			string canonical = Canonicalize(token);
			int modifierIndex = Array.IndexOf(ModifierOrder, canonical);

			if (modifierIndex >= 0)
			{
				modifiers.Add(modifierIndex);
			}
			else
			{
				keys.Add(canonical);
			}
		}

		string chord = string.Join('+', modifiers.Select(i => ModifierOrder[i]).Concat(keys));
		return new HotkeyBinding(chord);
	}

	public override string ToString() => Chord;

	// Maps modifier aliases to their canonical name; non-modifier tokens become a title-cased key.
	private static string Canonicalize(string token) => token.ToLowerInvariant() switch
	{
		"ctrl" or "control" => "Ctrl",
		"shift" => "Shift",
		"alt" => "Alt",
		"win" or "super" or "meta" or "cmd" => "Win",
		_ => token.Length == 1 ? token.ToUpperInvariant() : char.ToUpperInvariant(token[0]) + token[1..].ToLowerInvariant(),
	};
}
