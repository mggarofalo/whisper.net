// Port for capturing microphone audio. Implemented in Infrastructure (NAudio/WASAPI in Module 2);
// faked in the BDD specs so the dictation pipeline can be driven without a real input device.

using Domain.Audio;

namespace Application.Ports;

/// <summary>
/// Captures audio from the active input device for a single push-to-talk recording.
/// </summary>
/// <remarks>
/// <see cref="Start"/> opens the device and begins buffering; <see cref="StopAsync"/> stops and
/// returns the captured clip. Implementations are not required to be thread-safe — the state manager
/// owns the start/stop sequence and calls them from a single logical flow.
/// </remarks>
public interface IAudioSource
{
	/// <summary>Begins capturing from the active input device.</summary>
	void Start();

	/// <summary>Stops capturing and returns the buffered clip.</summary>
	ValueTask<AudioClip> StopAsync(CancellationToken cancellationToken);
}
