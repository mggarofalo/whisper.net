// Activates the end-to-end dictation pipeline for the application's lifetime. It closes the
// production wiring the earlier modules left: the raw global-key edges from IHotkeyListener are
// forwarded into the HotkeyActivationController (chord/push-to-talk/toggle matching), whose
// start/stop requests the DictationOrchestrator turns into capture -> transcribe -> deliver.
//
// The orchestrator is scoped — it shares one Mediator scope with the delivery handlers it dispatches —
// so the root host can't hold it directly. This hosted service opens a single long-lived scope on start,
// resolves the orchestrator from it (whose constructor subscribes to the controller), bridges the
// listener to the controller, and tears the bridge and scope down on graceful shutdown.

using Application.Ports;
using Domain.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Logic.AppManagement.Lifecycle;

public sealed class DictationOrchestratorHostedService(IServiceScopeFactory scopeFactory) : IHostedService
{
	private IServiceScope? _scope;
	private IHotkeyListener? _listener;
	private HotkeyActivationController? _controller;
	private DictationOrchestrator? _orchestrator;

	public Task StartAsync(CancellationToken cancellationToken)
	{
		_scope = scopeFactory.CreateScope();
		IServiceProvider services = _scope.ServiceProvider;

		// Resolving the orchestrator wires its controller -> capture -> deliver subscriptions for the app
		// lifetime; it then reacts to the controller's high-level start/stop requests.
		_orchestrator = services.GetRequiredService<DictationOrchestrator>();

		// Bridge the raw key edges into the activation controller so a real hotkey drives the pipeline.
		_listener = services.GetRequiredService<IHotkeyListener>();
		_controller = services.GetRequiredService<HotkeyActivationController>();
		_listener.KeyDown += OnKeyDown;
		_listener.KeyUp += OnKeyUp;

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		if (_listener is not null)
		{
			_listener.KeyDown -= OnKeyDown;
			_listener.KeyUp -= OnKeyUp;
			_listener = null;
		}

		_controller = null;
		_orchestrator = null;
		_scope?.Dispose();
		_scope = null;
		return Task.CompletedTask;
	}

	private void OnKeyDown(object? sender, KeyboardKeyEventArgs e)
	{
		// Esc exits continuous dictation mode and returns the pipeline to Idle; it is not a
		// recording chord, so it is handled here rather than forwarded to the activation controller.
		if (e.Key == KeyboardKey.Escape)
		{
			_orchestrator?.ExitContinuousMode();
			return;
		}

		_controller?.HandleKeyDown(e.Key, e.Modifiers);
	}

	private void OnKeyUp(object? sender, KeyboardKeyEventArgs e) => _controller?.HandleKeyUp(e.Key, e.Modifiers);
}
