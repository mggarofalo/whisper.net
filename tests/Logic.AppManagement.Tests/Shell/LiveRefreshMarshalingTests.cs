// Regression test for WHISPER-138: the live transcription-recorded refresh on Home and Stats MUST be
// marshalled to the UI thread. TranscriptionRecordedMessage is published on the record/background thread;
// running RefreshCommand there raises AsyncRelayCommand.CanExecuteChanged on the wrong thread, which a
// bound Refresh button (settings window open) surfaces as a cross-thread InvalidOperationException. These
// tests prove the view-models route the refresh through IUiDispatcher instead of running it on the
// publishing thread; a revert to a direct RefreshCommand.Execute would drop PostCount to zero.

using Application.History;
using Application.Ports;
using AwesomeAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests.Shell;

public sealed class LiveRefreshMarshalingTests
{
	// Reports "not on the UI thread" and records posts WITHOUT running them, so a test asserts the refresh
	// was marshalled (posted) rather than executed inline on the publishing thread.
	private sealed class RecordingDispatcher : IUiDispatcher
	{
		public int PostCount { get; private set; }

		public bool CheckAccess() => false;

		public void Post(Action action) => PostCount++;

		public Task InvokeAsync(Action action)
		{
			action();
			return Task.CompletedTask;
		}
	}

	private static TranscriptEntryDto Entry() =>
		new(Guid.NewGuid(), "just dictated", new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero), WordCount: 2);

	[Fact]
	public void Stats_marshals_the_live_refresh_off_the_publishing_thread()
	{
		IMessenger messenger = new WeakReferenceMessenger();
		RecordingDispatcher dispatcher = new();
		StatsViewModel viewModel = new(Substitute.For<IMediator>(), messenger, dispatcher);

		messenger.Send(new TranscriptionRecordedMessage(Entry()));

		dispatcher.PostCount.Should().Be(1, "the refresh must be posted to the UI thread, not run on the record thread");
		GC.KeepAlive(viewModel);
	}

	[Fact]
	public void Home_marshals_the_live_refresh_off_the_publishing_thread()
	{
		IMessenger messenger = new WeakReferenceMessenger();
		RecordingDispatcher dispatcher = new();
		HomeViewModel viewModel = new(
			Substitute.For<IMediator>(), messenger, Substitute.For<IUiCollectionSynchronizer>(), dispatcher);

		messenger.Send(new TranscriptionRecordedMessage(Entry()));

		dispatcher.PostCount.Should().Be(1, "the refresh must be posted to the UI thread, not run on the record thread");
		GC.KeepAlive(viewModel);
	}
}
