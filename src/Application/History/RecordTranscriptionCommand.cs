// CQRS command to record a completed transcription in history. Carries the recording outcome — the
// recognized text and when it happened — from which the handler builds a TranscriptEntry. Returns
// Unit; validated in the pipeline before it reaches the handler.

using Application.Interfaces;

namespace Application.History;

public sealed record RecordTranscriptionCommand(string Text, DateTimeOffset CreatedAt) : ICommand<Mediator.Unit>;
