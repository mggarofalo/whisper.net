// Handles RecordTranscriptionCommand: builds a TranscriptEntry from the validated outcome and appends
// it to history via the IHistoryStore port. The command has passed the ValidationBehavior pipeline,
// so the domain factory will not reject it here.

using Application.Interfaces;
using Application.Ports;
using Domain.History;

namespace Application.History;

public sealed class RecordTranscriptionHandler(IHistoryStore store)
	: ICommandHandler<RecordTranscriptionCommand, Mediator.Unit>
{
	public async ValueTask<Mediator.Unit> Handle(RecordTranscriptionCommand command, CancellationToken cancellationToken)
	{
		TranscriptEntry entry = TranscriptEntry.Create(command.Text, command.CreatedAt);
		await store.AddAsync(entry, cancellationToken);
		return Mediator.Unit.Value;
	}
}
