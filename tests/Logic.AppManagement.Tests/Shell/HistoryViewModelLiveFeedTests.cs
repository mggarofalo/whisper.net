// Unit depth for the live history feed, beyond the acceptance scenarios. Pins that a
// recorded-transcription message prepends the new entry newest-first and clears the empty state; that
// the feed stays live for the section's whole lifetime — including while it is the inactive cached
// instance (WHISPER-136), so an entry recorded on another tab is not missed; that a redelivered
// message never doubles an entry already shown; and, with paging and search (WHISPER-146), that a live
// entry keeps page 1 at its page size, never lands on a later page, and is skipped when it does not match
// the active search.

using Application.History;
using Application.Ports;
using AwesomeAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests.Shell;

public sealed class HistoryViewModelLiveFeedTests
{
	private const int PageSize = 25;

	private readonly IMediator _mediator = Substitute.For<IMediator>();
	private readonly IMessenger _messenger = new WeakReferenceMessenger();
	private readonly HistoryViewModel _viewModel;

	public HistoryViewModelLiveFeedTests()
	{
		// First-activation load returns an empty page by default; specific tests configure it before opening.
		ReturnPage([], totalCount: 0);
		_viewModel = new HistoryViewModel(_mediator, _messenger, Substitute.For<IUiCollectionSynchronizer>());
	}

	private static TranscriptEntryDto Dto(string text) =>
		new(Guid.NewGuid(), text, new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero), text.Length);

	private void ReturnPage(IReadOnlyList<TranscriptEntryDto> entries, int totalCount, int page = 1) =>
		_mediator.Send(Arg.Any<SearchHistoryQuery>(), Arg.Any<CancellationToken>())
			.Returns(new ValueTask<HistoryPageDto>(new HistoryPageDto(entries, page, PageSize, totalCount)));

	private void Open() => _viewModel.OnNavigatedTo();

	[Fact]
	public void A_recorded_entry_is_prepended_and_clears_the_empty_state_while_active()
	{
		Open();
		_viewModel.IsEmpty.Should().BeTrue("an empty first-activation load shows the empty state");

		TranscriptEntryDto entry = Dto("just dictated");
		_messenger.Send(new TranscriptionRecordedMessage(entry));

		_viewModel.Entries.Should().ContainSingle().Which.Text.Should().Be("just dictated");
		_viewModel.IsEmpty.Should().BeFalse();
	}

	[Fact]
	public void A_recorded_entry_is_prepended_above_existing_entries()
	{
		ReturnPage([Dto("older")], totalCount: 1);
		Open();
		_viewModel.Entries.Should().ContainSingle();

		_messenger.Send(new TranscriptionRecordedMessage(Dto("newer")));

		_viewModel.Entries.Should().HaveCount(2);
		_viewModel.Entries[0].Text.Should().Be("newer", "the live entry is prepended newest-first");
		_viewModel.Entries[1].Text.Should().Be("older", "already-loaded entries are preserved");
	}

	[Fact]
	public void The_feed_stays_live_after_the_section_is_deactivated()
	{
		Open();
		_viewModel.OnNavigatedFrom();

		// WHISPER-136: the live feed is persistent, so a transcription recorded while History is the inactive
		// cached instance still lands in the list and is there when the user returns — no re-query needed.
		_messenger.Send(new TranscriptionRecordedMessage(Dto("recorded while inactive")));

		_viewModel.Entries.Should().ContainSingle().Which.Text.Should().Be("recorded while inactive");
	}

	[Fact]
	public void A_redelivered_entry_is_not_doubled()
	{
		Open();
		TranscriptEntryDto entry = Dto("once only");

		_messenger.Send(new TranscriptionRecordedMessage(entry));
		_messenger.Send(new TranscriptionRecordedMessage(entry));

		_viewModel.Entries.Should().ContainSingle("a redelivery of the same entry must not double it up");
	}

	[Fact]
	public void A_live_entry_keeps_a_full_first_page_at_its_page_size_and_grows_the_page_count()
	{
		ReturnPage(Enumerable.Range(0, PageSize).Select(i => Dto($"entry {i}")).ToList(), totalCount: PageSize);
		Open();
		_viewModel.HasNextPage.Should().BeFalse();

		_messenger.Send(new TranscriptionRecordedMessage(Dto("newest")));

		_viewModel.Entries.Should().HaveCount(PageSize);
		_viewModel.Entries[0].Text.Should().Be("newest");
		_viewModel.TotalPages.Should().Be(2);
		_viewModel.HasNextPage.Should().BeTrue("the displaced oldest entry now sits on page 2");
	}

	[Fact]
	public async Task A_live_entry_is_not_inserted_into_a_later_page()
	{
		ReturnPage(Enumerable.Range(0, PageSize).Select(i => Dto($"entry {i}")).ToList(), totalCount: 30);
		Open();
		ReturnPage([Dto("older")], totalCount: 30, page: 2);
		await _viewModel.NextPageCommand.ExecuteAsync(null);

		_messenger.Send(new TranscriptionRecordedMessage(Dto("newest")));

		_viewModel.Entries.Select(entry => entry.Text).Should().Equal("older");
		_viewModel.TotalCount.Should().Be(31);
	}

	[Fact]
	public void A_live_entry_that_does_not_match_the_search_is_skipped()
	{
		Open();
		_viewModel.SearchText = "milk";

		_messenger.Send(new TranscriptionRecordedMessage(Dto("walk the dog")));
		_messenger.Send(new TranscriptionRecordedMessage(Dto("buy MILK")));

		_viewModel.Entries.Should().ContainSingle().Which.Text.Should().Be("buy MILK");
	}
}
