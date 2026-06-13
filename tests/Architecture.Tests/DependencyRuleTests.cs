// Enforces the clean-architecture dependency direction at test time. Each test maps to
// one rule from the acceptance criteria; NetArchTest inspects the compiled assemblies for forbidden
// namespace dependencies. The project-reference graph gives the structural guarantee — these tests
// guard it from regressing as real code is added to the (currently thin) layers.

using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace Architecture.Tests;

public sealed class DependencyRuleTests
{
	// Namespace roots used as forbidden-dependency markers (NetArchTest matches by prefix).
	private const string ApplicationNs = "Application";
	private const string DomainNs = "Domain";
	private const string LogicNs = "Logic";
	private const string InfrastructureNs = "Infrastructure";
	private const string PresentationNs = "Presentation";

	// Persistence technology must stay behind the Infrastructure ports — no core project may touch SQLite.
	private const string SqliteNs = "Microsoft.Data.Sqlite";

	// The opt-in audit log must be local-only — its storage adapter may not touch the network.
	private const string NetworkNs = "System.Net";

	private static Assembly Load(string assemblyName) => Assembly.Load(new AssemblyName(assemblyName));

	private static readonly Assembly[] LogicAssemblies =
	[
		Load("Logic.AppManagement"),
		Load("Logic.AudioManagement"),
		Load("Logic.ModelManagement"),
		Load("Logic.GpuContactPoint"),
	];

	// Asserts that no type in the assembly depends on any of the forbidden namespace roots.
	private static void AssertNoDependency(Assembly assembly, params string[] forbiddenNamespaces)
	{
		var result = Types.InAssembly(assembly)
			.Should()
			.NotHaveDependencyOnAny(forbiddenNamespaces)
			.GetResult();

		IEnumerable<string> offenders = (result.FailingTypes ?? [])
			.Select(t => t.FullName ?? t.Name);

		Assert.True(
			result.IsSuccessful,
			$"{assembly.GetName().Name} must not depend on [{string.Join(", ", forbiddenNamespaces)}]. " +
			$"Offending types: {string.Join(", ", offenders)}");
	}

	[Fact]
	public void Domain_depends_on_nothing_above_it() =>
		AssertNoDependency(Load(DomainNs), ApplicationNs, LogicNs, InfrastructureNs, PresentationNs);

	[Fact]
	public void Application_depends_only_on_domain() =>
		AssertNoDependency(Load(ApplicationNs), LogicNs, InfrastructureNs, PresentationNs);

	[Fact]
	public void Logic_projects_depend_only_on_application_and_domain()
	{
		foreach (Assembly logic in LogicAssemblies)
		{
			AssertNoDependency(logic, InfrastructureNs, PresentationNs);
		}
	}

	[Fact]
	public void Infrastructure_does_not_depend_on_presentation() =>
		AssertNoDependency(Load(InfrastructureNs), PresentationNs);

	// "Infrastructure is reachable only from Presentation" — proven by showing no core project reaches it.
	[Fact]
	public void No_core_project_depends_on_infrastructure()
	{
		AssertNoDependency(Load(DomainNs), InfrastructureNs);
		AssertNoDependency(Load(ApplicationNs), InfrastructureNs);

		foreach (Assembly logic in LogicAssemblies)
		{
			AssertNoDependency(logic, InfrastructureNs);
		}
	}

	// The SQLite persistence lives entirely behind the ports — no Application or Logic code
	// references Microsoft.Data.Sqlite directly. (Domain is already covered by depending on nothing above it.)
	[Fact]
	public void No_core_project_depends_on_sqlite()
	{
		AssertNoDependency(Load(ApplicationNs), SqliteNs);

		foreach (Assembly logic in LogicAssemblies)
		{
			AssertNoDependency(logic, SqliteNs);
		}
	}

	// Audit data is local-only — the SQLite audit-log adapter has no network dependency.
	[Fact]
	public void The_audit_log_adapter_has_no_network_dependency()
	{
		var result = Types.InAssembly(Load(InfrastructureNs))
			.That()
			.HaveName("SqliteAuditLog")
			.Should()
			.NotHaveDependencyOnAny(NetworkNs)
			.GetResult();

		Assert.True(result.IsSuccessful, "the SQLite audit log must be local-only (no System.Net dependency).");
	}

	[Fact]
	public void No_core_project_depends_on_presentation()
	{
		AssertNoDependency(Load(DomainNs), PresentationNs);
		AssertNoDependency(Load(ApplicationNs), PresentationNs);
		AssertNoDependency(Load(InfrastructureNs), PresentationNs);

		foreach (Assembly logic in LogicAssemblies)
		{
			AssertNoDependency(logic, PresentationNs);
		}
	}
}
