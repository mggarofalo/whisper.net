// The reusable focus-on-activate behavior (WHISPER-93): attached declaratively to a feature view's
// primary control, it moves keyboard focus there when the view loads — which happens on every section
// activation, since the shell re-renders the view per navigation while the view-model stays cached.
// This replaces the per-view "Loaded +=" focus hack pattern: the Loaded subscription lives once, here,
// managed by the behavior's attach/detach lifecycle, never in a view's code-behind.

using System.Windows;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;

namespace Presentation.Behaviors;

public sealed class FocusOnActivateBehavior : Behavior<FrameworkElement>
{
	protected override void OnAttached() => AssociatedObject.Loaded += OnLoaded;

	protected override void OnDetaching() => AssociatedObject.Loaded -= OnLoaded;

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		if (AssociatedObject.Focusable)
		{
			AssociatedObject.Focus();
			return;
		}

		// A non-focusable host (e.g. a UserControl wrapping its input element): focus the first
		// focusable element inside it instead.
		AssociatedObject.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
	}
}
