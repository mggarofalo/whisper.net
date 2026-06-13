// Drives the global-hotkey-listening scenarios. It owns HOW the listener is exercised so
// the step definitions stay one-liners: it starts/disposes the REAL EventLoopHotkeyListener through
// the IHotkeyListener port, feeds raw key codes in via the fake hook, records the domain key edges the
// port raises, and asserts at that boundary (translated key + modifier snapshot). The chord parsing
// here is test scaffolding only — production chord matching lives in Logic.AppManagement.

using Application.Ports;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Domain.Input;
using SharpHook.Data;

namespace Dictation.Specs.Drivers;

public sealed class HotkeyListenerDriver
{
	private readonly IHotkeyListener _listener;
	private readonly FakeGlobalKeyHook _hook;
	private readonly List<KeyboardKeyEventArgs> _downs = [];
	private readonly List<KeyboardKeyEventArgs> _ups = [];

	public HotkeyListenerDriver(IHotkeyListener listener, FakeGlobalKeyHook hook)
	{
		_listener = listener;
		_hook = hook;
		_listener.KeyDown += (_, e) => _downs.Add(e);
		_listener.KeyUp += (_, e) => _ups.Add(e);
	}

	public void Start() => _listener.Start();

	public void PressChord(string chord)
	{
		foreach (string token in chord.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			_hook.Press(ToCode(token));
		}
	}

	public void PressKey(string key) => _hook.Press(ToCode(key));

	public void ReleaseKey(string key) => _hook.Release(ToCode(key));

	public void Dispose() => ((IDisposable)_listener).Dispose();

	// --- assertions (port boundary) ---

	public void AssertKeyDown(string key, string modifiers)
	{
		_downs.Should().NotBeEmpty();
		KeyboardKeyEventArgs last = _downs[^1];
		last.Key.Should().Be(ParseKey(key));
		last.Modifiers.Should().Be(ParseModifiers(modifiers));
	}

	public void AssertKeyUp(string key, string modifiers)
	{
		_ups.Should().NotBeEmpty();
		KeyboardKeyEventArgs last = _ups[^1];
		last.Key.Should().Be(ParseKey(key));
		last.Modifiers.Should().Be(ParseModifiers(modifiers));
	}

	public void AssertHookStopped() => _hook.IsRunning.Should().BeFalse();

	public void AssertNoEventsFrom(Action stray)
	{
		int before = _downs.Count + _ups.Count;
		stray();
		(_downs.Count + _ups.Count).Should().Be(before);
	}

	public void ProduceStrayKey() => AssertNoEventsFrom(() => _hook.Press(KeyCode.VcA));

	// --- test-only parsing (raw codes in, domain values out) ---

	private static KeyCode ToCode(string token) => token.ToLowerInvariant() switch
	{
		"ctrl" or "control" => KeyCode.VcLeftControl,
		"shift" => KeyCode.VcLeftShift,
		"alt" => KeyCode.VcLeftAlt,
		"win" => KeyCode.VcLeftMeta,
		"esc" or "escape" => KeyCode.VcEscape,
		_ => Enum.Parse<KeyCode>($"Vc{token}", ignoreCase: true),
	};

	private static KeyboardKey ParseKey(string name) => name.ToLowerInvariant() switch
	{
		"ctrl" or "control" => KeyboardKey.Control,
		"shift" => KeyboardKey.Shift,
		"alt" => KeyboardKey.Alt,
		"win" => KeyboardKey.Win,
		_ => Enum.Parse<KeyboardKey>(name, ignoreCase: true),
	};

	private static KeyModifiers ParseModifiers(string text)
	{
		KeyModifiers modifiers = KeyModifiers.None;
		foreach (string token in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			modifiers |= token.ToLowerInvariant() switch
			{
				"none" => KeyModifiers.None,
				"ctrl" or "control" => KeyModifiers.Control,
				"shift" => KeyModifiers.Shift,
				"alt" => KeyModifiers.Alt,
				"win" => KeyModifiers.Win,
				_ => throw new FormatException($"Unknown modifier '{token}'."),
			};
		}

		return modifiers;
	}
}
