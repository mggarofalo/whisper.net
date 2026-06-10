// Tunables for trailing-silence trimming (WHISPER-112): how long a quiet tail must run before it
// counts as dead air rather than the soft end of speech (TrailingSilenceWindowMs), how much of the
// recorded tail to keep beyond the last speech when trimming (TrailingPadMs), and the amplitude below
// which a sample counts as silence.

namespace Logic.AudioManagement;

public sealed record SilenceTrimmerOptions(int TrailingSilenceWindowMs = 150, int TrailingPadMs = 50, float AmplitudeThreshold = 0.01f);
