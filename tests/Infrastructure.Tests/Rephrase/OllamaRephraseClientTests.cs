// Inner TDD loop for the rephrase adapter, over a fake HTTP transport (no Ollama, no socket).
// Confirms the opt-in gate (disabled -> no call), the happy path (a request to a loopback /api/generate
// returning the rewritten text), graceful degradation on a backend failure, the defensive loopback
// guard, and that genuine caller cancellation still propagates.

using System.Net;
using System.Text;
using Application.Rephrase;
using AwesomeAssertions;
using Infrastructure.Rephrase;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Infrastructure.Tests.Rephrase;

public sealed class OllamaRephraseClientTests
{
	private readonly StubHttpMessageHandler _handler = new();

	private OllamaRephraseClient Client(OllamaRephraseOptions options) =>
		new(new HttpClient(_handler), Options.Create(options), NullLogger<OllamaRephraseClient>.Instance);

	[Fact]
	public async Task Disabled_by_default_makes_no_network_call()
	{
		RephraseResult result = await Client(new OllamaRephraseOptions()).RephraseAsync("hello", "Polish.", CancellationToken.None);

		result.Status.Should().Be(RephraseStatus.Disabled);
		result.Text.Should().Be("hello");
		_handler.SendCount.Should().Be(0);
	}

	[Fact]
	public async Task Enabled_localhost_rephrase_posts_to_the_loopback_generate_endpoint_and_returns_the_text()
	{
		_handler.RespondWithJson("{\"response\":\"polished\",\"done\":true}");

		RephraseResult result = await Client(new OllamaRephraseOptions { Enabled = true, Endpoint = "http://localhost:11434" })
			.RephraseAsync("rough", "Polish.", CancellationToken.None);

		result.Status.Should().Be(RephraseStatus.Rephrased);
		result.Text.Should().Be("polished");
		_handler.LastRequestUri!.IsLoopback.Should().BeTrue();
		_handler.LastRequestUri.AbsolutePath.Should().Be("/api/generate");
	}

	[Fact]
	public async Task A_server_error_degrades_to_the_original_text()
	{
		_handler.RespondWithStatus(HttpStatusCode.InternalServerError);

		RephraseResult result = await Client(new OllamaRephraseOptions { Enabled = true, Endpoint = "http://127.0.0.1:11434" })
			.RephraseAsync("keep me", "Polish.", CancellationToken.None);

		result.Status.Should().Be(RephraseStatus.Failed);
		result.Text.Should().Be("keep me");
	}

	[Fact]
	public async Task A_transport_failure_degrades_to_the_original_text()
	{
		_handler.ThrowOnSend();

		RephraseResult result = await Client(new OllamaRephraseOptions { Enabled = true, Endpoint = "http://localhost:11434" })
			.RephraseAsync("keep me", "Polish.", CancellationToken.None);

		result.Status.Should().Be(RephraseStatus.Failed);
		result.Text.Should().Be("keep me");
	}

	[Fact]
	public async Task A_non_loopback_endpoint_is_never_contacted_even_if_enabled()
	{
		RephraseResult result = await Client(new OllamaRephraseOptions { Enabled = true, Endpoint = "http://ollama.example.com" })
			.RephraseAsync("secret", "Polish.", CancellationToken.None);

		result.Status.Should().Be(RephraseStatus.Failed);
		_handler.SendCount.Should().Be(0);
	}

	[Fact]
	public async Task Caller_cancellation_propagates()
	{
		Func<Task> act = async () => await Client(new OllamaRephraseOptions { Enabled = true, Endpoint = "http://localhost:11434" })
			.RephraseAsync("hello", "Polish.", new CancellationToken(canceled: true));

		await act.Should().ThrowAsync<OperationCanceledException>();
	}

	private sealed class StubHttpMessageHandler : HttpMessageHandler
	{
		private string _json = "{\"response\":\"\"}";
		private HttpStatusCode _status = HttpStatusCode.OK;
		private bool _throw;

		public int SendCount { get; private set; }

		public Uri? LastRequestUri { get; private set; }

		public void RespondWithJson(string json) => _json = json;

		public void RespondWithStatus(HttpStatusCode status) => _status = status;

		public void ThrowOnSend() => _throw = true;

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			SendCount++;
			LastRequestUri = request.RequestUri;

			if (_throw)
			{
				throw new HttpRequestException("connection refused");
			}

			return Task.FromResult(new HttpResponseMessage(_status)
			{
				Content = new StringContent(_json, Encoding.UTF8, "application/json"),
			});
		}
	}
}
