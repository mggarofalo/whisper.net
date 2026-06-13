// Inner TDD loop for the shell view-model caching fix. These pin the navigation contract
// at the NavigationService + DI-scope boundary, WPF-free: feature view-models are resolved scoped, so
// navigating back to a section returns the SAME cached instance (state preserved), navigation only
// toggles activate/deactivate (it never disposes the outgoing view-model), and the cached instances are
// disposed exactly once — by the UI scope — when the shell closes.

using AwesomeAssertions;
using Logic.AppManagement.Shell;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Logic.AppManagement.Tests.Shell;

public sealed class NavigationServiceTests
{
	[Fact]
	public void Navigating_back_returns_the_same_cached_view_model_instance()
	{
		using TestScope scope = new();
		NavigationService navigation = scope.CreateNavigationService();

		navigation.NavigateTo("A");
		object first = navigation.CurrentViewModel!;
		navigation.NavigateTo("B");
		navigation.NavigateTo("A");

		navigation.CurrentViewModel.Should().BeSameAs(first, "a section keeps one instance per shell UI scope");
	}

	[Fact]
	public void Navigating_away_does_not_dispose_the_outgoing_view_model()
	{
		using TestScope scope = new();
		NavigationService navigation = scope.CreateNavigationService();

		navigation.NavigateTo("A");
		TrackingViewModel a = (TrackingViewModel)navigation.CurrentViewModel!;
		navigation.NavigateTo("B");

		a.Disposals.Should().Be(0, "navigating away must not dispose the cached view-model");
		a.Deactivations.Should().Be(1, "navigating away deactivates the outgoing view-model");
	}

	[Fact]
	public void Navigation_activates_the_incoming_and_deactivates_the_outgoing()
	{
		using TestScope scope = new();
		NavigationService navigation = scope.CreateNavigationService();

		navigation.NavigateTo("A");
		TrackingViewModel a = (TrackingViewModel)navigation.CurrentViewModel!;
		navigation.NavigateTo("B");
		OtherViewModel b = (OtherViewModel)navigation.CurrentViewModel!;

		a.Activations.Should().Be(1);
		a.Deactivations.Should().Be(1);
		b.Activations.Should().Be(1);
		b.Deactivations.Should().Be(0);
	}

	[Fact]
	public void Cached_view_models_are_disposed_once_when_the_shell_scope_is_disposed()
	{
		TrackingViewModel a;
		OtherViewModel b;

		using (TestScope scope = new())
		{
			NavigationService navigation = scope.CreateNavigationService();
			navigation.NavigateTo("A");
			a = (TrackingViewModel)navigation.CurrentViewModel!;
			navigation.NavigateTo("B");
			b = (OtherViewModel)navigation.CurrentViewModel!;

			a.Disposals.Should().Be(0, "the scope is still alive");
		}

		a.Disposals.Should().Be(1, "the UI scope disposes each cached view-model exactly once on close");
		b.Disposals.Should().Be(1);
	}

	// A disposable feature view-model that records its lifecycle so the tests can assert it.
	private sealed class TrackingViewModel : IFeatureViewModel, IDisposable
	{
		public int Activations { get; private set; }
		public int Deactivations { get; private set; }
		public int Disposals { get; private set; }

		public void OnNavigatedTo() => Activations++;
		public void OnNavigatedFrom() => Deactivations++;
		public void Dispose() => Disposals++;
	}

	// A second, distinct scoped view-model type so navigation has somewhere to go (one cached instance per
	// type per scope, so the two sections must be different types). Tracks the same lifecycle as the first.
	private sealed class OtherViewModel : IFeatureViewModel, IDisposable
	{
		public int Activations { get; private set; }
		public int Deactivations { get; private set; }
		public int Disposals { get; private set; }

		public void OnNavigatedTo() => Activations++;
		public void OnNavigatedFrom() => Deactivations++;
		public void Dispose() => Disposals++;
	}

	// Wraps a root provider + one UI scope, mirroring how the shell resolves its view-model graph from a
	// single long-lived scope. The view-models are registered scoped, exactly as production registers them.
	private sealed class TestScope : IDisposable
	{
		private readonly ServiceProvider _root;
		private readonly IServiceScope _scope;

		public TestScope()
		{
			ServiceCollection services = new();
			services.AddScoped<TrackingViewModel>();
			services.AddScoped<OtherViewModel>();
			_root = services.BuildServiceProvider();
			_scope = _root.CreateScope();
		}

		public NavigationService CreateNavigationService()
		{
			NavigationSection[] sections =
			[
				new("A", typeof(TrackingViewModel)),
				new("B", typeof(OtherViewModel)),
			];

			return new NavigationService(_scope.ServiceProvider, sections);
		}

		public void Dispose()
		{
			_scope.Dispose();
			_root.Dispose();
		}
	}
}
