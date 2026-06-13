// Maps a resolved DeliveryStrategy to the concrete text injector that implements it: the
// SendInput typing path or the clipboard-paste path. Holding both adapters here keeps the strategy ->
// implementation routing in Infrastructure, so the Application handler selects a strategy without any
// awareness of which Win32 mechanism backs it.

using Application.Delivery;
using Application.Ports;
using Domain.Settings;

namespace Infrastructure.TextDelivery;

public sealed class TextInjectorFactory(SendInputTextInjector typing, ClipboardTextInjector paste) : ITextInjectorFactory
{
	public ITextInjector For(DeliveryStrategy strategy) =>
		strategy == DeliveryStrategy.Paste ? paste : typing;
}
