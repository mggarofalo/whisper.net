// Boundary projection of a capture device for the audio settings view: its stable id, a
// friendly name to display, and whether it is the current OS default. The view lists these and marks
// the one matching the persisted selection; the special "follow system default" choice is represented
// by AudioDevice.SystemDefault, not an entry here.

namespace Application.Audio;

public sealed record AudioDeviceDto(string Id, string Name, bool IsSystemDefault);
