// Inner TDD loop for the settings/feature view-model foundation (WHISPER-76). These pin the
// foundational contract M12 builds on, WPF-free: every settings/feature view-model is an
// ObservableValidator (so it is both observable and validation-capable), and every one of its
// source-generated [ObservableProperty] members raises INotifyPropertyChanged when it changes — no
// hand-written, magic-string OnPropertyChanged. The outer @WHISPER-76 scenario drives this down.

using System.ComponentModel;
using System.Reflection;
using Application.Ports;
using AwesomeAssertions;
using CommunityToolkit.Mvvm.ComponentModel;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests.Shell;

public sealed class SettingsViewModelObservabilityTests
{
	// Every settings/feature view-model migrated to the validation-capable base in WHISPER-76.
	public static TheoryData<string> MigratedViewModels =>
	[
		nameof(HomeViewModel),
		nameof(ModelViewModel),
		nameof(AudioDeviceViewModel),
		nameof(HotkeyViewModel),
		nameof(HistoryViewModel),
		nameof(StatsViewModel),
	];

	[Theory]
	[MemberData(nameof(MigratedViewModels))]
	public void View_model_is_a_validation_capable_observable(string viewModelName)
	{
		object viewModel = Create(viewModelName);

		viewModel.Should().BeAssignableTo<ObservableValidator>(
			"settings/feature view-models share the validation-capable observable base after WHISPER-76");
	}

	[Theory]
	[MemberData(nameof(MigratedViewModels))]
	public void Every_generated_bindable_property_raises_property_changed(string viewModelName)
	{
		object viewModel = Create(viewModelName);
		List<string?> raised = [];
		((INotifyPropertyChanged)viewModel).PropertyChanged += (_, args) => raised.Add(args.PropertyName);

		PropertyInfo[] bindable = viewModel.GetType()
			.GetProperties(BindingFlags.Instance | BindingFlags.Public)
			.Where(property => property.CanRead && property.CanWrite && property.SetMethod!.IsPublic)
			.ToArray();

		bindable.Should().NotBeEmpty($"{viewModelName} has at least one bindable [ObservableProperty]");

		foreach (PropertyInfo property in bindable)
		{
			raised.Clear();
			property.SetValue(viewModel, DistinctValue(property.PropertyType, property.GetValue(viewModel)));

			raised.Should().Contain(property.Name,
				$"setting {viewModelName}.{property.Name} must raise INotifyPropertyChanged for that property");
		}
	}

	// Construct a view-model with substituted ports — the foundation contract is about change
	// notification and base type, neither of which needs a real Mediator round-trip.
	private static object Create(string viewModelName) => viewModelName switch
	{
		nameof(HomeViewModel) => new HomeViewModel(Substitute.For<IMediator>(), new CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger(), Substitute.For<IUiCollectionSynchronizer>()),
		nameof(ModelViewModel) => new ModelViewModel(Substitute.For<IMediator>()),
		nameof(AudioDeviceViewModel) => new AudioDeviceViewModel(Substitute.For<IMediator>()),
		nameof(HotkeyViewModel) => new HotkeyViewModel(Substitute.For<IMediator>(), new CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger()),
		nameof(HistoryViewModel) => new HistoryViewModel(Substitute.For<IMediator>(), new CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger(), Substitute.For<IUiCollectionSynchronizer>()),
		nameof(StatsViewModel) => new StatsViewModel(Substitute.For<IMediator>()),
		_ => throw new ArgumentOutOfRangeException(nameof(viewModelName), viewModelName, "unknown view-model"),
	};

	// A value guaranteed to differ from the current one, so SetProperty's equality guard does not suppress
	// the change notification we are asserting.
	private static object DistinctValue(Type type, object? current)
	{
		Type underlying = Nullable.GetUnderlyingType(type) ?? type;

		if (underlying == typeof(bool))
		{
			return !(bool)(current ?? false);
		}

		if (underlying == typeof(int))
		{
			return (int)(current ?? 0) + 1;
		}

		if (underlying == typeof(TimeSpan))
		{
			return (TimeSpan)(current ?? TimeSpan.Zero) + TimeSpan.FromSeconds(1);
		}

		if (underlying == typeof(string))
		{
			return (string?)current == "changed-value" ? "other-value" : "changed-value";
		}

		throw new NotSupportedException($"no distinct value rule for {type}");
	}
}
