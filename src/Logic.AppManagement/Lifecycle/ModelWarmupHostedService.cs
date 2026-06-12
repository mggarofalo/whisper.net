// Warms the dictation model as soon as the app starts (WHISPER-127) so the FIRST dictation isn't slowed
// by the cold model load + native init. Dictation runs through the ITranscriber port (WhisperTranscriber),
// which otherwise loads the model lazily on the first real transcription — the "long pause on first use".
// On startup this service kicks off ITranscriber.PreloadAsync in the BACKGROUND (load + a throwaway
// warm-up inference); it never blocks host startup or the UI thread, and it swallows failures (a fresh
// install with no model yet, or a transient load error) so warm-up can never crash the host — the first
// real dictation then just falls back to the lazy load it has today. It also re-warms when the user
// switches the active model (the instant-apply SettingsChangedMessage, WHISPER-78), so the first dictation
// after a model change is fast too, not only the first after launch. Singleton dependencies only, so the
// Generic Host owns it directly; registered only in the production composition (the specs fake ITranscriber).

using Application.Ports;
using Application.Settings;
using CommunityToolkit.Mvvm.Messaging;
using Domain.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Logic.AppManagement.Lifecycle;

public sealed class ModelWarmupHostedService(
	ITranscriber transcriber,
	ISettingsStore store,
	IMessenger messenger,
	ILogger<ModelWarmupHostedService> logger) : IHostedService
{
	// Cancels an in-flight warm-up when the host stops, so shutdown doesn't wait on a cold model load.
	private readonly CancellationTokenSource _cts = new();
	private string? _lastModelId;

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		AppSettings settings = await store.LoadAsync(cancellationToken);
		_lastModelId = settings.ModelId;

		// Re-warm when the active model changes (instant-apply channel, WHISPER-78). Weak registration,
		// matching the other lifecycle services: the host owns this singleton, so there is no leak and no
		// manual unsubscribe is required for correctness.
		messenger.Register<ModelWarmupHostedService, SettingsChangedMessage>(
			this, static (recipient, message) => recipient.OnSettingsChanged(message.Value));

		// Warm in the background so the cold model load never blocks host startup.
		WarmInBackground("startup");
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		// Stop listening before cancelling, so no new warm-up is scheduled while we tear down.
		messenger.UnregisterAll(this);
		_cts.Cancel();
		_cts.Dispose();
		return Task.CompletedTask;
	}

	private void OnSettingsChanged(AppSettings settings)
	{
		// Only a model change is worth re-warming — every other setting commit broadcasts the same message,
		// and re-running inference for them would needlessly contend the transcriber's load gate.
		if (string.Equals(settings.ModelId, _lastModelId, StringComparison.Ordinal))
		{
			return;
		}

		_lastModelId = settings.ModelId;
		WarmInBackground("model change");
	}

	// Run the warm-up entirely on the thread pool: the settings-change callback can arrive on the UI thread,
	// and the cold model load must never run there. The task is intentionally unobserved — WarmAsync owns
	// all of its errors, so a faulted task can never escape.
	private void WarmInBackground(string reason) => _ = Task.Run(() => WarmAsync(reason));

	private async Task WarmAsync(string reason)
	{
		try
		{
			await transcriber.PreloadAsync(_cts.Token).ConfigureAwait(false);
			logger.LogInformation("Model warm-up complete ({Reason}); the first dictation will not pay the cold-load cost.", reason);
		}
		catch (OperationCanceledException)
		{
			// Shutdown raced the warm-up; nothing to do.
		}
		catch (Exception ex)
		{
			// Best-effort: typically no usable model yet (fresh install / onboarding) or a transient load
			// failure. Dictation still works — the first utterance just loads the model lazily as before.
			logger.LogInformation(ex, "Model warm-up ({Reason}) did not complete; the model will load lazily on first dictation.", reason);
		}
	}
}
