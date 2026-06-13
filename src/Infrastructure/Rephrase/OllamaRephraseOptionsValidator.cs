// Startup validation for the rephrase feature. When rephrase is enabled, the configured
// endpoint MUST be loopback — a remote host is rejected here (fail-fast at startup) rather than silently
// used, so transcript text can never be sent off the machine. When rephrase is disabled the endpoint is
// irrelevant and not checked.

using Microsoft.Extensions.Options;

namespace Infrastructure.Rephrase;

public sealed class OllamaRephraseOptionsValidator : IValidateOptions<OllamaRephraseOptions>
{
	public ValidateOptionsResult Validate(string? name, OllamaRephraseOptions options)
	{
		if (!options.Enabled)
		{
			return ValidateOptionsResult.Success;
		}

		if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out Uri? endpoint))
		{
			return ValidateOptionsResult.Fail($"AI rephrase endpoint '{options.Endpoint}' is not a valid absolute URI.");
		}

		if (!endpoint.IsLoopback)
		{
			return ValidateOptionsResult.Fail(
				$"AI rephrase endpoint must be localhost only; '{endpoint.Host}' is not a loopback host.");
		}

		return ValidateOptionsResult.Success;
	}
}
