// The WPF half of the collection-sync seam: enables WPF's collection synchronization for
// a (collection, gate) pair so the binding engine reads the collection under the same lock its
// mutations take. Registration must run on the UI thread before the collection participates in a
// binding — view-models register at construction, which the navigation flow performs on the UI thread,
// so the IUiDispatcher CheckAccess fast-path applies (with a posted fallback for off-thread callers).

using System.Collections;
using System.Windows.Data;
using Application.Ports;

namespace Presentation.Threading;

public sealed class WpfCollectionSynchronizer(IUiDispatcher dispatcher) : IUiCollectionSynchronizer
{
	public void Enable(IEnumerable collection, object gate)
	{
		if (dispatcher.CheckAccess())
		{
			BindingOperations.EnableCollectionSynchronization(collection, gate);
			return;
		}

		dispatcher.Post(() => BindingOperations.EnableCollectionSynchronization(collection, gate));
	}
}
