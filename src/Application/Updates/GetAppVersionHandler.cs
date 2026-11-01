// Handles GetAppVersionQuery: reads the build's informational version through the IBuildInfo port and
// formats it for display.

using Application.Interfaces;
using Application.Ports;

namespace Application.Updates;

public sealed class GetAppVersionHandler(IBuildInfo buildInfo) : IQueryHandler<GetAppVersionQuery, string>
{
	public ValueTask<string> Handle(GetAppVersionQuery query, CancellationToken cancellationToken) =>
		ValueTask.FromResult(AppVersionFormatter.Format(buildInfo.InformationalVersion));
}
