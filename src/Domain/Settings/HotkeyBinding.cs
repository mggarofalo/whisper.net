// The global hotkey that triggers dictation, modeled as an immutable value object. Bindings are
// parsed and normalized to a canonical chord (modifiers in a fixed order, then the key) so that
// equivalent combinations compare equal — "Win+Ctrl" and "Ctrl+Win" are the same binding. An empty
// or key-less binding is rejected at construction, which is the invariant the settings validator
// relies on. Alongside the text form it exposes the chord structurally — the modifier set and the
// optional primary key — so the activation logic (Logic.AppManagement) can match it against the live
// key stream without re-parsing strings. This is the single binding model M5 builds on.

using System.Globalization;
using Domain.Input;

namespace Domain.Settings;

public sealed record HotkeyBinding
{
	// Canonical modifier ordering; the normalized chord always lists modifiers in this order.
	private static readonly string[] ModifierOrder = ["Ctrl", "Shift", "Alt", "Win"];

	// The canonical text form, e.g. "Ctrl+Shift+A". Equality is over the normalized chord (the
	// structural fields below are derived from it, so they never diverge from it).
	public string Chord { get; }

	// The modifiers the chord requires, as a side-agnostic set.
	public KeyModifiers Modifiers { get; }

	// The non-modifier key the chord triggers on, or <see cref="KeyboardKey.None"/> for a pure-modifier
	// chord (the push-to-talk "Ctrl+Win").
	public KeyboardKey PrimaryKey { get; }

	private HotkeyBinding(string chord, KeyModifiers modifiers, KeyboardKey primaryKey)
	{
		Chord = chord;
		Modifiers = modifiers;
		PrimaryKey = primaryKey;
	}

	// Builds a binding from already-decomposed parts — the path the capture-next-key helper uses to
	// turn a pressed chord into a binding without round-tripping through text. Produces the same
	// canonical form Parse would, so the two are interchangeable and compare equal. A capture with no
	// modifiers and no primary key (a bare modifier that was released) is rejected as empty.
	public static HotkeyBinding FromKeys(KeyModifiers modifiers, KeyboardKey primaryKey)
	{
		List<string> parts = [];

		if (modifiers.HasFlag(KeyModifiers.Control))
		{
			parts.Add("Ctrl");
		}

		if (modifiers.HasFlag(KeyModifiers.Shift))
		{
			parts.Add("Shift");
		}

		if (modifiers.HasFlag(KeyModifiers.Alt))
		{
			parts.Add("Alt");
		}

		if (modifiers.HasFlag(KeyModifiers.Win))
		{
			parts.Add("Win");
		}

		if (primaryKey != KeyboardKey.None)
		{
			parts.Add(KeyToken(primaryKey));
		}

		if (parts.Count == 0)
		{
			throw new DomainException("Hotkey binding must not be empty.");
		}

		return new HotkeyBinding(string.Join('+', parts), modifiers, primaryKey);
	}

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

		KeyModifiers modifierFlags = KeyModifiers.None;
		foreach (int index in modifiers)
		{
			modifierFlags |= ModifierFlag(index);
		}

		KeyboardKey primaryKey = keys.Count == 0 ? KeyboardKey.None : ParseKey(keys.First());

		return new HotkeyBinding(chord, modifierFlags, primaryKey);
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

	// The modifier flag for a canonical-order index (0=Ctrl, 1=Shift, 2=Alt, 3=Win).
	private static KeyModifiers ModifierFlag(int orderIndex) => orderIndex switch
	{
		0 => KeyModifiers.Control,
		1 => KeyModifiers.Shift,
		2 => KeyModifiers.Alt,
		_ => KeyModifiers.Win,
	};

	// Maps a canonical key token to its domain key. Bare digits become D0–D9 (matching the listener's
	// translation); anything the domain does not model becomes Unknown, which simply never matches.
	private static KeyboardKey ParseKey(string canonical)
	{
		if ((canonical.Length == 1) && char.IsAsciiDigit(canonical[0]))
		{
			return Enum.Parse<KeyboardKey>($"D{canonical}");
		}

		return Enum.TryParse(canonical, out KeyboardKey key) ? key : KeyboardKey.Unknown;
	}

	// The inverse of ParseKey: the canonical token for a primary key, so FromKeys yields the same chord
	// text Parse would. Digits D0–D9 collapse back to "0"–"9"; every other key uses its enum name.
	private static string KeyToken(KeyboardKey key) =>
		key is >= KeyboardKey.D0 and <= KeyboardKey.D9
			? (key - KeyboardKey.D0).ToString(CultureInfo.InvariantCulture)
			: key.ToString();
}
