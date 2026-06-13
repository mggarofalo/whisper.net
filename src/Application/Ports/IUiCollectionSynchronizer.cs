// The cross-thread collection-binding seam. A view-model that exposes a bound collection
// which may be mutated off the UI thread registers it (with the lock its mutations take) through this
// port at construction — before any view can bind. The WPF implementation enables WPF's collection
// synchronization for the pair; tests substitute a recorder, keeping the view-models WPF-free.

using System.Collections;

namespace Application.Ports;

public interface IUiCollectionSynchronizer
{
	/// <summary>Registers <paramref name="collection"/> for cross-thread binding, guarded by <paramref name="gate"/>.</summary>
	void Enable(IEnumerable collection, object gate);
}
