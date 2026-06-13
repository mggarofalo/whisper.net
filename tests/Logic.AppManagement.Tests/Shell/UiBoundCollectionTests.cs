// Unit depth for the collection-sync pattern, beyond the acceptance scenarios.
// Pins that EVERY mutation kind on UiBoundCollection holds the Gate while its change notification
// fires (the contract WPF's collection synchronization relies on), and that HistoryViewModel registers
// its Entries with the gate through the seam at construction.

using System.Collections.Specialized;
using Application.Ports;
using AwesomeAssertions;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests.Shell;

public sealed class UiBoundCollectionTests
{
	[Fact]
	public void Every_mutation_holds_the_gate_while_notifying()
	{
		UiBoundCollection<string> collection = ["seed"];
		List<NotifyCollectionChangedAction> actionsUnderLock = [];

		collection.CollectionChanged += (_, e) =>
		{
			Monitor.IsEntered(collection.Gate).Should().BeTrue($"the {e.Action} mutation must hold the gate");
			actionsUnderLock.Add(e.Action);
		};

		collection.Add("added");
		collection[0] = "replaced";
		collection.Move(0, 1);
		collection.RemoveAt(0);
		collection.Clear();

		actionsUnderLock.Should().Equal(
			NotifyCollectionChangedAction.Add,
			NotifyCollectionChangedAction.Replace,
			NotifyCollectionChangedAction.Move,
			NotifyCollectionChangedAction.Remove,
			NotifyCollectionChangedAction.Reset);
	}

	[Fact]
	public void History_view_model_registers_entries_with_their_gate_at_construction()
	{
		IUiCollectionSynchronizer synchronizer = Substitute.For<IUiCollectionSynchronizer>();

		HistoryViewModel viewModel = new(Substitute.For<IMediator>(), new CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger(), synchronizer);

		synchronizer.Received(1).Enable(viewModel.Entries, viewModel.Entries.Gate);
	}
}
