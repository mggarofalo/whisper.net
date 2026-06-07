// Tunables for the VAD silence policy. A window counts as speech when its probability reaches
// SpeechThreshold; leading silence before the first speech is trimmed down to LeadingKeepMs (a small
// preroll so onset isn't clipped); internal pauses longer than MidSilenceCollapseMs are collapsed to
// that length. All three are configurable per the issue's requirement.

namespace Logic.AudioManagement;

public sealed record VadOptions(float SpeechThreshold = 0.5f, int MidSilenceCollapseMs = 1_000, int LeadingKeepMs = 300);
