// Unit tests for the paged browse slice (WHISPER-17): the query validator's paging rules and the
// handler's newest-first ordering, paging math, and optional case-insensitive text filter (the edge
// cases behind the @WHISPER-17 scenarios). Uses a substituted IHistoryStore and the real HistoryMapper.

using Application.History;
using Application.Ports;
using Domain.History;
using FluentValidation.Results;
using NSubstitute;
using Xunit;

namespace Application.Tests.History;

public sealed class BrowseHistoryTests
{
	private static readonly DateTimeOffset Base = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

	private readonly IHistoryStore _store = Substitute.For<IHistoryStore>();
	private readonly HistoryMapper _mapper = new();

	private void StoreHas(int count)
	{
		// Returned oldest-first so the assertions prove the handler imposes newest-first ordering.
		TranscriptEntry[] entries = Enumerable.Range(1, count)
			.Select(i => new TranscriptEntry(Guid.NewGuid(), $"entry {i:D4}", Base.AddMinutes(i)))
			.ToArray();

		_store.GetEntriesAsync(Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
			.Returns(entries);
	}

	[Theory]
	[InlineData(10, 1, 10)]
	[InlineData(10, 3, 5)]
	[InlineData(50, 1, 25)]
	public async Task Returns_the_requested_page(int pageSize, int page, int expectedCount)
	{
		StoreHas(25);
		BrowseHistoryHandler handler = new(_store, _mapper);

		IReadOnlyList<TranscriptEntryDto> result =
			await handler.Handle(new BrowseHistoryQuery(pageSize, page), CancellationToken.None);

		Assert.Equal(expectedCount, result.Count);
	}

	[Fact]
	public async Task Orders_each_page_most_recent_first()
	{
		StoreHas(25);
		BrowseHistoryHandler handler = new(_store, _mapper);

		IReadOnlyList<TranscriptEntryDto> result =
			await handler.Handle(new BrowseHistoryQuery(PageSize: 10, Page: 1), CancellationToken.None);

		Assert.Equal("entry 0025", result[0].Text);
		Assert.True(result.SequenceEqual(result.OrderByDescending(e => e.CreatedAt)));
	}

	[Fact]
	public async Task Applies_the_case_insensitive_text_filter()
	{
		TranscriptEntry[] entries =
		[
			new(Guid.NewGuid(), "buy MILK", Base.AddMinutes(1)),
			new(Guid.NewGuid(), "send email", Base.AddMinutes(2)),
			new(Guid.NewGuid(), "more milk please", Base.AddMinutes(3)),
		];
		_store.GetEntriesAsync(Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
			.Returns(entries);
		BrowseHistoryHandler handler = new(_store, _mapper);

		IReadOnlyList<TranscriptEntryDto> result =
			await handler.Handle(new BrowseHistoryQuery(PageSize: 10, Page: 1, TextFilter: "milk"), CancellationToken.None);

		Assert.Equal(["more milk please", "buy MILK"], result.Select(e => e.Text));
	}

	[Theory]
	[InlineData(0, 1)]
	[InlineData(-1, 1)]
	[InlineData(201, 1)]
	[InlineData(10, 0)]
	[InlineData(10, -1)]
	public void Validator_rejects_invalid_paging(int pageSize, int page)
	{
		ValidationResult result = new BrowseHistoryQueryValidator().Validate(new BrowseHistoryQuery(pageSize, page));

		Assert.False(result.IsValid);
	}

	[Fact]
	public void Validator_accepts_valid_paging()
	{
		ValidationResult result = new BrowseHistoryQueryValidator().Validate(new BrowseHistoryQuery(PageSize: 10, Page: 1));

		Assert.True(result.IsValid);
	}
}
