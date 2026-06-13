// Handles ListCaptureDevicesQuery: reads the active capture devices from the enumerator
// port and projects each to a DTO, flagging the one that is the OS default. Pure projection over the
// port — the real NAudio enumeration stays in Infrastructure.

using Application.Interfaces;
using Application.Ports;
using Domain.Audio;

namespace Application.Audio;

public sealed class ListCaptureDevicesHandler(IAudioDeviceEnumerator enumerator)
	: IQueryHandler<ListCaptureDevicesQuery, IReadOnlyList<AudioDeviceDto>>
{
	public ValueTask<IReadOnlyList<AudioDeviceDto>> Handle(ListCaptureDevicesQuery query, CancellationToken cancellationToken)
	{
		string? defaultId = enumerator.GetSystemDefaultId();

		IReadOnlyList<AudioDeviceDto> devices = enumerator.GetCaptureDevices()
			.Select(device => new AudioDeviceDto(device.Id, device.Name, string.Equals(device.Id, defaultId, StringComparison.Ordinal)))
			.ToList();

		return ValueTask.FromResult(devices);
	}
}
