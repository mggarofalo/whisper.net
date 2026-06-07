// Unit tests for the history slice (WHISPER-47): the command validator's rules, the record handler's
// persistence, and the query handler's newest-first ordering + limit (the edge cases behind the
// @WHISPER-47 scenarios). Uses a substituted IHistoryStore and the real HistoryMapper.

using Application.Configuration;
using Application.History;
using Application.Ports;
using Domain.History;
using FluentValidation.Results;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Application.Tests.History;

public sealed class HistoryTests
{
	private static readonly DateTimeOffset T10 = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset T11 = new(2026, 1, 1, 11, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset T12 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

	private readonly IHistoryStore _store = Substitute.For<IHistoryStore>();
	private readonly HistoryMapper _mapper = new();

	[Fact]
	public void Validator_rejects_empty_text()
	{
		ValidationResult result = new RecordTranscriptionCommandValidator()
			.Validate(new RecordTranscriptionCommand("", T10));

		Assert.False(result.IsValid);
	}

	[Fact]
	public void Validator_rejects_an_unset_timestamp()
	{
		ValidationResult result = new RecordTranscriptionCommandValidator()
			.Validate(new RecordTranscriptionCommand("hello", default));

		Assert.False(result.IsValid);
	}

	[Fact]
	public void Validator_accepts_a_well_formed_command()
	{
		ValidationResult result = new RecordTranscriptionCommandValidator()
			.Validate(new RecordTranscriptionCommand("hello", T10));

		Assert.True(result.IsValid);
	}

	private static RecordTranscriptionHandler NewRecordHandler(IHistoryStore store, int maxEntries = 1000) =>
		new(store, Options.Create(new RetentionOptions { MaxEntries = maxEntries }));

	[Fact]
	public async Task Record_handler_saves_an_entry_built_from_the_command()
	{
		RecordTranscriptionHandler handler = NewRecordHandler(_store);

		await handler.Handle(new RecordTranscriptionCommand("take notes", T11), CancellationToken.None);

		await _store.Received(1).AddAsync(
			Arg.Is<TranscriptEntry>(e => e.Text == "take notes" && e.CreatedAt == T11),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Record_handler_prunes_to_the_configured_retention_limit_after_writing()
	{
		RecordTranscriptionHandler handler = NewRecordHandler(_store, maxEntries: 100);

		await handler.Handle(new RecordTranscriptionCommand("take notes", T11), CancellationToken.None);

		await _store.Received(1).PruneToMostRecentAsync(100, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Query_handler_orders_newest_first_and_applies_the_limit()
	{
		TranscriptEntry[] outOfOrder =
		[
			new(Guid.NewGuid(), "ten", T10),
			new(Guid.NewGuid(), "twelve", T12),
			new(Guid.NewGuid(), "eleven", T11),
		];
		_store.GetEntriesAsync(Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
			.Returns(outOfOrder);
		QueryHistoryHandler handler = new(_store, _mapper);

		IReadOnlyList<TranscriptEntryDto> result =
			await handler.Handle(new QueryHistoryQuery(null, null, 2), CancellationToken.None);

		Assert.Equal(["twelve", "eleven"], result.Select(e => e.Text));
	}

	[Fact]
	public async Task Query_handler_without_a_limit_returns_all_newest_first()
	{
		TranscriptEntry[] entries =
		[
			new(Guid.NewGuid(), "ten", T10),
			new(Guid.NewGuid(), "twelve", T12),
		];
		_store.GetEntriesAsync(Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
			.Returns(entries);
		QueryHistoryHandler handler = new(_store, _mapper);

		IReadOnlyList<TranscriptEntryDto> result =
			await handler.Handle(new QueryHistoryQuery(null, null, null), CancellationToken.None);

		Assert.Equal(["twelve", "ten"], result.Select(e => e.Text));
	}
}
