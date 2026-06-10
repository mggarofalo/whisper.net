// Published on the shared IMessenger when an in-progress recording approaches the soft maximum
// duration (WHISPER-111) — at 80% of the limit — so the UI can warn the user before anything could
// be lost. Recording continues regardless; the limit is soft and no audio is ever dropped. Carries
// how much has been recorded and what the configured limit is, both in milliseconds.

namespace Application.Dictation;

public sealed record DictationNearLimitMessage(int RecordedMs, int LimitMs);
