// Probes whether the Whisper.net NATIVE runtime (whisper.dll / ggml) can actually load. This
// is distinct from "is a model downloaded": the native library is what WhisperFactory needs before it can
// read any model, and a packaging defect (e.g. embedding the natives for single-file self-extract, where
// the loader can't find them) silently breaks ALL transcription in the installed app. The doctor uses this
// to surface that as a hard failure instead of a runtime crash no one sees. Implemented in Infrastructure
// (the only layer that references Whisper.net); a higher-layer diagnostic check consumes the verdict.

namespace Application.Ports;

/// <summary>The result of probing the Whisper native runtime: whether it loaded, and a human-readable detail.</summary>
public sealed record WhisperRuntimeStatus(bool IsAvailable, string Detail);

public interface IWhisperRuntimeProbe
{
	/// <summary>Attempts to load the Whisper native runtime and reports whether it succeeded.</summary>
	WhisperRuntimeStatus Probe();
}
