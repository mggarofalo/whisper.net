// Published on the shared IMessenger when a dictation fails — the capture device errored, or the
// transcribe/deliver pipeline threw (pairing with the user notification). The recording is discarded
// or the pipeline returns to Idle either way; this signal exists so the overlay can show a brief error
// state instead of just vanishing, so a windowless failure does not read as "nothing happened".
// Carries nothing — the overlay shows a generic error; the detailed message goes to the user notifier.

namespace Application.Dictation;

public sealed record DictationFailedMessage;
