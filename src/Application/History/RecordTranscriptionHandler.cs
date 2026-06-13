// Handles RecordTranscriptionCommand: builds a TranscriptEntry from the validated outcome and appends
// it to history via the IHistoryStore port. The command has passed the ValidationBehavior pipeline,
// so the domain factory will not reject it here. After each write the retention policy is
// enforced — entries beyond the configured maximum are pruned — keeping history from growing without
// bound. The policy lives here (Application), out of the storage adapter. Once recorded, it publishes a
// TranscriptionRecordedMessage on the shared messenger so an open History list updates
// live; the message carries the entry's DTO so the subscriber can prepend it without a re-query.

using Application.Configuration;
using Application.Interfaces;
using Application.Ports;
using CommunityToolkit.Mvvm.Messaging;
using Domain.History;
using Microsoft.Extensions.Options;

namespace Application.History;

public sealed class RecordTranscriptionHandler(
	IHistoryStore store,
	IOptions<RetentionOptions> retention,
	IMessenger messenger,
	HistoryMapper mapper)
	: ICommandHandler<RecordTranscriptionCommand, Mediator.Unit>
{
	public async ValueTask<Mediator.Unit> Handle(RecordTranscriptionCommand command, CancellationToken cancellationToken)
	{
		TranscriptEntry entry = TranscriptEntry.Create(command.Text, command.CreatedAt, command.Duration);
		await store.AddAsync(entry, cancellationToken);

		// Enforce retention after the write; a non-positive limit disables pruning (handled by the store).
		await store.PruneToMostRecentAsync(retention.Value.MaxEntries, cancellationToken);

		// Notify any open History list so it shows the new entry live; the new entry is the
		// most recent, so retention never prunes it before this publishes.
		messenger.Send(new TranscriptionRecordedMessage(mapper.ToDto(entry)));

		return Mediator.Unit.Value;
	}
}
