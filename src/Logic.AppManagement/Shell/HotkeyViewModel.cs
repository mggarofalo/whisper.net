// The shell's hotkey section (WHISPER-33; native validation in WHISPER-77): shows the current dictation
// hotkey and assigns a new one. It depends on nothing but IMediator — it loads via GetSettingsQuery and
// saves via UpdateSettingsCommand, carrying the whole settings DTO with the hotkey swapped. The chord the
// user is editing lives in a validated [ObservableProperty] (HotkeyInput) decorated with DataAnnotations
// and [NotifyDataErrorInfo], so an invalid chord surfaces natively (the view renders an adorner) and the
// Save is gated behind HasErrors — an invalid binding is never sent to Mediator, let alone persisted. The
// server-side FluentValidation rule remains the backstop. Built on CommunityToolkit.Mvvm ObservableValidator
// and WPF-free so the behavior is driven for real in specs; the thin view binds to it.

using System.ComponentModel.DataAnnotations;
using Application.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Domain;
using Domain.Input;
using Domain.Settings;
using Mediator;

namespace Logic.AppManagement.Shell;

public sealed partial class HotkeyViewModel : FeatureViewModel
{
	private readonly IMediator _mediator;
	private readonly IMessenger _messenger;

	private AppSettingsDto? _settings;

	public HotkeyViewModel(IMediator mediator, IMessenger messenger)
	{
		_mediator = mediator;
		_messenger = messenger;
	}

	/// <summary>The current, persisted dictation hotkey chord (e.g. "Ctrl+Shift+D").</summary>
	[ObservableProperty]
	private string? _currentHotkey;

	/// <summary>The chord being edited. Validated natively: an empty or unrecognized chord flags an error,
	/// which surfaces as an adorner in the view and blocks the Save command.</summary>
	[ObservableProperty]
	[NotifyDataErrorInfo]
	[NotifyCanExecuteChangedFor(nameof(AssignCommand))]
	[Required(ErrorMessage = "A hotkey is required.")]
	[CustomValidation(typeof(HotkeyViewModel), nameof(ValidateHotkey))]
	private string? _hotkeyInput;

	/// <summary>A validation error from the last assignment attempt, or null when the last attempt succeeded.</summary>
	[ObservableProperty]
	private string? _error;

	// Live subscriptions exist only while this section is the shell's active content (WHISPER-94): the
	// instant-apply registration is added on activate and removed on deactivate, so an inactive cached
	// section gets no callbacks. The messenger is the shared WeakReferenceMessenger, so even a missed
	// deactivation could never root this cached view-model. Activation also triggers the load
	// (WHISPER-109): without it the section showed no binding and AssignAsync silently no-opped on its
	// null-settings guard, so assignment never persisted.
	protected override void OnActivated()
	{
		_messenger.Register<HotkeyViewModel, SettingsChangedMessage>(
			this, static (recipient, message) => recipient.OnSettingsChanged(message));
		LoadCommand.Execute(null);
	}

	protected override void OnDeactivated() => _messenger.Unregister<SettingsChangedMessage>(this);

	// A live settings commit (instant apply, WHISPER-78) refreshes the displayed binding while active —
	// e.g. a hotkey assigned elsewhere shows here without a reload.
	private void OnSettingsChanged(SettingsChangedMessage message)
	{
		string chord = message.Value.Hotkey.Chord;
		CurrentHotkey = chord;

		if (_settings is not null)
		{
			_settings = _settings with { Hotkey = chord };
		}
	}

	// Load the persisted hotkey through Mediator, seeding the editable field with the current binding.
	[RelayCommand]
	private async Task LoadAsync(CancellationToken cancellationToken)
	{
		_settings = await _mediator.Send(new GetSettingsQuery(), cancellationToken);
		CurrentHotkey = _settings.Hotkey;
		HotkeyInput = _settings.Hotkey;
		Error = null;
	}

	// Assign the edited chord. Validation is gated client-side: an invalid chord (empty or unrecognized)
	// is refused here — no UpdateSettingsCommand is dispatched and nothing is persisted — and the error is
	// surfaced. A chord param is accepted for programmatic/test callers; the view binds HotkeyInput directly.
	[RelayCommand(CanExecute = nameof(CanAssign))]
	private async Task AssignAsync(string? chord, CancellationToken cancellationToken)
	{
		if (chord is not null)
		{
			HotkeyInput = chord;
		}

		ValidateAllProperties();

		if (HasErrors)
		{
			Error = GetErrors(nameof(HotkeyInput)).FirstOrDefault()?.ErrorMessage ?? "Invalid hotkey.";
			return;
		}

		if (_settings is null)
		{
			return;
		}

		try
		{
			await _mediator.Send(new UpdateSettingsCommand(_settings with { Hotkey = HotkeyInput! }), cancellationToken);
			_settings = _settings with { Hotkey = HotkeyInput! };
			CurrentHotkey = HotkeyInput;
			Error = null;
		}
		catch (FluentValidation.ValidationException exception)
		{
			Error = exception.Errors.FirstOrDefault()?.ErrorMessage ?? "Invalid hotkey.";
		}
	}

	// The Save button is disabled while the edited chord is invalid; the in-body guard is the real gate.
	private bool CanAssign(string? chord) => !HasErrors;

	// DataAnnotations custom rule (WPF-free): a chord is valid when it parses to a binding whose primary key
	// is recognized. Emptiness is left to [Required]; a pure-modifier chord (push-to-talk "Ctrl+Win") and a
	// lone function key ("F13") are valid, but an unrecognized token ("Ctrl+Zorp") is rejected.
	public static ValidationResult? ValidateHotkey(string? chord, ValidationContext context)
	{
		if (string.IsNullOrWhiteSpace(chord))
		{
			return ValidationResult.Success;
		}

		HotkeyBinding binding;
		try
		{
			binding = HotkeyBinding.Parse(chord);
		}
		catch (DomainException)
		{
			return new ValidationResult($"'{chord}' is not a valid hotkey combination.");
		}

		return binding.PrimaryKey == KeyboardKey.Unknown
			? new ValidationResult($"'{chord}' contains a key that is not recognized.")
			: ValidationResult.Success;
	}
}
