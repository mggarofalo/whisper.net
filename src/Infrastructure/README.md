# Infrastructure

Implements the Application **ports** against real systems — Whisper.net transcription, NAudio
(WASAPI) capture, ONNX/Silero VAD, SendInput text injection, model file I/O, persistence. The only
layer that talks to the outside world.

**Depends on:** Application, Domain, and all `Logic.*`. **Referenced only by:** Presentation.
