// Maps a resolved delivery strategy to the text injector that implements it, so the delivery handler
// can route a delivery without knowing which concrete adapter (typing vs clipboard paste) — or any
// Win32 detail — backs each strategy. Implemented in Infrastructure over the two ITextInjector adapters.

using Application.Ports;
using Domain.Settings;

namespace Application.Delivery;

public interface ITextInjectorFactory
{
	/// <summary>Returns the text injector that delivers via <paramref name="strategy"/>.</summary>
	ITextInjector For(DeliveryStrategy strategy);
}
