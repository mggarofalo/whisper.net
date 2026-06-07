// Drives the history scenarios through the REAL Mediator pipeline (validation + handlers + Mapperly),
// substituting only the IHistoryStore port. RecordTranscriptionCommand is asserted at the port
// boundary (an entry was added); QueryHistoryQuery is asserted on the returned, ordered result. To
// prove the handler — not the fake — owns newest-first ordering, the store returns entries out of
// order. Scenario-scoped, so captured state is fresh per scenario.

using Application.History;
using Application.Ports;
using AwesomeAssertions;
using Domain.History;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class HistoryDriver(IMediator mediator, IHistoryStore store)
{
	private static readonly DateTimeOffset Oldest = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset Middle = new(2026, 1, 1, 11, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset Newest = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

	private IReadOnlyList<TranscriptEntryDto>? _result;

	public void StoreHasThreeEntriesFromDifferentTimes()
	{
		// Returned deliberately out of chronological order so the assertion proves the handler sorts.
		TranscriptEntry[] entries =
		[
			new(Guid.NewGuid(), "oldest", Oldest),
			new(Guid.NewGuid(), "newest", Newest),
			new(Guid.NewGuid(), "middle", Middle),
		];

		store.GetEntriesAsync(Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
			.Returns(entries);
	}

	public async Task RecordTranscription(string text) =>
		await mediator.Send(new RecordTranscriptionCommand(text, Newest));

	public void AssertEntrySaved(string expectedText) =>
		store.Received(1).AddAsync(
			Arg.Is<TranscriptEntry>(entry => entry.Text == expectedText),
			Arg.Any<CancellationToken>());

	public async Task QueryHistory(int limit) =>
		_result = await mediator.Send(new QueryHistoryQuery(From: null, To: null, Limit: limit));

	public void AssertTwoMostRecentNewestFirst()
	{
		_result.Should().NotBeNull();
		_result!.Select(entry => entry.Text).Should().Equal("newest", "middle");
	}
}
