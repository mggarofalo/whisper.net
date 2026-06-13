// Thin step definitions for the capture feature. Each step delegates to the
// AudioCaptureDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class AudioCaptureSteps(AudioCaptureDriver driver)
{
	private static int Channels(string word) => word == "stereo" ? 2 : 1;

	[Given(@"a capture device producing (\d+) Hz (stereo|mono) float audio")]
	public void GivenACaptureDevice(int sampleRate, string channels) => driver.DeviceFormat(sampleRate, Channels(channels));

	[Given(@"the device has (\d+) frames buffered to flush on stop")]
	public void GivenBufferedFrames(int count) => driver.BufferFramesToFlush(count);

	[Given(@"capture has started")]
	public void GivenCaptureHasStarted() => driver.Start();

	[When(@"capture starts")]
	public void WhenCaptureStarts() => driver.Start();

	[When(@"capture starts again")]
	public void WhenCaptureStartsAgain() => driver.Start();

	[When(@"the device produces a frame of (\d+) samples")]
	public void WhenTheDeviceProducesAFrame(int sampleCount) => driver.ProduceFrame(sampleCount);

	[When(@"capture stops")]
	public void WhenCaptureStops() => driver.Stop();

	[When(@"the capture device becomes unavailable")]
	public void WhenTheDeviceBecomesUnavailable() => driver.DeviceBecomesUnavailable();

	[Then(@"a frame of (\d+) samples is delivered in the negotiated (\d+) Hz (stereo|mono) format")]
	public void ThenAFrameIsDelivered(int sampleCount, int sampleRate, string channels) =>
		driver.AssertSingleFrameDelivered(sampleCount, sampleRate, Channels(channels));

	[Then(@"the device is started only once")]
	public void ThenStartedOnce() => driver.AssertStartedOnce();

	[Then(@"the (\d+) buffered frames are delivered")]
	public void ThenBufferedFramesDelivered(int count) => driver.AssertFrameCount(count);

	[Then(@"no further frames are delivered afterward")]
	public void ThenNoFurtherFrames() => driver.ProduceStrayFrame();

	[Then(@"the capture device is released")]
	public void ThenDeviceReleased() => driver.AssertDeviceReleased();

	[Then(@"a capture failure is reported with reason ""(.*)""")]
	public void ThenCaptureFailed(string reason) => driver.AssertCaptureFailed(reason);

	[Then(@"no error is raised to the caller")]
	public void ThenNoErrorRaised()
	{
		// Reaching this step means no exception propagated from the failing device — the capture
		// failure was surfaced as an event, which is the behavior under test.
	}
}
