// Tunables for the capture normalization stage: how much lead-in audio to keep so speech onset before
// the trigger isn't clipped (preroll); how long a single recording may run before the soft limit
// signals fire (WHISPER-111) — the limit is SOFT: recording continues and nothing is dropped, the
// CaptureBuffer's NearMaxDuration/MaxDurationReached events merely let the app warn the user; how long
// it may run before the HARD failsafe ceiling (default 2x the soft limit) at which the CaptureBuffer's
// HardLimitReached event makes the orchestration stop AND transcribe the recording — the enforcement
// bound that keeps an unattended recording from growing without end, while still never discarding
// audio; the rate every clip is normalized to (16 kHz mono is what the Whisper model expects, so it is
// the default target); and how long to keep capturing after the stop signal (WHISPER-112) so the
// device's asynchronously-flushed tail — the user's final syllables — lands in the clip instead of
// being dropped.

namespace Logic.AudioManagement;

public sealed record AudioBufferingOptions(
	int PrerollMs = 300,
	int MaxDurationMs = 600_000,
	int TargetSampleRate = 16_000,
	int PostReleaseGraceMs = 400,
	int HardMaxDurationMs = 1_200_000);
