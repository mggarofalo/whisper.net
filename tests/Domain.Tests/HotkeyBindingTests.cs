// Covers the HotkeyBinding value object: normalization to a canonical chord, the structural
// decomposition (modifier set + optional primary key) the activation logic matches against, the
// resulting order-insensitive equality, and rejection of empty / key-less bindings (the invariant the
// settings validator leans on).

using AwesomeAssertions;
using Domain;
using Domain.Input;
using Domain.Settings;
using Xunit;

namespace Domain.Tests;

public sealed class HotkeyBindingTests
{
	[Theory]
	[InlineData("ctrl+win", "Ctrl+Win")]            // pure-modifier push-to-talk chord
	[InlineData("WIN + CTRL", "Ctrl+Win")]
	[InlineData("shift+alt+space", "Shift+Alt+Space")]
	[InlineData("f13", "F13")]                       // single key, no modifier
	[InlineData("control+a", "Ctrl+A")]
	[InlineData("ctrl+shift", "Ctrl+Shift")]
	public void Parse_normalizes_to_a_canonical_chord(string raw, string expected)
	{
		HotkeyBinding.Parse(raw).Chord.Should().Be(expected);
	}

	[Theory]
	[InlineData("ctrl+alt+k", "Ctrl + Alt + K")]     // the spaced form the capture control displays
	[InlineData("f13", "F13")]
	[InlineData("ctrl+win", "Ctrl + Win")]
	public void Display_chord_spaces_the_canonical_separators(string raw, string expected)
	{
		HotkeyBinding.Parse(raw).DisplayChord.Should().Be(expected);
	}

	[Theory]
	[InlineData("ctrl+win", KeyModifiers.Control | KeyModifiers.Win, KeyboardKey.None)]   // pure-modifier
	[InlineData("ctrl+shift", KeyModifiers.Control | KeyModifiers.Shift, KeyboardKey.None)]
	[InlineData("shift+alt+space", KeyModifiers.Shift | KeyModifiers.Alt, KeyboardKey.Space)]
	[InlineData("control+a", KeyModifiers.Control, KeyboardKey.A)]
	[InlineData("ctrl+alt+d", KeyModifiers.Control | KeyModifiers.Alt, KeyboardKey.D)]
	[InlineData("f13", KeyModifiers.None, KeyboardKey.F13)]                                 // extended key, no modifier
	public void Parse_decomposes_the_chord_into_modifiers_and_a_primary_key(
		string raw, KeyModifiers expectedModifiers, KeyboardKey expectedPrimaryKey)
	{
		HotkeyBinding binding = HotkeyBinding.Parse(raw);

		binding.Modifiers.Should().Be(expectedModifiers);
		binding.PrimaryKey.Should().Be(expectedPrimaryKey);
	}

	[Fact]
	public void Modifier_order_does_not_affect_equality()
	{
		HotkeyBinding.Parse("ctrl+win").Should().Be(HotkeyBinding.Parse("win+ctrl"));
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Empty_bindings_are_rejected(string raw)
	{
		Action parsing = () => HotkeyBinding.Parse(raw);

		parsing.Should().Throw<DomainException>();
	}
}
