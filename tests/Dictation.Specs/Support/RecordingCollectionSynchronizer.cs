// A recording IUiCollectionSynchronizer for the @WHISPER-91 scenarios (and any spec resolving a
// list-bearing view-model): captures each (collection, gate) registration so specs assert a bound
// collection was registered with its lock at construction — before any view could bind — without WPF.

using System.Collections;
using Application.Ports;

namespace Dictation.Specs.Support;

public sealed class RecordingCollectionSynchronizer : IUiCollectionSynchronizer
{
	public List<(IEnumerable Collection, object Gate)> Registrations { get; } = [];

	public void Enable(IEnumerable collection, object gate) => Registrations.Add((collection, gate));
}
