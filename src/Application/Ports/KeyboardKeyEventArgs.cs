// The payload of a key edge from IHotkeyListener: which key moved and the modifier set in effect at
// that instant. For a modifier-key edge the modifier it controls is already reflected in Modifiers
// (down → included, up → excluded), so a subscriber sees a consistent snapshot without tracking state
// itself.

using Domain.Input;

namespace Application.Ports;

public sealed record KeyboardKeyEventArgs(KeyboardKey Key, KeyModifiers Modifiers);
