// Makes a substituted IHistoryStore honor the SearchAsync contract over whatever entries the scenario has
// seeded through GetEntriesAsync: newest-first, a case-insensitive "contains" filter, skip/take paging and
// the total match count. Drivers keep seeding history the one way they always have (GetEntriesAsync
// returns), and the History section's store-side search reads that same history. The real SQL filter is
// pinned by SqliteHistoryStoreTests; this fake only has to keep the port's promise.

using Application.Ports;
using Domain.History;
using NSubstitute;

namespace Dictation.Specs.Support;

internal static class HistoryStoreSearchContract
{
	public static void AnswerSearchesFromSeededEntries(IHistoryStore store) =>
		store.SearchAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns(call => SearchAsync(store, call.ArgAt<string?>(0), call.ArgAt<int>(1), call.ArgAt<int>(2), call.ArgAt<CancellationToken>(3)));

	// Awaits the seeded read rather than blocking on it, so a scenario that holds GetEntriesAsync open (to
	// observe a loading state) holds the search open too.
	private static async ValueTask<HistoryPage> SearchAsync(IHistoryStore store, string? text, int skip, int take, CancellationToken cancellationToken)
	{
		IReadOnlyList<TranscriptEntry> seeded = await store.GetEntriesAsync(null, null, null, cancellationToken);

		List<TranscriptEntry> matching = seeded
			.Where(entry => string.IsNullOrEmpty(text) || entry.Text.Contains(text, StringComparison.OrdinalIgnoreCase))
			.OrderByDescending(entry => entry.CreatedAt)
			.ToList();

		return new HistoryPage(matching.Skip(skip).Take(take).ToList(), matching.Count);
	}
}
