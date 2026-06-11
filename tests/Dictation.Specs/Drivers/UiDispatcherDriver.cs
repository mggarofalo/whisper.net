// Drives the @WHISPER-90 UI-dispatcher-seam scenarios. It owns HOW the seam is exercised so the steps
// stay one-liners: it builds the REAL TrayIconViewModel / LevelOverlayViewModel over their real
// controllers (real RecordingStateMachine, faked shell/lifetime/audio seams) and a synchronous
// RecordingUiDispatcher, drives controller events as production threads would, and asserts both the
// marshaling contract (posted vs CheckAccess fast-path; never a blocking call) and the resulting
// view-model state. The grep assertion makes AC1 enforceable: no production source touches
// Application.Current.Dispatcher any more — everything marshals through the seam.

using Application.Ports;
using AwesomeAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Dictation.Specs.Support;
using Domain.Audio;
using Domain.Recording;
using Logic.AppManagement;
using Logic.AppManagement.Tray;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class UiDispatcherDriver : IDisposable
{
	private readonly RecordingStateMachine _stateMachine = new();
	private readonly IAudioSource _audioSource = Substitute.For<IAudioSource>();
	private readonly FakeShellPresenter _shellPresenter = new();
	private readonly FakeApplicationLifetime _lifetime = new();
	private readonly RecordingUiDispatcher _dispatcher = new();

	private TrayController? _trayController;
	private TrayIconViewModel? _trayViewModel;
	private LevelOverlayController? _overlayController;
	private LevelOverlayViewModel? _overlayViewModel;

	public void CreateTrayViewModel()
	{
		_trayController = new TrayController(_stateMachine, _shellPresenter, _lifetime);
		_trayViewModel = new TrayIconViewModel(_trayController, _dispatcher);
	}

	public void CreateOverlayViewModel()
	{
		// The messenger and clock are incidental here (this driver tests the dispatcher seam, WHISPER-90);
		// the overlay's limit/failure feedback is exercised by the @WHISPER-102 driver.
		_overlayController = new LevelOverlayController(_stateMachine, _audioSource, new WeakReferenceMessenger(), new ManualTimeProvider());
		_overlayViewModel = new LevelOverlayViewModel(_overlayController, _dispatcher);
	}

	// The scenario's "already on the UI thread": the dispatcher reports access, so handlers take the
	// fast-path instead of queueing.
	public void GrantUiThreadAccess() => _dispatcher.IsOnUiThread = true;

	public void StartRecording() => _stateMachine.RequestStart();

	public void StartRecordingAndEmitFrame()
	{
		_stateMachine.RequestStart();

		float[] loud = new float[480];
		Array.Fill(loud, 0.6f);
		_audioSource.FrameAvailable += Raise.EventWith(
			new AudioFrameAvailableEventArgs(loud, new CaptureFormat(16_000, 1, 32, AudioSampleFormat.IeeeFloat)));
	}

	// --- assertions ---

	public void AssertStatusUpdateWasMarshaled()
	{
		_dispatcher.PostCount.Should().BeGreaterThan(0, "an off-UI-thread status change must be queued through the seam");
		_dispatcher.InvokeAsyncCount.Should().Be(0, "the seam has no blocking path and status updates do not await");
	}

	public void AssertNoDispatcherRoundTrip()
	{
		_dispatcher.PostCount.Should().Be(0, "a caller already on the UI thread takes the CheckAccess fast-path");
		_dispatcher.InvokeAsyncCount.Should().Be(0);
	}

	public void AssertTrayReflectsRecording()
	{
		_trayViewModel!.Status.Should().Be(RecordingState.Recording);
		_trayViewModel.ToolTipText.Should().Be("Whisper — recording");
	}

	public void AssertLevelUpdateWasPostedWithoutBlocking()
	{
		_dispatcher.PostCount.Should().BeGreaterThan(0, "per-frame level updates are posted, never invoked synchronously");
		_dispatcher.InvokeAsyncCount.Should().Be(0, "the audio thread never awaits the UI thread for a meter refresh");
	}

	public void AssertOverlayReflectsLevel()
	{
		_overlayViewModel!.IsOverlayVisible.Should().BeTrue();
		_overlayViewModel.Level.Should().BeGreaterThan(0);
	}

	public void AssertNoProductionSourceReferencesWpfDispatcher()
	{
		string sourceRoot = Path.Combine(FindRepositoryRoot(), "src");
		string[] offenders = Directory
			.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
			.Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
			.Where(file => File.ReadAllText(file).Contains("Application.Current.Dispatcher"))
			.ToArray();

		offenders.Should().BeEmpty("all UI-thread marshaling goes through the IUiDispatcher seam (WHISPER-90 AC1)");
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
		_trayViewModel?.Dispose();
		_trayController?.Dispose();
		_overlayViewModel?.Dispose();
		_overlayController?.Dispose();
		_lifetime.Dispose();
	}
}
