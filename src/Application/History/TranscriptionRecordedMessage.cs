// Published on the shared IMessenger right after a transcription is appended to history (WHISPER-114), so
// an open History list can prepend the new entry live instead of going stale until a manual Refresh. It
// carries the recorded entry already projected to its DTO, so the subscriber can add it to its bound
// collection without a re-query (which would disturb the user's browsed page / scroll position).

namespace Application.History;

public sealed record TranscriptionRecordedMessage(TranscriptEntryDto Entry);
