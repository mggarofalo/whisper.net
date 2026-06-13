// Unit depth for the activation lifecycle, beyond the acceptance scenarios.
// Pins the contract on the reference recipient (HotkeyViewModel over a real WeakReferenceMessenger):
// the registration exists exactly while the section is active, an inactive cached view-model gets no
// callbacks, double transitions are idempotent, and — the leak half — a registered, never-deactivated
// view-model is still garbage-collectable, because the weak messenger cannot root it. Activation also
// triggers the settings load, pinned here so the section can never again show no binding
// while AssignAsync silently no-ops on its null-settings guard.

using Application.Settings;
using AwesomeAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Domain.Settings;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests.Shell;

public sealed class FeatureViewModelLifecycleTests
{
	private const string LoadedChord = "Ctrl+Shift+D";

	private readonly WeakReferenceMessenger _messenger = new();
	private readonly HotkeyViewModel _viewModel;

	public FeatureViewModelLifecycleTests() =>
		_viewModel = new HotkeyViewModel(MediatorReturningSettings(), _messenger);

	private static SettingsChangedMessage ChangeTo(string chord) =>
		new(new AppSettings("base.en", HotkeyBinding.Parse(chord), silenceThresholdMs: 700, fillerWordRemovalEnabled: false));

	// Activation triggers the settings load, so the mediator substitute must serve a valid
	// DTO — a bare substitute would fault the activation-triggered load with a null result.
	private static IMediator MediatorReturningSettings()
	{
		IMediator mediator = Substitute.For<IMediator>();
		mediator.Send(Arg.Any<GetSettingsQuery>(), Arg.Any<CancellationToken>()).Returns(new AppSettingsDto(
			ModelId: "base.en",
			Hotkey: LoadedChord,
			SilenceThresholdMs: 700,
			FillerWordRemovalEnabled: false));
		return mediator;
	}

	[Fact]
	public void Registers_on_activate_and_unregisters_on_deactivate()
	{
		_messenger.IsRegistered<SettingsChangedMessage>(_viewModel).Should().BeFalse("a fresh view-model is dormant");

		_viewModel.OnNavigatedTo();
		_messenger.IsRegistered<SettingsChangedMessage>(_viewModel).Should().BeTrue();

		_viewModel.OnNavigatedFrom();
		_messenger.IsRegistered<SettingsChangedMessage>(_viewModel).Should().BeFalse();
	}

	[Fact]
	public async Task Activation_triggers_the_load_and_seeds_the_current_binding()
	{
		_viewModel.CurrentHotkey.Should().BeNull("a fresh view-model has loaded nothing yet");

		_viewModel.OnNavigatedTo();
		await (_viewModel.LoadCommand.ExecutionTask ?? Task.CompletedTask);

		_viewModel.CurrentHotkey.Should().Be(LoadedChord,
			"activation must load the persisted binding so the section never shows it as unset");
		_viewModel.HotkeyInput.Should().Be(LoadedChord,
			"the activation-triggered load seeds the editable chord with the current binding");
	}

	[Fact]
	public void Inactive_cached_view_model_gets_no_callbacks()
	{
		_viewModel.OnNavigatedTo();
		_viewModel.OnNavigatedFrom();

		_messenger.Send(ChangeTo("Ctrl+Alt+Z"));

		_viewModel.CurrentHotkey.Should().Be(LoadedChord,
			"an inactive cached view-model must not react (it still shows what activation loaded)");
	}

	[Fact]
	public void Active_view_model_reflects_a_published_change()
	{
		_viewModel.OnNavigatedTo();

		_messenger.Send(ChangeTo("Ctrl+Alt+Z"));

		_viewModel.CurrentHotkey.Should().Be("Ctrl+Alt+Z");
	}

	[Fact]
	public void Repeated_transitions_are_idempotent()
	{
		_viewModel.OnNavigatedTo();
		_viewModel.OnNavigatedTo();
		_messenger.IsRegistered<SettingsChangedMessage>(_viewModel).Should().BeTrue();

		_viewModel.OnNavigatedFrom();
		_viewModel.OnNavigatedFrom();
		_messenger.IsRegistered<SettingsChangedMessage>(_viewModel).Should().BeFalse();
	}

	[Fact]
	public void Registered_view_model_is_still_collectable()
	{
		WeakReference reference = CreateActivatedAndDrop(_messenger);

		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		reference.IsAlive.Should().BeFalse(
			"the WeakReferenceMessenger must not root a registered view-model");
	}

	// Kept non-inlined so the only strong reference dies with the method frame.
	[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
	private static WeakReference CreateActivatedAndDrop(IMessenger messenger)
	{
		HotkeyViewModel viewModel = new(MediatorReturningSettings(), messenger);
		viewModel.OnNavigatedTo();
		return new WeakReference(viewModel);
	}
}
