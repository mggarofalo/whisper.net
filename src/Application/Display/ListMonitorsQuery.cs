// CQRS query to list the attached display monitors for the overlay's target-monitor picker. A read-only
// request carrying no data; the handler resolves the IMonitorCatalog port. Kept as a query (not a direct
// port call from the view-model) so the WPF-free General view-model stays behind IMediator like every
// other section.

using Application.Interfaces;

namespace Application.Display;

public sealed record ListMonitorsQuery : IQuery<IReadOnlyList<MonitorInfo>>;
