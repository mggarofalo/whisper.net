// The opt-in, localhost-only AI rephrase adapter implementing IRephraseClient against a
// local Ollama HTTP endpoint. Privacy is the point: when disabled (the default) it makes NO network
// call and returns the original text; when enabled it only ever talks to a loopback host (a remote host
// is rejected at startup by OllamaRephraseOptionsValidator, and re-checked here defensively before any
// request). Backend problems (Ollama down, timeout, non-2xx, bad payload) are surfaced as a recoverable
// Failed result carrying the original text — a rephrase failure must never crash the dictation pipeline.
// User cancellation is still honored (it is not a rephrase failure).

using System.Net.Http.Json;
using Application.Ports;
using Application.Rephrase;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Rephrase;

public sealed class OllamaRephraseClient(
	HttpClient httpClient,
	IOptions<OllamaRephraseOptions> options,
	ILogger<OllamaRephraseClient> logger) : IRephraseClient
{
	public async ValueTask<RephraseResult> RephraseAsync(string text, string instruction, CancellationToken cancellationToken)
	{
		OllamaRephraseOptions current = options.Value;

		// Opt-in gate: disabled is the default, and a disabled client never touches the network.
		if (!current.Enabled)
		{
			return RephraseResult.Disabled(text);
		}

		// Defensive loopback re-check: startup validation already rejects a remote endpoint, but never
		// send transcript text to a non-loopback host even if that guard were somehow bypassed.
		if (!Uri.TryCreate(current.Endpoint, UriKind.Absolute, out Uri? endpoint) || !endpoint.IsLoopback)
		{
			logger.LogWarning("AI rephrase endpoint '{Endpoint}' is not loopback; skipping rephrase.", current.Endpoint);
			return RephraseResult.Failed(text);
		}

		using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(TimeSpan.FromSeconds(current.TimeoutSeconds));

		try
		{
			Uri generateUri = new(endpoint, "/api/generate");
			OllamaGenerateRequest request = new(current.Model, $"{instruction}\n\n{text}", Stream: false);

			using HttpResponseMessage response =
				await httpClient.PostAsJsonAsync(generateUri, request, timeout.Token).ConfigureAwait(false);
			response.EnsureSuccessStatusCode();

			OllamaGenerateResponse? body =
				await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(timeout.Token).ConfigureAwait(false);

			string? rephrased = body?.Response?.Trim();
			return string.IsNullOrEmpty(rephrased)
				? RephraseResult.Failed(text)
				: RephraseResult.Rephrased(rephrased);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// A genuine caller cancellation is not a rephrase failure — propagate it.
			throw;
		}
		catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or NotSupportedException or System.Text.Json.JsonException)
		{
			// Ollama unreachable, timed out, returned non-2xx, or sent an unparseable body: degrade
			// gracefully to the original text rather than letting the pipeline fault.
			logger.LogWarning(ex, "AI rephrase failed against {Endpoint}; returning the original text.", current.Endpoint);
			return RephraseResult.Failed(text);
		}
	}

	private sealed record OllamaGenerateRequest(string Model, string Prompt, bool Stream);

	private sealed record OllamaGenerateResponse(string? Response);
}
