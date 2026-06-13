// The Driver owns HOW keystroke delivery is exercised, so the steps stay one-liners that
// only describe WHAT. It drives the REAL SendInputTextInjector over a recording fake keyboard — the
// same pattern the capture specs use (real adapter logic, fake OS seam) — and asserts at the boundary
// that the focused field receives the exact characters. Because delivery is by typing, not paste, the
// "terminal that ignores paste" case needs no special handling: it succeeds like any other window.

using Application.Ports;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Infrastructure.TextDelivery;

namespace Dictation.Specs.Drivers;

public sealed class TextInjectionDriver
{
	private readonly FakeKeyboardInput _keyboard = new();
	private readonly ITextInjector _injector;
	private string _pending = string.Empty;

	public TextInjectionDriver() => _injector = new SendInputTextInjector(_keyboard);

	public void ResultIsReady(string text) => _pending = text;

	public void DeliverResult() => _injector.Inject(_pending);

	public void AssertFocusedFieldReceived(string expected) =>
		_keyboard.ReconstructTypedText().Should().Be(expected);
}
