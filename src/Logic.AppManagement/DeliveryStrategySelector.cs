// Resolves the delivery strategy for a single delivery (WHISPER-8). The rule is pure precedence: an
// explicit per-delivery override wins; absent one, the configured default applies. Keeping this in
// Logic.AppManagement (over the Application abstraction) means the policy is testable in isolation and
// the delivery handler stays free of branching.

using Application.Delivery;
using Domain.Settings;

namespace Logic.AppManagement;

public sealed class DeliveryStrategySelector : IDeliveryStrategySelector
{
	public DeliveryStrategy Resolve(DeliveryStrategy configuredDefault, DeliveryStrategy? overrideStrategy) =>
		overrideStrategy ?? configuredDefault;
}
