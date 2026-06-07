// Drives the usage-stats scenarios through the REAL Mediator pipeline and the REAL Logic aggregator,
// substituting only the IHistoryStore port. It seeds the store with entries whose word counts sum to
// a target, requests the stats, and asserts on the aggregated result. Scenario-scoped.

using Application.Ports;
using Application.Statistics;
using AwesomeAssertions;
using Domain.History;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class UsageStatsDriver(IMediator mediator, IHistoryStore store)
{
	private static readonly DateTimeOffset When = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

	private UsageStatsDto? _result;

	// Seeds the store with `sessions` entries whose word counts sum to `words`.
	public void StoreContains(int words, int sessions)
	{
		List<TranscriptEntry> entries = [];
		int perSession = words / sessions;
		int remainder = words % sessions;

		for (int i = 0; i < sessions; i++)
		{
			int wordsThisSession = perSession + (i < remainder ? 1 : 0);
			entries.Add(new TranscriptEntry(Guid.NewGuid(), Words(wordsThisSession), When.AddMinutes(i)));
		}

		ReturnEntries(entries);
	}

	public void StoreIsEmpty() => ReturnEntries([]);

	public async Task RequestUsageStats() =>
		_result = await mediator.Send(new GetUsageStatsQuery(From: null, To: null));

	public void AssertReports(int words, int sessions)
	{
		_result.Should().NotBeNull();
		_result!.TotalWords.Should().Be(words);
		_result.TotalSessions.Should().Be(sessions);
	}

	private void ReturnEntries(IReadOnlyList<TranscriptEntry> entries) =>
		store.GetEntriesAsync(Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
			.Returns(entries);

	private static string Words(int count) => string.Join(' ', Enumerable.Repeat("word", count));
}
