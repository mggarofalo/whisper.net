// An in-memory stand-in for the OS startup registration (the IStartupRegistration port).
// Models the single source of truth a real registry Run key provides:
// enabling/disabling flips one flag, so it is inherently idempotent (no duplicates, no orphans) — the
// real registry idempotency is proven separately against an actual HKCU subkey in Infrastructure.Tests.
//
// It also models the drift a real Run key can hold: a registration written by an earlier install, whose
// command names an executable that is no longer there. Scenarios set that up through SetRegisteredCommand
// so the healing and diagnostic behavior can be driven without touching the machine's startup list.

using Application.Ports;

namespace Dictation.Specs.Support;

public sealed class FakeStartupRegistration : IStartupRegistration
{
	private string? _registeredCommand;

	public string ExpectedCommand { get; set; } = "\"C:\\Apps\\Whisper\\Presentation.exe\"";

	public string? RegisteredCommand => _registeredCommand;

	public bool RegisteredTargetExists { get; private set; }

	public bool IsEnabled() => _registeredCommand is not null;

	public void Enable()
	{
		_registeredCommand = ExpectedCommand;
		RegisteredTargetExists = true;
	}

	public void Disable()
	{
		_registeredCommand = null;
		RegisteredTargetExists = false;
	}

	// Test seam: set the starting registration state for a scenario's Given.
	public void SetInitialState(bool enabled)
	{
		if (enabled)
		{
			Enable();
		}
		else
		{
			Disable();
		}
	}

	// Test seam: register a specific command, optionally one whose target no longer exists — the stale
	// entry an install that moved or was removed leaves behind.
	public void SetRegisteredCommand(string command, bool targetExists)
	{
		_registeredCommand = command;
		RegisteredTargetExists = targetExists;
	}
}
