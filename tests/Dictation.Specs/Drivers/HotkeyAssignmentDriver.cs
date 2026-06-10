// Drives the @WHISPER-109 hotkey-assignment scenarios. The defect: navigating to the hotkey section
// activated it (messenger registration) but never loaded the persisted settings, so the binding label
// stayed empty and AssignAsync silently returned on its null-settings guard — assignment was a no-op.
// This driver therefore enters through the REAL lifecycle entry point — vm.OnNavigatedTo(), exactly as
// the shell's navigation service activates a section — never LoadCommand directly. It composes the REAL
// HotkeyViewModel, the REAL HotkeyConfigurationHostedService over the REAL HotkeyActivationController,
// and the REAL Mediator pipeline (GetSettings / UpdateSettings, whose handler publishes on the
// instant-apply channel), faking only the settings store with a round-trip so a save is visible to the
// next load — including the "next launch", simulated by a fresh controller + hosted service over the
// same store.

using Application.Ports;
using AwesomeAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Domain.Settings;
using Logic.AppManagement;
using Logic.AppManagement.Lifecycle;
using Logic.AppManagement.Shell;
using Mediator;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class HotkeyAssignmentDriver
{
	private readonly IMessenger _messenger;
	private readonly ISettingsStore _store;
	private readonly HotkeyViewModel _viewModel;

	// The "running app": the live controller and the hosted service that configures it. A relaunch
	// replaces both with fresh instances over the same persisted store, exactly like a process restart.
	private HotkeyActivationController _controller = new();
	private HotkeyConfigurationHostedService? _service;

	private AppSettings _persisted = AppSettings.Default;

	public HotkeyAssignmentDriver(IMediator mediator, ISettingsStore store, IMessenger messenger)
	{
		_store = store;
		_messenger = messenger;
		_viewModel = new HotkeyViewModel(mediator, messenger);

		// Round-trip the store so a save is visible to the next load (and to the next "launch").
		_store.LoadAsync(Arg.Any<CancellationToken>()).Returns(_ => _persisted);
		_store.When(s => s.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()))
			.Do(call => _persisted = call.Arg<AppSettings>());
	}

	// --- given ---

	public void PersistSettingsWithHotkey(string chord) =>
		_persisted = new AppSettings(
			_persisted.ModelId,
			HotkeyBinding.Parse(chord),
			_persisted.SilenceThresholdMs,
			_persisted.FillerWordRemovalEnabled,
			_persisted.CaptureDeviceId,
			_persisted.AuditLogEnabled,
			_persisted.SetupCompleted);

	public async Task StartPipeline()
	{
		_service = new HotkeyConfigurationHostedService(
			_controller, _store, _messenger, NullLogger<HotkeyConfigurationHostedService>.Instance);
		await _service.StartAsync(CancellationToken.None);
	}

	// --- when ---

	// Enter through the REAL navigation lifecycle, not LoadCommand: OnNavigatedTo is what the shell calls
	// when the section becomes active, and the load it must trigger is exactly what WHISPER-109 fixes.
	// Awaiting the command's ExecutionTask (null until activation actually starts a load) keeps the
	// scenario deterministic without bypassing the entry point.
	public async Task OpenHotkeySection()
	{
		_viewModel.OnNavigatedTo();
		await (_viewModel.LoadCommand.ExecutionTask ?? Task.CompletedTask);
	}

	// Capture commits the chord into the validated input (what the capture control does on a full press),
	// then Assign dispatches it with no parameter — the same two-step flow the view drives.
	public async Task CaptureAndAssign(string chord)
	{
		_viewModel.HotkeyInput = chord;
		await _viewModel.AssignCommand.ExecuteAsync(null);
	}

	// A process restart: a fresh controller and hosted service start over the same persisted store.
	public async Task RelaunchApplication()
	{
		_controller = new HotkeyActivationController();
		await StartPipeline();
	}

	// --- then ---

	public void AssertCurrentBindingShown(string chord) =>
		_viewModel.CurrentHotkey.Should().Be(HotkeyBinding.Parse(chord).Chord,
			"activating the section must load and show the persisted binding");

	public void AssertMatcherBoundTo(string chord) =>
		_controller.Binding.Chord.Should().Be(HotkeyBinding.Parse(chord).Chord);

	public void AssertMatcherNotBoundTo(string chord) =>
		_controller.Binding.Chord.Should().NotBe(HotkeyBinding.Parse(chord).Chord);

	public void AssertPersistedHotkeyIs(string chord) =>
		_persisted.Hotkey.Chord.Should().Be(HotkeyBinding.Parse(chord).Chord);
}
