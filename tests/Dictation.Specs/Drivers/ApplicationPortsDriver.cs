// Drives the Application-ports scenarios. The first behavior shows a port (ITranscriber) being driven
// through its interface via an NSubstitute fake — the seam the whole BDD harness relies on. The
// second inspects every interface under Application.Ports by reflection and asserts none of its method
// signatures leak a native or framework type (only BCL, Domain, and Application types are allowed).

using System.Reflection;
using Application.Ports;
using AwesomeAssertions;
using Domain.Audio;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class ApplicationPortsDriver(ITranscriber transcriber)
{
	// Assemblies a port signature may reference: the BCL, the Domain layer, and the Application layer.
	private static readonly string[] AllowedAssemblies = ["Domain", "Application", "System", "mscorlib", "netstandard"];

	private string? _transcribedText;

	public void TranscriberReturns(string text) =>
		transcriber
			.TranscribeAsync(Arg.Any<AudioClip>(), Arg.Any<CancellationToken>())
			.Returns(new TranscriptionResult(text));

	public async Task RequestTranscription() =>
		_transcribedText = (await transcriber.TranscribeAsync(AudioClip.OneSecondOfSilence(), CancellationToken.None)).Text;

	public void AssertTranscribed(string expected) =>
		_transcribedText.Should().Be(expected);

	// Walks every Application.Ports interface and asserts no method exposes a non-allowed (native or
	// framework) type anywhere in its parameter or return signature.
	public void AssertNoPortLeaksNativeOrFrameworkTypes()
	{
		Type[] ports = typeof(ITranscriber).Assembly
			.GetTypes()
			.Where(t => t.IsInterface && t.Namespace == "Application.Ports")
			.ToArray();

		ports.Should().NotBeEmpty("the Application.Ports namespace should contain the port interfaces");

		List<string> offenders = [];

		foreach (Type port in ports)
		{
			foreach (MethodInfo method in port.GetMethods())
			{
				IEnumerable<Type> signatureTypes = method.GetParameters()
					.Select(p => p.ParameterType)
					.Append(method.ReturnType);

				foreach (Type type in signatureTypes)
				{
					if (!IsAllowed(type))
					{
						offenders.Add($"{port.Name}.{method.Name} -> {type.FullName}");
					}
				}
			}
		}

		offenders.Should().BeEmpty();
	}

	// A type is allowed if every leaf type (unwrapping generics, arrays, and nullables) comes from an
	// allowed assembly.
	private static bool IsAllowed(Type type)
	{
		if (type.IsGenericParameter)
		{
			return true;
		}

		if (type.HasElementType)
		{
			return IsAllowed(type.GetElementType()!);
		}

		if (type.IsGenericType)
		{
			return IsFromAllowedAssembly(type) && type.GetGenericArguments().All(IsAllowed);
		}

		return IsFromAllowedAssembly(type);
	}

	private static bool IsFromAllowedAssembly(Type type)
	{
		string assemblyName = type.Assembly.GetName().Name ?? string.Empty;
		return AllowedAssemblies.Any(allowed =>
			assemblyName.Equals(allowed, StringComparison.Ordinal) ||
			assemblyName.StartsWith("System", StringComparison.Ordinal));
	}
}
