// A fake HTTP transport for the rephrase scenarios: it records whether (and where) a
// request was sent, so a scenario can prove a disabled client makes NO network call, and can stand in
// for a healthy or a failing local Ollama without any real socket.

using System.Net;
using System.Text;

namespace Dictation.Specs.Support;

internal sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
	private string _responseJson = "{\"response\":\"\"}";
	private bool _failWithServerError;

	public int SendCount { get; private set; }

	public Uri? LastRequestUri { get; private set; }

	public void RespondWith(string responseText) =>
		_responseJson = $"{{\"response\":\"{responseText}\",\"done\":true}}";

	public void FailWithServerError() => _failWithServerError = true;

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		SendCount++;
		LastRequestUri = request.RequestUri;

		if (_failWithServerError)
		{
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
		}

		HttpResponseMessage response = new(HttpStatusCode.OK)
		{
			Content = new StringContent(_responseJson, Encoding.UTF8, "application/json"),
		};

		return Task.FromResult(response);
	}
}
