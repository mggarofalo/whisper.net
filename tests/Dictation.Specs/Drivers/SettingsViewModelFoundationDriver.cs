// Drives the settings view-model foundation scenarios. It resolves the shell's REAL feature section
// view-models from the scenario's DI scope (exactly as the shell composes them) and asserts the
// foundational contract M12 builds on: every settings/feature view-model is an ObservableValidator
// (so it is both INotifyPropertyChanged and INotifyDataErrorInfo — validation-capable) and its
// bindable state raises source-generated change notification. The thin WPF views that bind to these
// view-models are Presentation glue verified by smoke.

using System.ComponentModel;
using AwesomeAssertions;
using CommunityToolkit.Mvvm.ComponentModel;
using Logic.AppManagement.Shell;

namespace Dictation.Specs.Drivers;

public sealed class SettingsViewModelFoundationDriver(
	HomeViewModel home,
	ModelViewModel model,
	AudioDeviceViewModel audio,
	HotkeyViewModel hotkey,
	HistoryViewModel history,
	StatsViewModel stats)
{
	private string? _changedProperty;

	// Every section the shell can navigate to, resolved for real from the scenario scope.
	private IReadOnlyList<object> FeatureViewModels => [home, model, audio, hotkey, history, stats];

	// AC1: each feature view-model derives from ObservableValidator, so validation and the
	// instant-apply channel have one uniform, validation-capable observable base to build on.
	public void AssertEachIsValidationCapableObservable()
	{
		foreach (object viewModel in FeatureViewModels)
		{
			viewModel.Should().BeAssignableTo<ObservableValidator>(
				"every settings/feature view-model must share the validation-capable observable base");
		}
	}

	// AC2: subscribe to the real view-model, change its bindable hotkey, and capture the notification so
	// the scenario can prove the property is a source-generated [ObservableProperty] (it raises INPC).
	public void SetCurrentHotkey(string chord)
	{
		((INotifyPropertyChanged)hotkey).PropertyChanged += (_, args) => _changedProperty = args.PropertyName;
		hotkey.CurrentHotkey = chord;
	}

	public void AssertCurrentHotkeyNotified() =>
		_changedProperty.Should().Be(nameof(HotkeyViewModel.CurrentHotkey),
			"a bindable [ObservableProperty] raises source-generated change notification when it changes");
}
