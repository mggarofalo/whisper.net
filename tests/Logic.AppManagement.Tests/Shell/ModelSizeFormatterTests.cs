// Pins the model download size format: Windows-style binary units, whole megabytes below a gigabyte and one
// decimal above, never "1024 MB".

using AwesomeAssertions;
using Logic.AppManagement.Shell;
using Xunit;

namespace Logic.AppManagement.Tests.Shell;

public sealed class ModelSizeFormatterTests
{
	[Theory]
	[InlineData(0L, "0 MB")]
	[InlineData(-5L, "0 MB")]
	[InlineData(77_691_713L, "74 MB")]
	[InlineData(147_951_465L, "141 MB")]
	[InlineData(1_073_217_536L, "1.0 GB")]
	[InlineData(1_533_763_059L, "1.4 GB")]
	[InlineData(3_095_033_483L, "2.9 GB")]
	public void Formats_sizes_like_windows(long bytes, string expected) =>
		ModelSizeFormatter.Format(bytes).Should().Be(expected);
}