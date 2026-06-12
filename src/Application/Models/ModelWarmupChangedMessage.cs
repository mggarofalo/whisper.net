// Published on the shared IMessenger when the dictation model's warm-up (WHISPER-127) starts and again
// when it ends — the single app-wide signal the UI uses to show a "warming up" cue and then clear it
// (WHISPER-129). IsWarming is true when a warm-up begins and false when it ends (success, failure, OR
// cancellation), so every surface clears in lockstep and none is ever left stuck "warming". Broadcast for
// both the startup warm-up and the re-warm after an active-model switch. Carries only the flag; what the
// model is doing in detail is the warm-up service's log, not the user's overlay.

namespace Application.Models;

public sealed record ModelWarmupChangedMessage(bool IsWarming);
