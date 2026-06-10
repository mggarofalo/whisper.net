// Drives the @WHISPER-95 error-surfacing scenarios. It owns HOW failures are exercised so the steps
// stay one-liners. The pipeline half drives the REAL DictationOrchestrator (real capture/delivery
// composition, failing transcriber / failing capture device) and asserts the scenario-scoped recording
// notifier received a clear notice while the pipeline recovered to Idle. The notifier half drives the
// REAL TrayUserNotifier over a synchronous recording dispatcher, proving the marshal-through-the-seam
// and the never-throws degradation when the balloon is missing or failing. The dispatcher-exception
// notice is asserted on the composition root artifact, like the theming/packaging drivers.

using Application.Ports;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Domain.Audio;
using Logic.AppManagement;
using Logic.AppManagement.Notifications;
using Logic.AudioManagement;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class UserNotificationDriver(
	DictationOrchestrator orchestrator,
	ITranscriber transcriber,
	FakeAudioCaptureClient captureClient,
	RecordingUserNotifier notifier,
	ManualTimeProvider time,
	AudioBufferingOptions bufferingOptions)
{
	private static readonly string RepositoryRoot = FindRepositoryRoot();

	private readonly RecordingUiDispatcher _dispatcher = new();
	private readonly RecordingLogger<TrayUserNotifier> _logger = new();
	private readonly List<(string Title, string Message)> _balloons = [];
	private TrayUserNotifier? _trayNotifier;

	// --- failing pipeline (AC1) ---

	public void TranscriptionWillFail() =>
		transcriber
			.TranscribeAsync(Arg.Any<AudioClip>(), Arg.Any<CancellationToken>())
			.Returns<TranscriptionResult>(_ => throw new InvalidOperationException("transcription failed"));

	public async Task RecordAndStop()
	{
		orchestrator.Start();
		captureClient.ProduceFrame(new float[960]);
		await orchestrator.StopAndElapseGraceAsync(time, bufferingOptions);
	}

	public void StartAndFailDevice()
	{
		orchestrator.Start();
		captureClient.ProduceFrame(new float[960]);
		captureClient.Fail(AudioCaptureError.DeviceUnavailable, "device unplugged");
	}

	public void AssertFailureNotified()
	{
		notifier.Notifications.Should().NotBeEmpty("a backend failure must surface a user notification (WHISPER-95 AC1)");
		(string title, string message) = notifier.Notifications[0];
		title.Should().NotBeNullOrWhiteSpace();
		message.Should().NotBeNullOrWhiteSpace();
		message.Should().NotContain("Exception", "the notice is non-technical; the technical record stays in the log");
	}

	public void AssertPipelineIdle() => orchestrator.Stage.Should().Be(DictationStage.Idle);

	// --- the notifier itself (AC2) ---

	public void CreateNotifierWithRecordingBalloon()
	{
		_trayNotifier = new TrayUserNotifier(_dispatcher, _logger);
		_trayNotifier.AttachPresenter((title, message) => _balloons.Add((title, message)));
	}

	public void CreateNotifierWithoutPresenter() => _trayNotifier = new TrayUserNotifier(_dispatcher, _logger);

	public void CreateNotifierWithThrowingPresenter()
	{
		_trayNotifier = new TrayUserNotifier(_dispatcher, _logger);
		_trayNotifier.AttachPresenter((_, _) => throw new InvalidOperationException("notifications suppressed"));
	}

	public void RaiseErrorOffUiThread() => _trayNotifier!.NotifyError("Dictation failed", "test message");

	public void AssertBalloonMarshaledThroughSeam()
	{
		_dispatcher.PostCount.Should().Be(1, "an off-UI-thread notification is queued through the dispatcher seam");
		_balloons.Should().ContainSingle().Which.Title.Should().Be("Dictation failed");
	}

	public void AssertSwallowedWithWarning() =>
		_logger.Entries.Should().Contain(
			entry => entry.Level == LogLevel.Warning,
			"a suppressed/failed notification is logged and swallowed, never thrown (WHISPER-95 AC2)");

	// --- the dispatcher-exception notice (AC3) ---

	public void AssertDispatcherExceptionHandlerNotifies()
	{
		string app = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Presentation", "App.xaml.cs"));

		app.Should().Contain("DispatcherUnhandledException", "the composition root handles dispatcher exceptions");
		app.Should().Contain("NotifyError", "the handler surfaces a notice through the notifier (WHISPER-95 AC3)");
		app.Should().Contain("The app is still running", "the notice is reassuring and non-technical");
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
}
