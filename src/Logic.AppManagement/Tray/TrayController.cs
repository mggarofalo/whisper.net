// The tray icon's coordination logic, kept out of Presentation so it can be driven for
// real in specs. It mirrors the live recording status (subscribing to the RecordingStateMachine) into
// a status the tray icon reflects plus a human-readable tooltip, and exposes the two menu actions:
// Open Settings surfaces the settings window through the IShellPresenter seam, and Quit triggers a
// graceful host shutdown via IHostApplicationLifetime (the same mechanism the host bootstrap wires up).
// The thin H.NotifyIcon view binds to this; it owns no UI itself.

using Application.Ports;
using Domain.Recording;
using Microsoft.Extensions.Hosting;

namespace Logic.AppManagement.Tray;

public sealed class TrayController : IDisposable
{
	private readonly RecordingStateMachine _stateMachine;
	private readonly IShellPresenter _shell;
	private readonly IHostApplicationLifetime _lifetime;

	public TrayController(RecordingStateMachine stateMachine, IShellPresenter shell, IHostApplicationLifetime lifetime)
	{
		_stateMachine = stateMachine;
		_shell = shell;
		_lifetime = lifetime;
		Status = stateMachine.State;
		_stateMachine.StateChanged += OnStateChanged;
	}

	/// <summary>The current dictation status the tray icon reflects.</summary>
	public RecordingState Status { get; private set; }

	/// <summary>Human-readable description of the current status, for the tray tooltip / accessibility text.</summary>
	public string Tooltip => Status switch
	{
		RecordingState.Recording => "Whisper — recording",
		RecordingState.Transcribing => "Whisper — transcribing",
		_ => "Whisper — idle",
	};

	/// <summary>Raised whenever <see cref="Status"/> changes, so the view can refresh icon and tooltip.</summary>
	public event EventHandler? StatusChanged;

	/// <summary>"Open Settings" menu action: surface the settings window.</summary>
	public void OpenSettings() => _shell.ShowSettings();

	/// <summary>"Quit" menu action: request a graceful application shutdown.</summary>
	public void Quit() => _lifetime.StopApplication();

	private void OnStateChanged(object? sender, RecordingStateChangedEventArgs e)
	{
		Status = e.Current;
		StatusChanged?.Invoke(this, EventArgs.Empty);
	}

	public void Dispose() => _stateMachine.StateChanged -= OnStateChanged;
}
