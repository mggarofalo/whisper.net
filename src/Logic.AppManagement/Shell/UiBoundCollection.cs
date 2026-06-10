// The sanctioned bound-collection helper (WHISPER-91): an ObservableCollection whose every mutation
// takes its own Gate, so a registered view (WPF's collection synchronization reads under the same
// gate) can be updated from any thread without a cross-thread exception. List-bearing view-models
// expose one of these and register it through IUiCollectionSynchronizer at construction — see
// docs/architecture.md ("Background-thread collection updates") for the convention.

using System.Collections.ObjectModel;
using Application.Ports;

namespace Logic.AppManagement.Shell;

public sealed class UiBoundCollection<T> : ObservableCollection<T>
{
	/// <summary>The lock guarding every mutation — the same object registered for cross-thread binding.</summary>
	public object Gate { get; } = new();

	protected override void InsertItem(int index, T item)
	{
		lock (Gate)
		{
			base.InsertItem(index, item);
		}
	}

	protected override void RemoveItem(int index)
	{
		lock (Gate)
		{
			base.RemoveItem(index);
		}
	}

	protected override void SetItem(int index, T item)
	{
		lock (Gate)
		{
			base.SetItem(index, item);
		}
	}

	protected override void MoveItem(int oldIndex, int newIndex)
	{
		lock (Gate)
		{
			base.MoveItem(oldIndex, newIndex);
		}
	}

	protected override void ClearItems()
	{
		lock (Gate)
		{
			base.ClearItems();
		}
	}
}

public static class UiCollectionSynchronizerExtensions
{
	/// <summary>Registers a <see cref="UiBoundCollection{T}"/> with its own gate for cross-thread binding.</summary>
	public static void Enable<T>(this IUiCollectionSynchronizer synchronizer, UiBoundCollection<T> collection) =>
		synchronizer.Enable(collection, collection.Gate);
}
