# Handoff: Overlay cluster — WHISPER-101 → 100 → 102

Work in this order; **102 strictly last** (it consumes WHISPER-111's limit messages and must not
change the overlay footprint that 100 just positioned). All three touch the overlay
(`src/Logic.AppManagement/Overlay/LevelOverlayViewModel.cs` + controller, and the overlay window
XAML in `src/Presentation`), so strictly sequential.

Fetch full descriptions/ACs from Plane (UUIDs in `README.md`).

## WHISPER-101 — Overlay level meter: perceptual (dB) mapping instead of raw RMS

- The meter currently maps raw RMS linearly, so normal speech barely moves it. Fix: dBFS mapping —
  `db = 20 * log10(rms)` (guard rms ≤ 0 → floor), then normalize a usable speech window
  (e.g., −60 dB floor … 0 dB ceiling) to 0..1 for the meter.
- This is pure math in `Logic.AppManagement` (the meter level lives on `LevelOverlayViewModel`,
  fed from audio frames — find where RMS is computed today and replace the mapping at that seam).
  Fully unit-testable: pin the floor (silence → 0), a known sine RMS → expected normalized value,
  the ceiling clamp, and monotonicity. `@WHISPER-101` scenarios if the mapping is observable
  through a driver; otherwise unit tests + recorded spec exception in the PR.
- Watch: the existing meter tests/specs that pin the current linear behavior — update them
  intentionally (that's the mandated change), same as 112 did for the trimmer pins.
- Branch: `fix/whisper-101-meter-db-mapping`.

## WHISPER-100 — Overlay at bottom-center of the work area

- Position the dictation overlay bottom-center of the **work area** (excludes the taskbar —
  `SystemParameters.WorkArea` for the primary screen, or the screen the focused window is on if
  that's what the issue specifies — read the AC). Account for DPI scaling: WPF positions in DIPs;
  work-area values are already DIP-converted via `SystemParameters`, but verify on a scaled display.
- Placement math belongs in `Logic.AppManagement` (testable: given work-area rect + overlay size →
  expected origin), with the WPF window applying it. That split is what the layering demands and
  lets the math carry `@WHISPER-100` unit/scenario coverage; the actual on-screen placement is the
  manual-verification remainder (single + multi-monitor, different taskbar positions, DPI 125/150%).
- Branch: `fix/whisper-100-overlay-placement`.

## WHISPER-102 — Overlay feedback: recording state, elapsed time, near-cap warning (LAST)

- Consumes the WHISPER-111 messages (all in `src/Application/Dictation/`, published on `IMessenger`
  by `DictationOrchestrator`, payloads `(int RecordedMs, int LimitMs)`):
  - `DictationNearLimitMessage` — 80% of soft limit (soft default 10 min)
  - `DictationAtLimitMessage` — soft limit reached, recording continues
  - `DictationHardLimitStopMessage` — hard failsafe (20 min) auto-stopped and transcribed
  These have zero consumers today — this issue binds them. Subscribe in the overlay VM with the
  WHISPER-94 messenger discipline (weak registration in OnActivated-equivalent, unregister on
  deactivate; `HotkeyViewModel`/`HotkeyConfigurationHostedService` are the reference patterns).
- Also: recording state (the orchestrator's `StageChanged`/`DictationStage` already drives the
  overlay — extend the visual state machine) and elapsed time (drive from a `TimeProvider`-based
  timer for testability — `ManualTimeProvider` exists in both test projects; `CaptureBuffer.RecordedDurationMs`
  is the authoritative recorded duration if a pull model is cleaner than a VM timer).
- **Hard constraint from the original briefing: the overlay's footprint must not change** — 100
  just positioned it. New feedback goes inside the existing bounds (state color/icon, compact text,
  meter recolor near cap) — no growth, no second window.
- Specs: `@WHISPER-102` scenarios publishing the messages through the real messenger and asserting
  VM state (near-cap flag, elapsed text, state transitions). UI rendering remains smoke +
  manual remainder.
- Branch: `feat/whisper-102-overlay-feedback`.
