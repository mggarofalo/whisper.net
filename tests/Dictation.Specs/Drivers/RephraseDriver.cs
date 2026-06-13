// Drives the rephrase scenarios against the REAL OllamaRephraseClient and OllamaRephraseOptionsValidator
// over a recording HTTP transport — no real Ollama, no socket. It proves the opt-in gate (disabled ->
// no call), the loopback-only validation (a remote host is rejected), the happy path (a request to a
// loopback endpoint, rewritten text), and graceful degradation when the backend fails.

using Application.Rephrase;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Infrastructure.Rephrase;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dictation.Specs.Drivers;

public sealed class RephraseDriver
{
	private readonly RecordingHttpMessageHandler _handler = new();
	private OllamaRephraseOptions _options = new();
	private RephraseResult? _result;
	private ValidateOptionsResult? _validation;

	public void NeverEnabled() => _options = new OllamaRephraseOptions { Enabled = false };

	public void Enable() => _options = new OllamaRephraseOptions { Enabled = true };

	public void UseEndpointHost(string host) => _options.Endpoint = $"http://{host}:11434";

	public void EnableAgainstLocalReturning(string responseText)
	{
		_options = new OllamaRephraseOptions { Enabled = true, Endpoint = "http://localhost:11434" };
		_handler.RespondWith(responseText);
	}

	public void EnableAgainstFailingLocal()
	{
		_options = new OllamaRephraseOptions { Enabled = true, Endpoint = "http://localhost:11434" };
		_handler.FailWithServerError();
	}

	public async Task Rephrase(string text) =>
		_result = await Client().RephraseAsync(text, "Polish this.", CancellationToken.None);

	public void ValidateConfiguration() =>
		_validation = new OllamaRephraseOptionsValidator().Validate(Options.DefaultName, _options);

	private OllamaRephraseClient Client() =>
		new(new HttpClient(_handler), Options.Create(_options), NullLogger<OllamaRephraseClient>.Instance);

	public void AssertNoNetworkCall() => _handler.SendCount.Should().Be(0);

	public void AssertDisabledResult() => _result!.Status.Should().Be(RephraseStatus.Disabled);

	public void AssertValidationFailedLocalhostOnly()
	{
		_validation!.Failed.Should().BeTrue();
		_validation.FailureMessage.Should().Contain("localhost only");
	}

	public void AssertRequestWentToLoopback() => _handler.LastRequestUri!.IsLoopback.Should().BeTrue();

	public void AssertRephrasedTo(string expected)
	{
		_result!.Status.Should().Be(RephraseStatus.Rephrased);
		_result.Text.Should().Be(expected);
	}

	public void AssertDegradedToOriginal(string expected)
	{
		_result!.Status.Should().Be(RephraseStatus.Failed);
		_result.Text.Should().Be(expected);
	}
}
