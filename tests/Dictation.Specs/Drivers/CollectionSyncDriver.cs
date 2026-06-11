// Drives the @WHISPER-91 collection-synchronization scenarios. It owns HOW the pattern is exercised so
// the steps stay one-liners: it builds the REAL HistoryViewModel over the real Mediator pipeline, a
// faked history store, and the recording synchronizer (asserting registration-at-construction), proves
// a UiBoundCollection mutation contends for the registered gate, loads history from a thread-pool
// thread (the off-UI-thread mutation the pattern exists for), and checks the convention is documented.

using Application.History;
using Application.Ports;
using AwesomeAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Dictation.Specs.Support;
using Domain.History;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class CollectionSyncDriver(
	IMediator mediator,
	IHistoryStore store,
	IMessenger messenger,
	RecordingCollectionSynchronizer synchronizer) : IDisposable
{
	private static readonly string RepositoryRoot = FindRepositoryRoot();

	private HistoryViewModel? _viewModel;
	private UiBoundCollection<string>? _collection;
	private readonly ManualResetEventSlim _lockHeld = new();
	private readonly ManualResetEventSlim _releaseLock = new();
	private Task? _lockHolder;
	private Task? _pendingAdd;

	public void CreateHistoryViewModel() => _viewModel = new HistoryViewModel(mediator, messenger, synchronizer);

	public void AssertEntriesRegisteredWithGate()
	{
		synchronizer.Registrations.Should().ContainSingle(
			"the view-model registers its bound collection exactly once, at construction");
		(System.Collections.IEnumerable collection, object gate) = synchronizer.Registrations[0];
		collection.Should().BeSameAs(_viewModel!.Entries);
		gate.Should().BeSameAs(_viewModel.Entries.Gate);
	}

	// --- locked-mutation contention ---

	public void CreateStandaloneCollection() => _collection = [];

	public void HoldGateOnAnotherThread()
	{
		_lockHolder = Task.Run(() =>
		{
			lock (_collection!.Gate)
			{
				_lockHeld.Set();
				_releaseLock.Wait();
			}
		});
		_lockHeld.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue("the holder thread must acquire the gate");
	}

	public void StartAddOnBackgroundThread() => _pendingAdd = Task.Run(() => _collection!.Add("item"));

	public void AssertAddCompletesOnlyAfterGateReleased()
	{
		_pendingAdd!.Wait(TimeSpan.FromMilliseconds(250)).Should().BeFalse(
			"the mutation must block while another thread holds the gate");

		_releaseLock.Set();

		_pendingAdd.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue("the mutation completes once the gate is free");
		_collection.Should().ContainSingle().Which.Should().Be("item");
	}

	// --- background-thread load ---

	public void StoreHasEntries()
	{
		DateTimeOffset day = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
		store.GetEntriesAsync(Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
			.Returns([
				new TranscriptEntry(Guid.NewGuid(), "first", day),
				new TranscriptEntry(Guid.NewGuid(), "second", day.AddMinutes(1)),
			]);
		CreateHistoryViewModel();
	}

	public Task LoadOnBackgroundThread() =>
		Task.Run(() => _viewModel!.LoadCommand.ExecuteAsync(null));

	public void AssertEntriesListedSafely()
	{
		_viewModel!.Entries.Should().HaveCount(2, "the background-thread load populated the registered collection");
		_viewModel.IsEmpty.Should().BeFalse();
	}

	// --- convention ---

	public void AssertConventionDocumented()
	{
		string doc = File.ReadAllText(Path.Combine(RepositoryRoot, "docs", "architecture.md"));

		doc.Should().Contain("Background-thread collection updates", "the convention has its own documented section (WHISPER-91 AC3)");
		doc.Should().Contain("UiBoundCollection", "the helper type is named so new list-bearing VMs adopt it");
		doc.Should().Contain("IUiCollectionSynchronizer", "the registration seam is named");
		doc.Should().Contain("EnableCollectionSynchronization", "the underlying WPF mechanism is named");
	}

	private static string FindRepositoryRoot()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Whisper.slnx")))
		{
			directory = directory.Parent;
		}

		return directory?.FullName
			?? throw new InvalidOperationException("Could not locate the repository root (Whisper.slnx).");
	}

	public void Dispose()
	{
		_releaseLock.Set();
		_lockHolder?.Wait(TimeSpan.FromSeconds(5));
		_lockHeld.Dispose();
		_releaseLock.Dispose();
	}
}
