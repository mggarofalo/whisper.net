// The result of running voice-activity detection over a clip: a speech probability for each fixed-
// size analysis window, plus how many samples each window spans. The silence-trimming policy turns
// these per-window probabilities into gate/trim decisions; keeping the raw probabilities here (rather
// than a yes/no) lets the threshold be applied as configurable policy, not baked into the detector.

namespace Domain.Audio;

public sealed record VadAnalysis(IReadOnlyList<float> WindowProbabilities, int WindowSamples);
