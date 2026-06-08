// Drives the @WHISPER-33 audio-device + hotkey configuration scenarios. It owns HOW the two settings
// view-models are exercised so the steps stay one-liners: it builds the REAL AudioDeviceViewModel and
// HotkeyViewModel over the REAL Mediator pipeline (ListCaptureDevices / GetSettings / UpdateSettings,
// including the FluentValidation behavior) and the REAL settings mapper, faking only the device
// enumerator and the settings store. The store substitute round-trips a save back into the next load,
// so a change can be shown to persist across a reload of the view. The thin WPF views that bind to the
// view-models are Presentation glue verified by smoke.

using Application.Ports;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Domain.Audio;
using Domain.Settings;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class AudioHotkeyConfigDriver
{
	private const string DefaultHotkey = "Ctrl+Shift+D";

	private readonly AudioDeviceViewModel _audio;
	private readonly HotkeyViewModel _hotkey;
	private readonly FakeAudioDeviceEnumerator _enumerator;
	private readonly ISettingsStore _store;

	private AppSettings _persisted =
		new("base.en", HotkeyBinding.Parse(DefaultHotkey), silenceThresholdMs: 700, fillerWordRemovalEnabled: false);

	public AudioHotkeyConfigDriver(IMediator mediator, FakeAudioDeviceEnumerator enumerator, ISettingsStore store)
	{
		_enumerator = enumerator;
		_store = store;
		_audio = new AudioDeviceViewModel(mediator);
		_hotkey = new HotkeyViewModel(mediator);

		// The store starts holding the persisted settings, and a save round-trips into the next load so a
		// change can be shown to persist across a reload.
		_store.LoadAsync(Arg.Any<CancellationToken>()).Returns(_ => _persisted);
		_store.When(s => s.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()))
			.Do(call => _persisted = call.Arg<AppSettings>());
	}

	// --- given ---

	public void DevicesAvailable(string first, string second) =>
		_enumerator.Configure([new AudioDevice(first, first), new AudioDevice(second, second)], first);

	// --- audio device flow ---

	public Task LoadAudio() => _audio.LoadCommand.ExecuteAsync(null);

	public Task SelectDevice(string id) => _audio.SelectCommand.ExecuteAsync(id);

	public Task ReloadAudio() => _audio.LoadCommand.ExecuteAsync(null);

	public void AssertDevicesListed(string first, string second)
	{
		_audio.Devices.Select(device => device.Id).Should().Contain([first, second]);
		_audio.SelectedDeviceId.Should().NotBeNull();
	}

	public void AssertSelectedDeviceIs(string id) => _audio.SelectedDeviceId.Should().Be(id);

	// --- hotkey flow ---

	public Task LoadHotkey() => _hotkey.LoadCommand.ExecuteAsync(null);

	public Task AssignHotkey(string chord) => _hotkey.AssignCommand.ExecuteAsync(chord);

	// After a reload the binding is the store's canonical chord, so compare against the canonical form of
	// the assigned chord rather than the raw input.
	public void AssertCurrentHotkeyIs(string chord) =>
		_hotkey.CurrentHotkey.Should().Be(HotkeyBinding.Parse(chord).Chord);

	public void AssertUpdatePersisted() =>
		_store.Received().SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());

	public void AssertHotkeyRejected(string previous)
	{
		_hotkey.Error.Should().NotBeNullOrEmpty("an invalid hotkey should surface a validation error");
		_hotkey.CurrentHotkey.Should().Be(HotkeyBinding.Parse(previous).Chord, "a rejected hotkey leaves the current binding unchanged");
	}

	public void AssertNothingPersisted() =>
		_store.DidNotReceive().SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());
}
