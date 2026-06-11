// Unit depth for the WHISPER-108 first-activation auto-load, beyond the @WHISPER-108 acceptance
// scenarios. Pins the base mechanism on a minimal counting view-model: the hook command runs exactly
// once per cached instance (first activation only — re-activation must never re-query), it runs AFTER
// OnActivated so a section's live subscriptions exist before its first load, and a section that
// declares no load (the default null hook) activates cleanly. (Home grew its own dashboard load in
// WHISPER-106, so the no-load case is pinned on a minimal stub here rather than on a real section.)

using AwesomeAssertions;
using CommunityToolkit.Mvvm.Input;
using Logic.AppManagement.Shell;
using Xunit;

namespace Logic.AppManagement.Tests.Shell;

public sealed class FeatureViewModelFirstActivationTests
{
	[Fact]
	public void First_activation_executes_the_load_exactly_once()
	{
		CountingViewModel viewModel = new();

		viewModel.OnNavigatedTo();

		viewModel.LoadCount.Should().Be(1, "the first activation must trigger the load (WHISPER-108)");
	}

	[Fact]
	public void Reactivation_does_not_reload()
	{
		CountingViewModel viewModel = new();

		viewModel.OnNavigatedTo();
		viewModel.OnNavigatedFrom();
		viewModel.OnNavigatedTo();
		viewModel.OnNavigatedFrom();
		viewModel.OnNavigatedTo();

		viewModel.LoadCount.Should().Be(1, "a cached view-model must not re-query on every tab switch");
	}

	[Fact]
	public void The_load_runs_after_activation()
	{
		CountingViewModel viewModel = new();

		viewModel.OnNavigatedTo();

		viewModel.WasActivatedBeforeLoad.Should().BeTrue(
			"OnActivated registers live subscriptions, which must exist before the first load runs");
	}

	[Fact]
	public void A_section_without_a_load_activates_cleanly()
	{
		PlainViewModel plain = new();

		plain.OnNavigatedTo();

		plain.IsActive.Should().BeTrue("the null default hook must leave plain sections untouched");
	}

	// Internal (not private): the ObservableValidator source generator emits assembly-level code that
	// must be able to reference the type.
	internal sealed class CountingViewModel : FeatureViewModel
	{
		private bool _activated;

		public int LoadCount { get; private set; }

		public bool WasActivatedBeforeLoad { get; private set; }

		public IAsyncRelayCommand LoadCommand { get; }

		public CountingViewModel() =>
			LoadCommand = new AsyncRelayCommand(() =>
			{
				WasActivatedBeforeLoad = _activated;
				LoadCount++;
				return Task.CompletedTask;
			});

		protected override IAsyncRelayCommand FirstActivationLoadCommand => LoadCommand;

		protected override void OnActivated() => _activated = true;
	}

	// A section that declares no first-activation load — the default null hook. Stands in for the old
	// empty Home section (which became a loading dashboard in WHISPER-106).
	internal sealed class PlainViewModel : FeatureViewModel;
}
