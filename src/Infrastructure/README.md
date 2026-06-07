# Infrastructure

Implements the Application **ports** against real systems — Whisper.net transcription, NAudio
(WASAPI) capture, ONNX/Silero VAD, SendInput text injection, model file I/O, persistence. The only
layer that talks to the outside world.

**Depends on:** Application, Domain, and all `Logic.*`. **Referenced only by:** Presentation.

## Text delivery & UIPI

Text is delivered into the focused window by synthesizing Unicode keystrokes (`SendInputTextInjector`),
with a clipboard-paste fallback (`ClipboardTextInjector`). Both run unelevated by default.

Windows **User Interface Privilege Isolation (UIPI)** blocks synthetic input — `SendInput` *and*
clipboard paste driven by a synthetic `Ctrl+V` — from a medium-integrity process into a window owned
by a higher-integrity (elevated/admin) process. The keystrokes are silently discarded, so delivery
appears to "do nothing."

To avoid failing silently, `Win32ForegroundIntegrityProbe` checks the foreground window's integrity
relative to ours before delivery; when it is higher, the pipeline withholds delivery and returns a
`DeliveryBlock.Uipi` result the UI surfaces to the user instead of typing into the void.

**Workaround:** to dictate into an elevated window, run whisper.net elevated ("Run as administrator")
so both processes are at the same integrity level. This is a Windows security boundary, not a bug —
there is no way for an unelevated process to inject input into an elevated one.
