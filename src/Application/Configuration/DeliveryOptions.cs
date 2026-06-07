// Strongly-typed binding for text-delivery settings, populated from the "Delivery" configuration
// section by AddApplication. The default strategy is the one used when a delivery supplies no override;
// it defaults to Type (the universal keystroke path) when no configuration is present.

using Domain.Settings;

namespace Application.Configuration;

public sealed class DeliveryOptions
{
	public const string SectionName = "Delivery";

	/// <summary>The delivery strategy used when a delivery does not specify a per-call override.</summary>
	public DeliveryStrategy DefaultStrategy { get; set; } = DeliveryStrategy.Type;
}
