// The payload of a recording state transition: where the machine was and where it now is. The tray/UI
// subscribes to reflect current status.

using Domain.Recording;

namespace Logic.AppManagement;

public sealed record RecordingStateChangedEventArgs(RecordingState Previous, RecordingState Current);
