// Payload for IDefaultDeviceWatcher.DefaultChanged: the id of the new OS default capture device (null
// if there is no longer a default). Domain/BCL types only, so the port leaks no native dependency.

namespace Application.Ports;

public sealed class DefaultDeviceChangedEventArgs(string? newDefaultId) : EventArgs
{
	/// <summary>The id of the new default capture device, or <c>null</c> if none remains.</summary>
	public string? NewDefaultId { get; } = newDefaultId;
}
