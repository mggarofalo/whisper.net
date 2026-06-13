// Unit tests for the doctor-mode arg router: the pure decision the Presentation entry point
// makes about whether to run diagnostics and exit instead of going tray-resident. Covers the documented
// flag, its aliases (case-insensitively), and the normal launch where no flag is present.

using AwesomeAssertions;
using Logic.AppManagement.Diagnostics;
using Xunit;

namespace Logic.AppManagement.Tests.Diagnostics;

public sealed class DoctorModeTests
{
	[Theory]
	[InlineData("--doctor")]
	[InlineData("--selftest")]
	[InlineData("/doctor")]
	[InlineData("--DOCTOR")]
	public void Requests_diagnostics_for_the_flag_and_its_aliases(string flag)
	{
		DoctorMode.IsRequested([flag]).Should().BeTrue();
	}

	[Fact]
	public void Finds_the_flag_among_other_arguments()
	{
		DoctorMode.IsRequested(["--verbose", "--doctor"]).Should().BeTrue();
	}

	[Fact]
	public void A_normal_launch_does_not_request_diagnostics()
	{
		DoctorMode.IsRequested([]).Should().BeFalse();
		DoctorMode.IsRequested(["--verbose"]).Should().BeFalse();
		DoctorMode.IsRequested(null).Should().BeFalse();
	}
}
