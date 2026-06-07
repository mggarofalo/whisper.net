// Resolves which delivery strategy a single delivery should use. The decision is pure precedence — an
// explicit per-delivery override wins, otherwise the configured default — and lives in Logic.AppManagement
// (the app-management behavior layer). Defined here in Application so the delivery handler can depend on
// it without referencing Logic; the configured default is supplied by the caller (from DeliveryOptions).

using Domain.Settings;

namespace Application.Delivery;

public interface IDeliveryStrategySelector
{
	/// <summary>Returns <paramref name="overrideStrategy"/> when supplied, otherwise <paramref name="configuredDefault"/>.</summary>
	DeliveryStrategy Resolve(DeliveryStrategy configuredDefault, DeliveryStrategy? overrideStrategy);
}
