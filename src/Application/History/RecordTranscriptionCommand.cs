// CQRS command to record a completed transcription in history. Carries the recording outcome — the
// recognized text, when it happened, and how long the captured audio ran (a usage-statistics measure)
// — from which the handler builds a TranscriptEntry. Returns Unit; validated in the pipeline
// before it reaches the handler. Duration defaults to zero so callers that do not measure it still compose.

using Application.Interfaces;

namespace Application.History;

public sealed record RecordTranscriptionCommand(string Text, DateTimeOffset CreatedAt, TimeSpan Duration = default)
	: ICommand<Mediator.Unit>;
