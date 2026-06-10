// Drives the @WHISPER-79 hotkey-capture scenarios at the WPF-free seam. The reusable WPF control is thin
// glue verified by smoke; what matters — and what this exercises — is the capture brain
// (HotkeyCaptureInterpreter) feeding the validated HotkeyViewModel.HotkeyInput over the REAL Mediator
// pipeline and faked settings store. So it proves: a full combination is captured and displayed spaced, a
// standalone modifier is ignored, Esc clears, and an unregisterable combination is flagged by validation
// and never persisted (no live-apply).

using Application.Ports;
using AwesomeAssertions;
using Domain.Input;
using Domain.Settings;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class HotkeyCaptureDriver
{
	private const string DefaultHotkey = "Ctrl+Shift+D";

	private readonly HotkeyViewModel _hotkey;
	private readonly ISettingsStore _store;

	private AppSettings _persisted =
		new("base.en", HotkeyBinding.Parse(DefaultHotkey), silenceThresholdMs: 700, fillerWordRemovalEnabled: false);

	private string? _display;
	private bool _captured;

	public HotkeyCaptureDriver(IMediator mediator, ISettingsStore store)
	{
		_store = store;
		_hotkey = new HotkeyViewModel(mediator);

		_store.LoadAsync(Arg.Any<CancellationToken>()).Returns(_ => _persisted);
		_store.When(s => s.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()))
			.Do(call => _persisted = call.Arg<AppSettings>());
	}

	public Task LoadEditor() => _hotkey.LoadCommand.ExecuteAsync(null);

	// Simulate the control's PreviewKeyDown: interpret the press and feed the validated property exactly as
	// the WPF control would, so the capture rules and the downstream validation run for real.
	public void Press(KeyModifiers modifiers, KeyboardKey key)
	{
		switch (HotkeyCaptureInterpreter.Interpret(modifiers, key, out HotkeyBinding? binding))
		{
			case HotkeyCaptureInterpreter.CaptureAction.Commit:
				_hotkey.HotkeyInput = binding!.Chord;
				_display = binding.DisplayChord;
				_captured = true;
				break;

			case HotkeyCaptureInterpreter.CaptureAction.Clear:
				_hotkey.HotkeyInput = string.Empty;
				_display = string.Empty;
				_captured = false;
				break;

			case HotkeyCaptureInterpreter.CaptureAction.Ignore:
			default:
				break;
		}
	}

	public Task AssignCaptured() => _hotkey.AssignCommand.ExecuteAsync(null);

	public void AssertDisplay(string expected) => _display.Should().Be(expected);

	public void AssertNothingCaptured() => _captured.Should().BeFalse("a standalone modifier / cleared capture records nothing");

	public void AssertCapturedIsValid() =>
		_hotkey.GetErrors(nameof(HotkeyViewModel.HotkeyInput)).Should().BeEmpty();

	public void AssertCapturedHasError() =>
		_hotkey.GetErrors(nameof(HotkeyViewModel.HotkeyInput)).Should().NotBeEmpty();

	public void AssertNothingPersisted() =>
		_store.DidNotReceive().SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());
}
