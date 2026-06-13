// Drives the history write-through arrangement. It owns the one seam those scenarios add
// on top of the end-to-end orchestration driver: the scenario-scoped IHistoryStore substitute is
// configured to round-trip — AddAsync keeps each entry, GetEntriesAsync returns what was kept — so a
// recorded transcription is asserted through the REAL read path (the history browser and stats
// dashboard view-models over the real Mediator pipeline), never as a Received() call on the store fake.

using Application.Ports;
using Domain.History;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class HistoryRecordingDriver(IHistoryStore store)
{
	private readonly List<TranscriptEntry> _recorded = [];

	// Make the store substitute behave like an (initially empty) real store. The substitute is scoped to
	// this scenario, so the round-trip configuration cannot leak into scenarios that configure explicit
	// returns on their own fresh substitute.
	public void HistoryStartsEmpty()
	{
		store.AddAsync(Arg.Any<TranscriptEntry>(), Arg.Any<CancellationToken>())
			.Returns(call =>
			{
				_recorded.Add(call.Arg<TranscriptEntry>());
				return ValueTask.CompletedTask;
			});

		// Newest-first, per the port contract; the read handlers re-order defensively anyway.
		store.GetEntriesAsync(Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
			.Returns(_ => _recorded.OrderByDescending(entry => entry.CreatedAt).ToList());
	}
}
