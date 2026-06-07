// Payload for IAudioSource.FrameAvailable: one buffer of captured samples plus the format they were
// captured in. The samples are a transient view onto the capture buffer — consumers that retain them
// past the event must copy. Kept in Domain/BCL types only so the port leaks no native dependency.

using Domain.Audio;

namespace Application.Ports;

public sealed class AudioFrameAvailableEventArgs(ReadOnlyMemory<float> samples, CaptureFormat format) : EventArgs
{
	/// <summary>Interleaved float samples for this frame, laid out per <see cref="Format"/>'s channel count.</summary>
	public ReadOnlyMemory<float> Samples { get; } = samples;

	/// <summary>The format the samples were captured in.</summary>
	public CaptureFormat Format { get; } = format;
}
