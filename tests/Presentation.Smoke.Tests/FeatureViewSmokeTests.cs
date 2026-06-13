// The thin view-level smoke layer. Behavior lives in the WPF-free view-model specs;
// these tests guard only the view glue, on a dedicated STA thread per test:
//  - every registered NavigationSection's view-model type resolves an implicit DataTemplate from the
//    real shell window's resources (a missing template fails, AC2), and
//  - each template's view constructs against its real, scope-resolved view-model and completes its
//    first bind with no exception and no data-binding trace error (a binding-path typo fails, AC1).

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AwesomeAssertions;
using Logic.AppManagement.Shell;
using Presentation.Shell;
using Xunit;

namespace Presentation.Smoke.Tests;

public sealed class FeatureViewSmokeTests
{
	[Fact]
	public void Every_registered_section_has_a_matching_data_template() => StaThread.Run(() =>
	{
		using SmokeScope scope = SmokeScope.Create();
		ShellWindow window = new(scope.Get<ShellViewModel>());

		foreach (NavigationSection section in scope.Sections)
		{
			object? template = window.TryFindResource(new DataTemplateKey(section.ViewModelType));

			template.Should().NotBeNull(
				$"the '{section.Key}' section's view-model ({section.ViewModelType.Name}) must have an " +
				"implicit DataTemplate in the shell resources");
		}
	});

	[Fact]
	public void Each_feature_view_constructs_and_binds_without_errors() => StaThread.Run(() =>
	{
		using BindingErrorCollector bindingErrors = new();
		using SmokeScope scope = SmokeScope.Create();
		ShellWindow window = new(scope.Get<ShellViewModel>());

		// Host inside the real window's tree so each view-model resolves its view exactly as the shell's
		// content region would — implicit DataTemplate from the window's resources, no Application needed.
		ContentControl host = new();
		((Panel)window.Content).Children.Add(host);

		foreach (NavigationSection section in scope.Sections)
		{
			host.Content = scope.Get(section.ViewModelType);
			host.Measure(new Size(800, 600));
			host.Arrange(new Rect(new Size(800, 600)));
			host.UpdateLayout();
			FlushDispatcherQueue();

			string expectedViewName = section.ViewModelType.Name.Replace("ViewModel", "View");
			FindDescendantByTypeName(host, expectedViewName).Should().NotBeNull(
				$"the '{section.Key}' section's template must have instantiated {expectedViewName}");

			bindingErrors.Errors.Should().BeEmpty(
				$"the '{section.Key}' view must complete its first bind cleanly — a binding error here is a " +
				"renamed/mistyped path the WPF-free specs cannot see");
		}
	});

	// Drain queued dispatcher work (deferred bindings, Loaded handlers raised by layout) so every
	// binding has actually run before the error collection is inspected.
	private static void FlushDispatcherQueue()
	{
		DispatcherFrame frame = new();
		Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => frame.Continue = false);
		Dispatcher.PushFrame(frame);
	}

	private static DependencyObject? FindDescendantByTypeName(DependencyObject root, string typeName)
	{
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(root, i);
			if (child.GetType().Name == typeName)
			{
				return child;
			}

			if (FindDescendantByTypeName(child, typeName) is { } match)
			{
				return match;
			}
		}

		return null;
	}
}
