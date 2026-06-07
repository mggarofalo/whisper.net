# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Optional AI rephrase (opt-in, localhost-only).** An optional post-processing step can rewrite
  recognized text with a locally-hosted [Ollama](https://ollama.com) model via the `IRephraseClient`
  port (`OllamaRephraseClient`). Privacy stance: it is **disabled by default** and makes **no network
  call** until explicitly enabled; when enabled it may only target a **loopback** endpoint
  (`localhost`/`127.0.0.1`/`[::1]`) — a remote endpoint is rejected at startup rather than silently
  used. Backend failures (Ollama down, timeout, non-2xx) degrade gracefully to the original text and
  never crash the dictation pipeline. This is the single disclosed transcript-bearing network seam.
  (WHISPER-40)
- **Custom vocabulary prompt-token conditioning.** A user-supplied vocabulary biases the Whisper
  decoder toward domain terms via an initial prompt, disabling the first-token log-probability
  threshold so the injected prompt cannot drop the genuine first token. Changes apply on the next
  transcription without reloading the model. (WHISPER-38)
- **Transcription normalization.** Bracketed/parenthesized noise labels (e.g. `[BLANK_AUDIO]`) are
  always stripped, and spoken filler words are removed when the "remove filler words" setting is on.
  (WHISPER-36)
