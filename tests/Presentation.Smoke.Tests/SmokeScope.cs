// The smoke layer's composition (WHISPER-96): the SAME per-layer registration extensions production
// and the BDD specs use (AddApplication + AddAppManagement), plus the real WPF collection synchronizer
// over the inline dispatcher default — so each feature view-model resolves exactly as the shell would
// resolve it, inside one UI scope. No Infrastructure port is faked because no command is executed:
// the smoke layer only constructs and binds.

using Application.DependencyInjection;
using Application.Ports;
using Logic.AppManagement.DependencyInjection;
using Logic.AppManagement.Shell;
using Microsoft.Extensions.DependencyInjection;
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

		// The real WPF collection synchronizer (WHISPER-91) over the AddAppManagement inline-dispatcher
		// default, so HistoryViewModel's registration runs the genuine EnableCollectionSynchronization path.
		services.AddSingleton<IUiCollectionSynchronizer, WpfCollectionSynchronizer>();

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
