// The shell's navigation implementation (WHISPER-19). It maps the registered section keys to their
// view-model types and, on NavigateTo, resolves the target from the DI container, deactivates the
// outgoing view-model, then activates the incoming one. The feature view-models are registered SCOPED
// (WHISPER-89), so resolving from the container returns the one cached instance per shell UI scope:
// navigating back to a section restores its state (selection, page, scroll) instead of rebuilding it.
// Navigation therefore only toggles activate/deactivate — it never recreates or disposes a view-model;
// the cached instances are disposed once, by the UI scope, when the shell closes. Resolving from the
// container is what keeps the view-models DI-composed (so a feature view-model gets its real IMediator).
// Scoped, like the orchestrator, because the view-models it resolves depend on the scoped Mediator — so
// it runs inside one UI scope, never the root, avoiding a captive scoped dependency.

using Microsoft.Extensions.DependencyInjection;

namespace Logic.AppManagement.Shell;

public sealed class NavigationService : INavigationService
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

		// Resolve the section's view-model from the container. Registered scoped, so this is the one cached
		// instance for this shell UI scope — its state is preserved across navigation, not rebuilt.
		object next = _services.GetRequiredService(section.ViewModelType);
		if (ReferenceEquals(next, CurrentViewModel))
		{
			return;
		}

		// Toggle activation only: quiesce the outgoing view-model but never dispose it — the cached
		// instances are owned by the UI scope and disposed once, when the shell closes.
		(CurrentViewModel as IFeatureViewModel)?.OnNavigatedFrom();
		CurrentViewModel = next;
		(next as IFeatureViewModel)?.OnNavigatedTo();
		CurrentViewModelChanged?.Invoke(this, EventArgs.Empty);
	}
}
