// Published on the shared IMessenger when an in-progress recording reaches the soft maximum
// duration. The limit is soft: the capture buffer keeps growing and nothing is
// dropped — this signal exists so the UI can tell the user the recording has run long. Carries
// how much has been recorded and what the configured limit is, both in milliseconds.

namespace Application.Dictation;

public sealed record DictationAtLimitMessage(int RecordedMs, int LimitMs);
