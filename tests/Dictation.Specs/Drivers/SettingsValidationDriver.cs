// Drives the native-validation scenarios. It exercises the REAL HotkeyViewModel over the REAL
// Mediator pipeline (GetSettings / UpdateSettings, including the FluentValidation backstop) and the REAL
// settings mapper, faking only the settings store. The view-model validates the edited chord with
// DataAnnotations + INotifyDataErrorInfo, so the driver can assert at the view-model boundary that an
// invalid chord flags a field error and is never persisted, while a valid chord is. The WPF
// Validation.ErrorTemplate/AdornerDecorator that renders the flag is Presentation glue verified by smoke.

using Application.Ports;
using AwesomeAssertions;
using Domain.Settings;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class SettingsValidationDriver
{
	private const string DefaultHotkey = "Ctrl+Shift+D";

	private readonly HotkeyViewModel _hotkey;
	private readonly ISettingsStore _store;

	private AppSettings _persisted =
		new("base.en", HotkeyBinding.Parse(DefaultHotkey), silenceThresholdMs: 700, fillerWordRemovalEnabled: false);

	public SettingsValidationDriver(IMediator mediator, ISettingsStore store, CommunityToolkit.Mvvm.Messaging.IMessenger messenger)
	{
		_store = store;
		_hotkey = new HotkeyViewModel(mediator, messenger);

		_store.LoadAsync(Arg.Any<CancellationToken>()).Returns(_ => _persisted);
		_store.When(s => s.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()))
			.Do(call => _persisted = call.Arg<AppSettings>());
	}

	public Task LoadEditor() => _hotkey.LoadCommand.ExecuteAsync(null);

	// Type a chord into the editable field exactly as the bound TextBox would, so native validation runs.
	public void EnterHotkey(string chord) => _hotkey.HotkeyInput = chord;

	public Task SaveHotkey() => _hotkey.AssignCommand.ExecuteAsync(null);

	public void AssertFieldHasError() =>
		_hotkey.GetErrors(nameof(HotkeyViewModel.HotkeyInput)).Should().NotBeEmpty(
			"an invalid chord must surface an INotifyDataErrorInfo error that the view renders as an adorner");

	public void AssertFieldHasNoError() =>
		_hotkey.GetErrors(nameof(HotkeyViewModel.HotkeyInput)).Should().BeEmpty();

	public void AssertNothingPersisted() =>
		_store.DidNotReceive().SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());

	public void AssertBindingPersisted(string chord) =>
		_store.Received().SaveAsync(
			Arg.Is<AppSettings>(settings => settings.Hotkey == HotkeyBinding.Parse(chord)),
			Arg.Any<CancellationToken>());
}
