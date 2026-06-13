// The smoke layer's composition: the SAME per-layer registration extensions production
// and the BDD specs use (AddApplication + AddAppManagement), plus the real WPF collection synchronizer
// over the inline dispatcher default — so each feature view-model resolves exactly as the shell would
// resolve it, inside one UI scope.
//
// Constructing the ShellViewModel is NOT side-effect-free: its constructor navigates to the first
// section (Home, which refreshes on activation) and loads the persisted theme
//. Both run read queries through Mediator (GetSettings / usage / history). Those are
// fire-and-forget commands, so a missing port doesn't fail a test directly — it throws on a background
// continuation, surfacing as an xUnit "Catastrophic failure" that fails the whole run. So the smoke
// scope substitutes the read ports that activation path touches with contract-honoring fakes (load
// returns the fresh-install defaults; history is empty), exactly as the BDD specs' TestDependencies do.

using Application.DependencyInjection;
using Application.Ports;
using Domain.Settings;
using Logic.AppManagement.DependencyInjection;
using Logic.AppManagement.Shell;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Presentation.Threading;

namespace Presentation.Smoke.Tests;

internal sealed class SmokeScope : IDisposable
{
	private readonly ServiceProvider _provider;
	private readonly IServiceScope _scope;

	private SmokeScope(ServiceProvider provider)
	{
		_provider = provider;
		_scope = provider.CreateScope();
		Sections = [.. provider.GetServices<NavigationSection>()];
	}

	public IReadOnlyList<NavigationSection> Sections { get; }

	public static SmokeScope Create()
	{
		ServiceCollection services = new();
		services.AddLogging();
		services.AddApplication();
		services.AddAppManagement();

		// The real WPF collection synchronizer over the AddAppManagement inline-dispatcher
		// default, so HistoryViewModel's registration runs the genuine EnableCollectionSynchronization path.
		services.AddSingleton<IUiCollectionSynchronizer, WpfCollectionSynchronizer>();

		// The read ports the ShellViewModel's construction-time activation touches (see header). The
		// settings store loads the fresh-install defaults (never null), and the history store reads back an
		// empty log — the same contract-honoring defaults a fresh install presents, so the dashboard's
		// activation refresh and the theme load both compose cleanly instead of leaking a background failure.
		services.AddScoped(_ =>
		{
			ISettingsStore store = Substitute.For<ISettingsStore>();
			store.LoadAsync(Arg.Any<CancellationToken>()).Returns(AppSettings.Default);
			return store;
		});
		services.AddScoped(_ =>
		{
			IHistoryStore store = Substitute.For<IHistoryStore>();
			store.GetEntriesAsync(Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
				.Returns(Array.Empty<Domain.History.TranscriptEntry>());
			return store;
		});

		return new SmokeScope(services.BuildServiceProvider());
	}

	public T Get<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

	public object Get(Type serviceType) => _scope.ServiceProvider.GetRequiredService(serviceType);

	public void Dispose()
	{
		_scope.Dispose();
		_provider.Dispose();
	}
}
