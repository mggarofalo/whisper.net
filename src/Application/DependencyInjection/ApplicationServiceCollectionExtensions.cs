// Per-layer DI registration for the Application layer. Wires the source-generated Mediator (scoped),
// installs the ValidationBehavior into its pipeline, and registers every FluentValidation validator
// in this assembly. This is the single registration entry point the Generic Host and the BDD specs
// both call (WHISPER-57), so production and test composition cannot drift.

using Application.Behaviors;
using Application.Interfaces;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
	public static IServiceCollection AddApplication(this IServiceCollection services)
	{
		// Source-generated Mediator: scoped lifetime, scan this assembly for handlers, and run every
		// request through ValidationBehavior before its handler.
		services.AddMediator(options =>
		{
			options.ServiceLifetime = ServiceLifetime.Scoped;
			options.Assemblies = [typeof(ICommand<>)];
			options.PipelineBehaviors = [typeof(ValidationBehavior<,>)];
		});

		services.AddValidatorsFromAssembly(typeof(ICommand<>).Assembly, includeInternalTypes: true);

		return services;
	}
}
