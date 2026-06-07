// Unit tests for the usage-summary slice (WHISPER-24): the handler loads history, delegates aggregation
// to the calculator, and projects the result through the REAL UsageSummaryMapper to a DTO (totals + the
// per-day breakdown). Uses a substituted IHistoryStore + IUsageStatsCalculator and the real mapper.

using Application.Ports;
using Application.Statistics;
using Domain.History;
using Domain.Statistics;
using NSubstitute;
using Xunit;

namespace Application.Tests.Statistics;

public sealed class UsageSummaryTests
{
	private readonly IHistoryStore _store = Substitute.For<IHistoryStore>();
	private readonly IUsageStatsCalculator _calculator = Substitute.For<IUsageStatsCalculator>();
	private readonly UsageSummaryMapper _mapper = new();

	[Fact]
	public async Task Handler_projects_the_aggregated_summary_to_a_dto()
	{
		_store.GetEntriesAsync(Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
			.Returns([]);
		UsageSummary summary = new(
			TotalTranscriptions: 2,
			TotalCharacters: 120,
			TotalAudioDuration: TimeSpan.FromSeconds(20),
			ByDay: [new DailyUsage(new DateOnly(2026, 1, 1), 2, 120, TimeSpan.FromSeconds(20))]);
		_calculator.Summarize(Arg.Any<IReadOnlyList<TranscriptEntry>>()).Returns(summary);

		GetUsageSummaryHandler handler = new(_store, _calculator, _mapper);
		UsageSummaryDto dto = await handler.Handle(new GetUsageSummaryQuery(null, null), CancellationToken.None);

		Assert.Equal(2, dto.TotalTranscriptions);
		Assert.Equal(120, dto.TotalCharacters);
		Assert.Equal(TimeSpan.FromSeconds(20), dto.TotalAudioDuration);
		DailyUsageDto day = Assert.Single(dto.ByDay);
		Assert.Equal(new DateOnly(2026, 1, 1), day.Day);
		Assert.Equal(2, day.Transcriptions);
		Assert.Equal(120, day.Characters);
		Assert.Equal(TimeSpan.FromSeconds(20), day.AudioDuration);
	}
}
