// Guard for the SendInput interop layout. SendInput requires cbSize == sizeof(INPUT); a
// union sized for only KEYBDINPUT (24 bytes) instead of the larger MOUSEINPUT (32) made Marshal.SizeOf
// 32 on x64 where Windows expects 40, so SendInput rejected every batch with ERROR_INVALID_PARAMETER and
// no transcribed text was ever typed. This pins the marshaled size so the regression can't return. The
// real SendInput call itself stays smoke-only (it needs a focused window and a live OS).

using AwesomeAssertions;
using Infrastructure.TextDelivery;
using Xunit;

namespace Infrastructure.Tests.TextDelivery;

public sealed class Win32KeyboardInputTests
{
	[Fact]
	public void INPUT_marshals_to_the_platform_size_SendInput_expects()
	{
		int expected = nint.Size == 8 ? 40 : 28;
		Win32KeyboardInput.NativeInputSize.Should().Be(expected,
			"SendInput requires cbSize == sizeof(INPUT); a smaller union makes it fail with error 87");
	}
}
