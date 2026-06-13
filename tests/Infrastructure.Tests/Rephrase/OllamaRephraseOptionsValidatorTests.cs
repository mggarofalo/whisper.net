// Inner TDD loop for the rephrase options validator: a disabled feature is always valid;
// an enabled feature must target a loopback endpoint, and a remote (or malformed) endpoint is rejected.

using AwesomeAssertions;
using Infrastructure.Rephrase;
using Microsoft.Extensions.Options;
using Xunit;

namespace Infrastructure.Tests.Rephrase;

public sealed class OllamaRephraseOptionsValidatorTests
{
	private readonly OllamaRephraseOptionsValidator _validator = new();

	private ValidateOptionsResult Validate(OllamaRephraseOptions options) => _validator.Validate(Options.DefaultName, options);

	[Fact]
	public void A_disabled_feature_is_valid_regardless_of_endpoint()
	{
		Validate(new OllamaRephraseOptions { Enabled = false, Endpoint = "http://ollama.example.com" })
			.Succeeded.Should().BeTrue();
	}

	[Theory]
	[InlineData("http://localhost:11434")]
	[InlineData("http://127.0.0.1:11434")]
	[InlineData("http://[::1]:11434")]
	public void An_enabled_feature_with_a_loopback_endpoint_is_valid(string endpoint)
	{
		Validate(new OllamaRephraseOptions { Enabled = true, Endpoint = endpoint }).Succeeded.Should().BeTrue();
	}

	[Fact]
	public void An_enabled_feature_with_a_remote_endpoint_is_rejected()
	{
		ValidateOptionsResult result = Validate(new OllamaRephraseOptions { Enabled = true, Endpoint = "http://ollama.example.com" });

		result.Failed.Should().BeTrue();
		result.FailureMessage.Should().Contain("localhost only");
	}

	[Fact]
	public void An_enabled_feature_with_a_malformed_endpoint_is_rejected()
	{
		Validate(new OllamaRephraseOptions { Enabled = true, Endpoint = "not a uri" }).Failed.Should().BeTrue();
	}
}
