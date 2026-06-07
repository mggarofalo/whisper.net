// Unit coverage for the pure integrity-RID comparison at the heart of the WHISPER-6 probe. The Win32
// token plumbing around it is the I/O boundary (verified by smoke); this pins the one decision that
// matters: a higher foreground RID means the focused window outranks us and input would be UIPI-dropped.

using Application.Ports;
using AwesomeAssertions;
using Infrastructure.TextDelivery;
using Xunit;

namespace Infrastructure.Tests.TextDelivery;

public sealed class Win32ForegroundIntegrityProbeTests
{
	// Windows integrity RIDs: LOW 0x1000, MEDIUM 0x2000, HIGH 0x3000, SYSTEM 0x4000.
	[Theory]
	[InlineData(0x3000u, 0x2000u, ForegroundIntegrity.Higher)] // elevated window vs our medium process
	[InlineData(0x2000u, 0x2000u, ForegroundIntegrity.Same)]
	[InlineData(0x1000u, 0x2000u, ForegroundIntegrity.Lower)]
	public void Compares_foreground_integrity_to_our_own(uint foregroundRid, uint currentRid, ForegroundIntegrity expected) =>
		Win32ForegroundIntegrityProbe.Compare(foregroundRid, currentRid).Should().Be(expected);
}
