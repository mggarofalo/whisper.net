// Verifies the Application composition entry point: AddApplication registers the
// Mediator with a scoped lifetime and produces a resolvable IMediator.

using Application.DependencyInjection;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Application.Tests;

public sealed class AddApplicationTests
{
	[Fact]
	public void Registers_mediator_with_scoped_lifetime()
	{
		ServiceCollection services = new();

		services.AddApplication();

		ServiceDescriptor mediator = Assert.Single(services, d => d.ServiceType == typeof(IMediator));
		Assert.Equal(ServiceLifetime.Scoped, mediator.Lifetime);
	}

	[Fact]
	public void Resolves_a_usable_mediator()
	{
		ServiceCollection services = new();
		services.AddApplication();

		using ServiceProvider provider = services.BuildServiceProvider();
		using IServiceScope scope = provider.CreateScope();

		Assert.NotNull(scope.ServiceProvider.GetService<IMediator>());
	}
}
