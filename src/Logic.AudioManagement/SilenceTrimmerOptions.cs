// Tunables for trailing-silence trimming (WHISPER-112). End-of-speech is detected by ENERGY, not by raw
// per-sample amplitude: the clip is scanned in short frames (FrameMs) and a frame counts as silence only
// when its RMS energy is below EnergyThreshold. A per-sample threshold cut quiet word endings — the
// individual samples of a word trailing off dip below the amplitude bar even though the frame still
// carries real speech energy — so a low-but-present tail was wrongly trimmed as dead air. RMS over a
// window distinguishes quiet speech (real energy) from genuine dead air (near the noise floor).
// TrailingSilenceWindowMs is how long a sub-threshold tail must run before it counts as dead air rather
// than the soft end of speech; TrailingPadMs is how much of the recorded tail to keep beyond the last
// speech when trimming.

namespace Logic.AudioManagement;

public sealed record SilenceTrimmerOptions(
	int TrailingSilenceWindowMs = 150,
	int TrailingPadMs = 50,
	float EnergyThreshold = 0.002f,
	int FrameMs = 20);
