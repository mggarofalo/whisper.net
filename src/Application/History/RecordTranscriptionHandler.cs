// Handles RecordTranscriptionCommand: builds a TranscriptEntry from the validated outcome and appends
// it to history via the IHistoryStore port. The command has passed the ValidationBehavior pipeline,
// so the domain factory will not reject it here. After each write the retention policy (WHISPER-17) is
// enforced — entries beyond the configured maximum are pruned — keeping history from growing without
// bound. The policy lives here (Application), out of the storage adapter.

using Application.Configuration;
using Application.Interfaces;
using Application.Ports;
using Domain.History;
using Microsoft.Extensions.Options;

namespace Application.History;

public sealed class RecordTranscriptionHandler(IHistoryStore store, IOptions<RetentionOptions> retention)
	: ICommandHandler<RecordTranscriptionCommand, Mediator.Unit>
{
	public async ValueTask<Mediator.Unit> Handle(RecordTranscriptionCommand command, CancellationToken cancellationToken)
	{
		TranscriptEntry entry = TranscriptEntry.Create(command.Text, command.CreatedAt, command.Duration);
		await store.AddAsync(entry, cancellationToken);

		// Enforce retention after the write; a non-positive limit disables pruning (handled by the store).
		await store.PruneToMostRecentAsync(retention.Value.MaxEntries, cancellationToken);

		return Mediator.Unit.Value;
	}
}
