// The faked delivery routing for the specs. It exposes the two injector substitutes — typing and paste
// — and hands back the one matching a strategy, so a scenario can assert which delivery path the
// pipeline chose by checking which injector received the text.

using Application.Delivery;
using Application.Ports;
using Domain.Settings;
using NSubstitute;

namespace Dictation.Specs.Support;

public sealed class FakeTextInjectorFactory : ITextInjectorFactory
{
	public ITextInjector Typing { get; } = Substitute.For<ITextInjector>();

	public ITextInjector Paste { get; } = Substitute.For<ITextInjector>();

	public ITextInjector For(DeliveryStrategy strategy) =>
		strategy == DeliveryStrategy.Paste ? Paste : Typing;
}
