// Verifies the Application ports (WHISPER-44) hold the properties the BDD harness depends on: every
// port is a pure interface that NSubstitute can fake (one case per port), the specified ports all
// exist, and no port exposes a static surface. The "no native/framework leakage" property is
// validated behaviorally by the @WHISPER-44 reflection scenario.

using System.Reflection;
using NSubstitute;
using Xunit;

namespace Application.Tests;

public sealed class PortsTests
{
	// Every interface declared in the Application.Ports namespace — the discovery the harness fakes.
	private static Type[] PortTypes() =>
		typeof(Application.Ports.ITranscriber).Assembly
			.GetTypes()
			.Where(t => t.IsInterface && t.Namespace == "Application.Ports")
			.ToArray();

	public static IEnumerable<object[]> Ports() => PortTypes().Select(t => new object[] { t });

	[Theory]
	[MemberData(nameof(Ports))]
	public void Every_port_is_substitutable_with_NSubstitute(Type portType)
	{
		object substitute = Substitute.For([portType], []);

		Assert.NotNull(substitute);
		Assert.True(portType.IsInstanceOfType(substitute));
	}

	[Fact]
	public void The_specified_ports_all_exist()
	{
		string[] expected =
		[
			"ITranscriber", "IAudioSource", "IVad", "ITextInjector", "IClipboard",
			"IRephraseClient", "IHistoryStore", "ISettingsStore", "IHotkeyListener", "IGpuProbe",
		];

		HashSet<string> actual = PortTypes().Select(t => t.Name).ToHashSet();

		foreach (string name in expected)
		{
			Assert.Contains(name, actual);
		}
	}

	[Fact]
	public void No_port_exposes_a_static_surface()
	{
		foreach (Type port in PortTypes())
		{
			Assert.True(port.IsInterface, $"{port.Name} must be an interface (substitutable seam)");

			foreach (MethodInfo method in port.GetMethods())
			{
				Assert.False(method.IsStatic, $"{port.Name}.{method.Name} must not be static");
			}
		}
	}
}
