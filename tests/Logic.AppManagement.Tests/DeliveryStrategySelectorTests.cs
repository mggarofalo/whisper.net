// Unit coverage for the WHISPER-8 strategy resolution: the configured default applies when no override
// is given, and a per-delivery override always wins. These map directly to the acceptance criterion's
// required cases (default, override, precedence).

using Domain.Settings;
using Logic.AppManagement;
using Xunit;

namespace Logic.AppManagement.Tests;

public sealed class DeliveryStrategySelectorTests
{
	private readonly DeliveryStrategySelector _selector = new();

	[Theory]
	[InlineData(DeliveryStrategy.Type)]
	[InlineData(DeliveryStrategy.Paste)]
	public void Uses_the_configured_default_when_no_override_is_supplied(DeliveryStrategy configuredDefault) =>
		Assert.Equal(configuredDefault, _selector.Resolve(configuredDefault, overrideStrategy: null));

	[Theory]
	[InlineData(DeliveryStrategy.Type, DeliveryStrategy.Paste)]
	[InlineData(DeliveryStrategy.Paste, DeliveryStrategy.Type)]
	public void Override_takes_precedence_over_the_configured_default(DeliveryStrategy configuredDefault, DeliveryStrategy overrideStrategy) =>
		Assert.Equal(overrideStrategy, _selector.Resolve(configuredDefault, overrideStrategy));
}
