// Smoke for the ListBox glue behind the WHISPER-149 model list, on a real WPF ListBox. Choosing a row must run
// the command and leave the selection on the committed (bound) value until the source moves it; a selection
// change that only follows the bound value must not count as a choice.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using AwesomeAssertions;
using Microsoft.Xaml.Behaviors;
using Presentation.Behaviors;
using Xunit;

namespace Presentation.Smoke.Tests;

public sealed class CommandedSelectionBehaviorSmokeTests
{
	[Fact]
	public void Choosing_a_row_runs_the_command_and_keeps_the_committed_selection() => StaThread.Run(() =>
	{
		(ListBox list, Source source, List<object?> chosen) = Build();

		// What a click does: the Selector moves its own selection.
		list.SetCurrentValue(System.Windows.Controls.Primitives.Selector.SelectedItemProperty, "b");
		Flush();

		chosen.Should().Equal("b");
		list.SelectedItem.Should().Be("a", "the selection stays on the committed value until the source moves it");
		BindingOperations.GetBindingExpression(list, System.Windows.Controls.Primitives.Selector.SelectedItemProperty)
			.Should().NotBeNull("restoring the selection keeps the one-way binding in place");

		// The view-model commits the choice: the selection follows, and that is not a second choice.
		source.Committed = "b";
		Flush();

		list.SelectedItem.Should().Be("b");
		chosen.Should().Equal("b");
	});

	private static (ListBox List, Source Source, List<object?> Chosen) Build()
	{
		Source source = new() { Committed = "a" };
		List<object?> chosen = [];
		ListBox list = new() { ItemsSource = new[] { "a", "b", "c" }, DataContext = source };
		list.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedItemProperty,
			new Binding(nameof(Source.Committed)) { Mode = BindingMode.OneWay });
		Interaction.GetBehaviors(list).Add(new CommandedSelectionBehavior { Command = new Recorder(chosen) });
		Flush();
		return (list, source, chosen);
	}

	private static void Flush()
	{
		DispatcherFrame frame = new();
		Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => frame.Continue = false);
		Dispatcher.PushFrame(frame);
	}

	private sealed class Source : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler? PropertyChanged;

		public object? Committed
		{
			get;
			set
			{
				field = value;
				OnPropertyChanged();
			}
		}

		private void OnPropertyChanged([CallerMemberName] string? name = null) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	private sealed class Recorder(List<object?> chosen) : ICommand
	{
		public event EventHandler? CanExecuteChanged { add { } remove { } }

		public bool CanExecute(object? parameter) => true;

		public void Execute(object? parameter) => chosen.Add(parameter);
	}
}
