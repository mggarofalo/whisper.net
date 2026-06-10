// Inner TDD loop for native settings validation (WHISPER-77), WPF-free. These pin the DataAnnotations +
// INotifyDataErrorInfo contract on the editable hotkey: an empty or unrecognized chord flags a field
// error and HasErrors, the Save command is gated (no UpdateSettingsCommand is dispatched while invalid),
// and a valid chord clears the error and is sent. The custom validator itself is also exercised directly.
// The outer @WHISPER-77 scenarios drive this down; the WPF adorner that renders the error is smoke-only.

using System.ComponentModel.DataAnnotations;
using Application.Settings;
using AwesomeAssertions;
using Domain.Settings;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests.Shell;

public sealed class HotkeyViewModelValidationTests
{
	private static readonly AppSettingsDto CurrentSettings = new(
		ModelId: "base.en",
		Hotkey: "Ctrl+Shift+D",
		CaptureDeviceId: "default",
		SilenceThresholdMs: 700,
		FillerWordRemovalEnabled: false);

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("Ctrl+Zorp")]
	[InlineData("Zorp")]
	public void Invalid_chord_flags_a_field_error(string chord)
	{
		HotkeyViewModel viewModel = new(Substitute.For<IMediator>(), new CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger());

		viewModel.HotkeyInput = chord;

		viewModel.GetErrors(nameof(HotkeyViewModel.HotkeyInput)).Should().NotBeEmpty();
		viewModel.HasErrors.Should().BeTrue();
	}

	[Theory]
	[InlineData("Ctrl+Shift+J")]
	[InlineData("Ctrl+Win")]
	[InlineData("F13")]
	public void Valid_chord_has_no_field_error(string chord)
	{
		HotkeyViewModel viewModel = new(Substitute.For<IMediator>(), new CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger());

		viewModel.HotkeyInput = chord;

		viewModel.GetErrors(nameof(HotkeyViewModel.HotkeyInput)).Should().BeEmpty();
		viewModel.HasErrors.Should().BeFalse();
	}

	[Fact]
	public async Task Save_is_blocked_while_the_chord_is_invalid()
	{
		IMediator mediator = Substitute.For<IMediator>();
		mediator.Send(Arg.Any<GetSettingsQuery>(), Arg.Any<CancellationToken>()).Returns(CurrentSettings);
		HotkeyViewModel viewModel = new(mediator, new CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger());
		await viewModel.LoadCommand.ExecuteAsync(null);

		viewModel.HotkeyInput = "Ctrl+Zorp";
		await viewModel.AssignCommand.ExecuteAsync(null);

		await mediator.DidNotReceive().Send(Arg.Any<UpdateSettingsCommand>(), Arg.Any<CancellationToken>());
		viewModel.Error.Should().NotBeNullOrEmpty("a blocked save surfaces the field error");
		viewModel.CurrentHotkey.Should().Be("Ctrl+Shift+D", "a rejected chord leaves the current binding unchanged");
	}

	[Fact]
	public async Task Save_dispatches_the_update_when_the_chord_is_valid()
	{
		IMediator mediator = Substitute.For<IMediator>();
		mediator.Send(Arg.Any<GetSettingsQuery>(), Arg.Any<CancellationToken>()).Returns(CurrentSettings);
		HotkeyViewModel viewModel = new(mediator, new CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger());
		await viewModel.LoadCommand.ExecuteAsync(null);

		viewModel.HotkeyInput = "Ctrl+Shift+J";
		await viewModel.AssignCommand.ExecuteAsync(null);

		await mediator.Received().Send(
			Arg.Is<UpdateSettingsCommand>(command => command.Settings.Hotkey == "Ctrl+Shift+J"),
			Arg.Any<CancellationToken>());
		viewModel.Error.Should().BeNull();
	}

	[Theory]
	[InlineData("Ctrl+Shift+J", true)]
	[InlineData("Ctrl+Win", true)]
	[InlineData("F13", true)]
	[InlineData("Ctrl+Zorp", false)]
	[InlineData("Zorp", false)]
	public void Custom_validator_accepts_only_recognized_chords(string chord, bool expectedValid)
	{
		ValidationResult? result = HotkeyViewModel.ValidateHotkey(chord, new ValidationContext(new object()));

		(result == ValidationResult.Success).Should().Be(expectedValid);
	}
}
