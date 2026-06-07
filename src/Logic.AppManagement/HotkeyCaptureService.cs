// "Capture next key" + rebinding for the hotkey settings flow. BeginCapture enters a one-shot listen
// mode; the first complete chord the user presses — zero-or-more modifiers followed by a primary key,
// including F13/extended keys — resolves to a HotkeyBinding, which is applied atomically to the
// HotkeyActivationController so the new chord triggers immediately and the old one stops. Releasing a
// bare modifier (no primary key) is rejected and Esc cancels; both leave the current binding intact.
// The controller stays the single binding model, so a rebind is just one Configure call.

using Domain.Input;
using Domain.Settings;

namespace Logic.AppManagement;

public sealed class HotkeyCaptureService(HotkeyActivationController controller)
{
	private bool _capturing;

	/// <summary>Raised when capture resolves to a valid binding (already applied to the controller).</summary>
	public event EventHandler<HotkeyBinding>? CaptureCompleted;

	/// <summary>Raised when capture ends without a valid chord (e.g. a bare modifier); binding unchanged.</summary>
	public event EventHandler? CaptureRejected;

	/// <summary>Raised when capture is cancelled (Esc); binding unchanged.</summary>
	public event EventHandler? CaptureCancelled;

	public bool IsCapturing => _capturing;

	// Enter one-shot listen mode. The next complete chord (or its rejection/cancellation) ends it.
	public void BeginCapture() => _capturing = true;

	public void HandleKeyDown(KeyboardKey key, KeyModifiers modifiers)
	{
		if (!_capturing)
		{
			return;
		}

		// Esc cancels the capture, leaving the current binding untouched.
		if (key == KeyboardKey.Escape)
		{
			_capturing = false;
			CaptureCancelled?.Invoke(this, EventArgs.Empty);
			return;
		}

		// A modifier alone never completes a chord — wait for the primary key (its flag is already in
		// the live modifier set the event carries).
		if (key.AsModifier() != KeyModifiers.None)
		{
			return;
		}

		// A non-modifier key completes the chord. Apply it atomically; one Configure swaps the binding
		// so the old chord stops triggering and the new one takes effect immediately.
		HotkeyBinding binding = HotkeyBinding.FromKeys(modifiers, key);
		_capturing = false;
		controller.Configure(binding, controller.Mode);
		CaptureCompleted?.Invoke(this, binding);
	}

	public void HandleKeyUp(KeyboardKey key, KeyModifiers modifiers)
	{
		if (!_capturing)
		{
			return;
		}

		// Everything released without a primary key ever arriving: the user pressed only modifiers.
		// Reject and keep the current binding.
		if (modifiers == KeyModifiers.None)
		{
			_capturing = false;
			CaptureRejected?.Invoke(this, EventArgs.Empty);
		}
	}
}
