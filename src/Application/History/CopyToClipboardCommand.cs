// CQRS command to put a past transcription's text back on the clipboard (WHISPER-45): the history view
// dispatches it when the user re-copies an entry. Carries the text to copy; returns Unit. Keeping the
// clipboard write behind a command means the view-model never touches the clipboard port directly.

using Application.Interfaces;

namespace Application.History;

public sealed record CopyToClipboardCommand(string Text) : ICommand<Mediator.Unit>;
