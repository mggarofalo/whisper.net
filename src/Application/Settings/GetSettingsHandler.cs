// Handles GetSettingsQuery: loads the current settings via the ISettingsStore port and maps them to
// the DTO returned to the caller. Pure orchestration — persistence lives behind the port, mapping
// behind the Mapperly mapper.

using Application.Interfaces;
using Application.Ports;
using Domain.Settings;

namespace Application.Settings;

public sealed class GetSettingsHandler(ISettingsStore store, SettingsMapper mapper)
	: IQueryHandler<GetSettingsQuery, AppSettingsDto>
{
	public async ValueTask<AppSettingsDto> Handle(GetSettingsQuery query, CancellationToken cancellationToken)
	{
		AppSettings settings = await store.LoadAsync(cancellationToken);
		return mapper.ToDto(settings);
	}
}
