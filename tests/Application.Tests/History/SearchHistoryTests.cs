// Unit tests for the History section's search slice: the validator's paging rules, the handler passing
// the search text and page bounds through to the store (so filtering happens there), blank text meaning
// "no filter", the page-count math, and clamping a page past the end to the last page. Uses a substituted
// IHistoryStore and the real HistoryMapper.

using Application.History;
using Application.Ports;
using Domain.History;
using FluentValidation.Results;
using NSubstitute;
using Xunit;

namespace Application.Tests.History;

public sealed class SearchHistoryTests
{
	private static readonly DateTimeOffset Base = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

	private readonly IHistoryStore _store = Substitute.For<IHistoryStore>();
	private readonly HistoryMapper _mapper = new();

	private SearchHistoryHandler NewHandler() => new(_store, _mapper);

	private static HistoryPage PageOf(int count, int total) =>
		new(Enumerable.Range(0, count).Select(i => new TranscriptEntry(Guid.NewGuid(), $"entry {i}", Base.AddMinutes(i))).ToList(), total);

	[Fact]
	public async Task Passes_the_search_text_and_page_bounds_to_the_store()
	{
		_store.SearchAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns(PageOf(5, 30));

		HistoryPageDto result = await NewHandler().Handle(new SearchHistoryQuery("  milk ", Page: 2, PageSize: 25), CancellationToken.None);

		await _store.Received(1).SearchAsync("milk", 25, 25, Arg.Any<CancellationToken>());
		Assert.Equal(2, result.Page);
		Assert.Equal(30, result.TotalCount);
		Assert.Equal(2, result.TotalPages);
		Assert.Equal(5, result.Entries.Count);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task Blank_text_searches_without_a_filter(string? text)
	{
		_store.SearchAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns(PageOf(0, 0));

		await NewHandler().Handle(new SearchHistoryQuery(text, 1, 25), CancellationToken.None);

		await _store.Received(1).SearchAsync(null, 0, 25, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task A_page_past_the_end_is_clamped_to_the_last_page()
	{
		_store.SearchAsync(Arg.Any<string?>(), 75, 25, Arg.Any<CancellationToken>()).Returns(PageOf(0, 30));
		_store.SearchAsync(Arg.Any<string?>(), 25, 25, Arg.Any<CancellationToken>()).Returns(PageOf(5, 30));

		HistoryPageDto result = await NewHandler().Handle(new SearchHistoryQuery(null, Page: 4, PageSize: 25), CancellationToken.None);

		Assert.Equal(2, result.Page);
		Assert.Equal(5, result.Entries.Count);
	}

	[Fact]
	public async Task An_empty_result_is_page_one_of_one()
	{
		_store.SearchAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns(PageOf(0, 0));

		HistoryPageDto result = await NewHandler().Handle(new SearchHistoryQuery("zebra", 1, 25), CancellationToken.None);

		Assert.Equal(1, result.Page);
		Assert.Equal(1, result.TotalPages);
		Assert.Empty(result.Entries);
	}

	[Theory]
	[InlineData(0, 1)]
	[InlineData(201, 1)]
	[InlineData(25, 0)]
	public void Rejects_invalid_paging(int pageSize, int page)
	{
		ValidationResult result = new SearchHistoryQueryValidator().Validate(new SearchHistoryQuery(null, page, pageSize));

		Assert.False(result.IsValid);
	}

	[Fact]
	public void Accepts_valid_paging() =>
		Assert.True(new SearchHistoryQueryValidator().Validate(new SearchHistoryQuery("milk", 1, 25)).IsValid);
}
