// Mapperly mapper between AppSettings (Domain) and AppSettingsDto (Application). Per the house rules
// (docs/coding-standards.md): a [Mapper] partial class with compile-time generated methods and no
// [UseMapper]. The HotkeyBinding <-> string conversion is supplied as user-implemented mapping
// methods on the same mapper, which Mapperly picks up automatically (this is not [UseMapper]
// composition). The real generated mapper is exercised in tests, never mocked.

using Domain.Settings;
using Riok.Mapperly.Abstractions;

namespace Application.Settings;

[Mapper]
public partial class SettingsMapper
{
	public partial AppSettingsDto ToDto(AppSettings settings);

	public partial AppSettings ToDomain(AppSettingsDto dto);

	// HotkeyBinding -> string for the DTO projection.
	private static string MapHotkey(HotkeyBinding hotkey) => hotkey.Chord;

	// string -> HotkeyBinding when reconstructing the domain settings (validated upstream).
	private static HotkeyBinding MapHotkey(string chord) => HotkeyBinding.Parse(chord);
}
