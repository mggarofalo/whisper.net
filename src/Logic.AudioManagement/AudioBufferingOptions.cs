// Tunables for the capture normalization stage: how much lead-in audio to keep so speech onset before
// the trigger isn't clipped (preroll), how long a single recording may grow before it is force-
// finalized (a memory-safety cap), and the rate every clip is normalized to. 16 kHz mono is what the
// Whisper model expects, so it is the default target.

namespace Logic.AudioManagement;

public sealed record AudioBufferingOptions(int PrerollMs = 300, int MaxDurationMs = 30_000, int TargetSampleRate = 16_000);
