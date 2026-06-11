// Drives the @WHISPER-81 model-download scenarios. It builds the REAL ModelViewModel over the REAL Mediator
// pipeline (ListModels / DownloadModel handlers) and the REAL catalog, faking only the device-facing
// downloader — gated on a signal so the test can observe a download IN FLIGHT, then cancel it. So it proves
// the user-visible outcomes WHISPER-81 adds: a download reports determinate progress and is running, a
// Cancel stops it and leaves the model un-activated, and a failed download surfaces a native error instead
// of crashing. The ProgressBar + Cancel button that bind to these are Presentation glue verified by smoke.

using Application.Ports;
using AwesomeAssertions;
using Domain.Models;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class ModelDownloadDriver
{
	private readonly ModelViewModel _viewModel;
	private readonly IModelDownloader _downloader;
	private readonly TaskCompletionSource _gate = new();

	private Task? _download;
	private string? _downloadingId;

	public ModelDownloadDriver(IMediator mediator, IModelDownloader downloader, IModelLifecycle lifecycle)
	{
		_downloader = downloader;

		// A DIFFERENT model is the loaded/active one, so the model under test here (base.en, which also
		// happens to be the persisted default) is cleanly not-active — the @WHISPER-81 assertions then mean
		// "the download did not activate it" rather than colliding with the persisted-active fallback
		// (WHISPER-118).
		lifecycle.Status.Returns(new ModelStatus("small.en", ModelState.Ready));

		_viewModel = new ModelViewModel(mediator);
	}

	public Task LoadList() => _viewModel.LoadCommand.ExecuteAsync(null);

	// The download reports 50% immediately, then blocks until the token cancels (this driver never opens the
	// gate, so the only way the in-flight download ends is cancellation — exactly what the scenario drives).
	public void ConfigureGatedDownload(string id) =>
		_downloader.DownloadAsync(Arg.Any<WhisperModelCatalogEntry>(), Arg.Any<IProgress<ModelDownloadProgress>>(), Arg.Any<CancellationToken>())
			.Returns(call => new ValueTask<string>(RunGatedDownload(
				call.ArgAt<IProgress<ModelDownloadProgress>>(1),
				call.ArgAt<CancellationToken>(2),
				id)));

	public void ConfigureFailingDownload() =>
		_downloader.DownloadAsync(Arg.Any<WhisperModelCatalogEntry>(), Arg.Any<IProgress<ModelDownloadProgress>>(), Arg.Any<CancellationToken>())
			.Returns<ValueTask<string>>(_ => throw new InvalidOperationException("download failed"));

	// Begin the download but do not await it — it is in flight (blocked) so the test can inspect the running
	// state and then cancel. The download is owned by the row (WHISPER-107).
	public void StartDownload(string id)
	{
		_downloadingId = id;
		_download = Item(id).DownloadCommand.ExecuteAsync(null);
	}

	// Download synchronously to its (failed) terminal state.
	public Task DownloadToCompletion(string id)
	{
		_downloadingId = id;
		return Item(id).DownloadCommand.ExecuteAsync(null);
	}

	public void Cancel() => Item(_downloadingId!).DownloadCancelCommand.Execute(null);

	public async Task AwaitDownload() => await _download!;

	public void AssertRunningWithProgress(string id)
	{
		Item(id).DownloadCommand.IsRunning.Should().BeTrue("the download is in flight");
		Item(id).DownloadState.Should().Be(ModelDownloadState.InProgress);
		Item(id).DownloadPercent.Should().Be(50d, "determinate progress is reported live");
	}

	public void AssertResetAndInactive(string id)
	{
		Item(id).DownloadState.Should().Be(ModelDownloadState.NotStarted, "a cancelled download resets the row");
		Item(id).IsActive.Should().BeFalse();
		_viewModel.ActiveModelId.Should().NotBe(id);
	}

	public void AssertNativeErrorShown() => Item(_downloadingId!).DownloadError.Should().NotBeNullOrEmpty();

	public void AssertNotActivated(string id)
	{
		Item(id).IsActive.Should().BeFalse();
		_viewModel.ActiveModelId.Should().NotBe(id);
	}

	private async Task<string> RunGatedDownload(IProgress<ModelDownloadProgress>? progress, CancellationToken cancellationToken, string id)
	{
		progress?.Report(new ModelDownloadProgress(50, 100));
		await _gate.Task.WaitAsync(cancellationToken);
		return $"/cache/{id}.bin";
	}

	private ModelItemViewModel Item(string id) =>
		_viewModel.Models.Single(model => string.Equals(model.Id, id, StringComparison.OrdinalIgnoreCase));
}
