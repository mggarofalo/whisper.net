// The shell's navigation implementation (WHISPER-19). It maps the registered section keys to their
// view-model types and, on NavigateTo, resolves the target from the DI container, deactivates and
// disposes the outgoing view-model, then activates the incoming one. Resolving from the container is
// what keeps the view-models DI-composed (so a feature view-model gets its real IMediator) and lets the
// shell stay open to new sections without code changes here. Scoped, like the orchestrator, because the
// view-models it resolves depend on the scoped Mediator — so it runs inside one UI scope, never the
// root, avoiding a captive scoped dependency.

using Microsoft.Extensions.DependencyInjection;

namespace Logic.AppManagement.Shell;

public sealed class NavigationService : INavigationService, IDisposable
{
	private readonly IServiceProvider _services;
	private readonly IReadOnlyDictionary<string, NavigationSection> _sections;

	public NavigationService(IServiceProvider services, IEnumerable<NavigationSection> sections)
	{
		_services = services;

		// Preserve registration order for the nav region while indexing by key for lookup. The dictionary
		// is case-insensitive so a nav button labelled "Model" resolves regardless of casing.
		List<NavigationSection> ordered = sections.ToList();
		_sections = ordered.ToDictionary(section => section.Key, StringComparer.OrdinalIgnoreCase);
		Sections = ordered.Select(section => section.Key).ToList();
	}

	public object? CurrentViewModel { get; private set; }

	public IReadOnlyList<string> Sections { get; }

	public event EventHandler? CurrentViewModelChanged;

	public void NavigateTo(string sectionKey)
	{
		if (!_sections.TryGetValue(sectionKey, out NavigationSection? section))
		{
			throw new ArgumentException($"No navigation section is registered for key '{sectionKey}'.", nameof(sectionKey));
		}

		// Resolve a fresh view-model from the container so it is fully DI-composed (its IMediator, etc.).
		object next = _services.GetRequiredService(section.ViewModelType);
		if (ReferenceEquals(next, CurrentViewModel))
		{
			return;
		}

		Deactivate(CurrentViewModel);
		CurrentViewModel = next;
		(next as IFeatureViewModel)?.OnNavigatedTo();
		CurrentViewModelChanged?.Invoke(this, EventArgs.Empty);
	}

	// Quiesce the outgoing view-model: tell it that it is no longer active, then dispose it if it holds
	// resources, so only the live section retains anything.
	private static void Deactivate(object? viewModel)
	{
		(viewModel as IFeatureViewModel)?.OnNavigatedFrom();
		(viewModel as IDisposable)?.Dispose();
	}

	public void Dispose() => Deactivate(CurrentViewModel);
}
