// Per-layer DI registration for the Application layer. Wires the source-generated Mediator (scoped),
// installs the ValidationBehavior into its pipeline, registers every FluentValidation validator in
// this assembly, and binds the layered configuration to strongly-typed options. This is the single
// registration entry point the Generic Host and the BDD specs both call (WHISPER-57), so production
// and test composition cannot drift.

using Application.Behaviors;
using Application.Configuration;
using Application.Interfaces;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
	public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration? configuration = null)
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

		// Mapperly mappers are stateless; register them as singletons so handlers can inject them.
		services.AddSingleton<Settings.SettingsMapper>();
		services.AddSingleton<History.HistoryMapper>();
		services.AddSingleton<Statistics.UsageStatsMapper>();

		// Register options with their defaults so they always resolve, then bind from the layered
		// configuration when it is available. DeliveryOptions must always resolve because the delivery
		// handler depends on it (the default strategy is Type when nothing is configured).
		services.AddOptions<DeliveryOptions>();

		// History retention (WHISPER-17): always resolvable so the record handler can enforce the limit;
		// the default cap applies when nothing is configured.
		services.AddOptions<RetentionOptions>();
		if (configuration is not null)
		{
			services.Configure<GeneralOptions>(configuration.GetSection(GeneralOptions.SectionName));
			services.Configure<DeliveryOptions>(configuration.GetSection(DeliveryOptions.SectionName));
			services.Configure<RetentionOptions>(configuration.GetSection(RetentionOptions.SectionName));
		}

		// Post-process configuration (WHISPER-41): a single live holder, seeded from the "PostProcess"
		// section when configuration is present and otherwise from defaults. Held (not IOptions-captured)
		// so the ConfigurePostProcessing command can update it and the pipeline sees the change next call.
		PostProcessOptions seed = configuration?.GetSection(PostProcessOptions.SectionName).Get<PostProcessOptions>()
			?? PostProcessOptions.Default;
		services.AddSingleton(new PostProcessSettingsHolder { Current = seed });

		return services;
	}
}
