// CQRS query for the audio settings view: lists the capture devices currently available so
// the user can pick one. A read-only request carrying no data; the handler reads the device enumerator
// port and projects to DTOs, marking the OS default.

using Application.Interfaces;

namespace Application.Audio;

public sealed record ListCaptureDevicesQuery : IQuery<IReadOnlyList<AudioDeviceDto>>;
