// Handles ListMonitorsQuery: returns the attached monitors from the IMonitorCatalog port. Pure
// orchestration — the enumeration lives behind the port (implemented in the Presentation composition
// root). Synchronous under the hood, wrapped in a ValueTask to fit the Mediator query contract.

using Application.Interfaces;
using Application.Ports;

namespace Application.Display;

public sealed class ListMonitorsHandler(IMonitorCatalog catalog)
	: IQueryHandler<ListMonitorsQuery, IReadOnlyList<MonitorInfo>>
{
	public ValueTask<IReadOnlyList<MonitorInfo>> Handle(ListMonitorsQuery query, CancellationToken cancellationToken) =>
		ValueTask.FromResult(catalog.GetMonitors());
}
