// Unit depth for the live history feed, beyond the acceptance scenarios. Pins that a
// recorded-transcription message prepends the new entry newest-first and clears the empty state; that
// the feed stays live for the section's whole lifetime — including while it is the inactive cached
// instance (WHISPER-136), so an entry recorded on another tab is not missed; and that a redelivered
// message never doubles an entry already shown.

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
	private readonly IMediator _mediator = Substitute.For<IMediator>();
	private readonly IMessenger _messenger = new WeakReferenceMessenger();
	private readonly HistoryViewModel _viewModel;

	public HistoryViewModelLiveFeedTests()
	{
		// First-activation load returns an empty page by default; specific tests configure it before opening.
		_mediator.Send(Arg.Any<BrowseHistoryQuery>(), Arg.Any<CancellationToken>())
			.Returns(new ValueTask<IReadOnlyList<TranscriptEntryDto>>(Array.Empty<TranscriptEntryDto>()));
		_viewModel = new HistoryViewModel(_mediator, _messenger, Substitute.For<IUiCollectionSynchronizer>());
	}

	private static TranscriptEntryDto Dto(string text) =>
		new(Guid.NewGuid(), text, new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero), text.Length);

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
		_mediator.Send(Arg.Any<BrowseHistoryQuery>(), Arg.Any<CancellationToken>())
			.Returns(new ValueTask<IReadOnlyList<TranscriptEntryDto>>(new[] { Dto("older") }));
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
}
