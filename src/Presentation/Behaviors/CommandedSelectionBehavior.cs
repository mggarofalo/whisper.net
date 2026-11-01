// A reusable behavior for a ListBox whose selection is a committed value the view-model owns (the model in use,
// say), bound ONE WAY to SelectedItem. Choosing an item — a click, or Space / Enter on the focused row — runs
// Command with that item instead of moving the selection; the selection then snaps back to the bound value, so
// it moves only when the view-model commits the choice (a switch that downloads first, or fails, never shows as
// selected). The arrow keys and Home / End move keyboard focus without choosing, so browsing the list cannot
// start work. A selection change that only brings SelectedItem in line with the bound value is not a choice.

using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Xaml.Behaviors;

namespace Presentation.Behaviors;

public sealed class CommandedSelectionBehavior : Behavior<ListBox>
{
	public static readonly System.Windows.DependencyProperty CommandProperty =
		System.Windows.DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(CommandedSelectionBehavior));

	/// <summary>Run with the chosen item.</summary>
	public ICommand? Command
	{
		get => (ICommand?)GetValue(CommandProperty);
		set => SetValue(CommandProperty, value);
	}

	protected override void OnAttached()
	{
		AssociatedObject.SelectionChanged += OnSelectionChanged;
		AssociatedObject.PreviewKeyDown += OnPreviewKeyDown;
	}

	protected override void OnDetaching()
	{
		AssociatedObject.SelectionChanged -= OnSelectionChanged;
		AssociatedObject.PreviewKeyDown -= OnPreviewKeyDown;
	}

	private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (e.AddedItems is [object chosen] && !Equals(chosen, CommittedValue()))
		{
			Choose(chosen);
		}

		RestoreCommittedSelection();
	}

	private void OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		// Keys pressed inside a row's own buttons belong to those buttons.
		if (e.OriginalSource is not ListBoxItem current)
		{
			return;
		}

		if (e.Key is Key.Enter or Key.Space)
		{
			Choose(current.DataContext);
			e.Handled = true;
			return;
		}

		ListBox list = AssociatedObject;
		int index = list.ItemContainerGenerator.IndexFromContainer(current);
		int last = list.Items.Count - 1;
		int target = e.Key switch
		{
			Key.Up => Math.Max(0, index - 1),
			Key.Down => Math.Min(last, index + 1),
			Key.Home => 0,
			Key.End => last,
			_ => -1,
		};

		if (target < 0)
		{
			return;
		}

		list.ScrollIntoView(list.Items[target]);
		(list.ItemContainerGenerator.ContainerFromIndex(target) as ListBoxItem)?.Focus();
		e.Handled = true;
	}

	private void Choose(object item)
	{
		if (Command is { } command && command.CanExecute(item))
		{
			command.Execute(item);
		}
	}

	// The value SelectedItem is bound to, read from its source: the committed choice.
	private object? CommittedValue()
	{
		BindingExpression? binding = BindingOperations.GetBindingExpression(AssociatedObject, Selector.SelectedItemProperty);
		return binding is { ResolvedSource: { } source, ResolvedSourcePropertyName: { } name }
			? TypeDescriptor.GetProperties(source)[name]?.GetValue(source)
			: null;
	}

	// Re-read the bound (committed) value into SelectedItem. Deferred, because a Selector ignores a selection
	// change made while it is still raising one; UpdateTarget keeps the one-way binding in place.
	private void RestoreCommittedSelection() =>
		AssociatedObject.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
			BindingOperations.GetBindingExpression(AssociatedObject, Selector.SelectedItemProperty)?.UpdateTarget());
}
