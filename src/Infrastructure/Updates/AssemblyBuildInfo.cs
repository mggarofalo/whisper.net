// Implements IBuildInfo over the MinVer-stamped assembly informational version (Domain.BuildVersion), the
// single source of truth for the product version.

using Application.Ports;
using Domain;

namespace Infrastructure.Updates;

public sealed class AssemblyBuildInfo : IBuildInfo
{
	public string InformationalVersion => BuildVersion.Informational;
}
