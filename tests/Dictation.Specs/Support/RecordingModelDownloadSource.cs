// A network source stand-in for the @WHISPER-4 scenarios. It records whether it was ever asked for
// bytes — so a scenario can prove a cache query made NO network request — and, when a download is
// requested, hands back canned bytes (still no real network).

using System.Text;
using Domain.Models;
using Infrastructure.Models;

namespace Dictation.Specs.Support;

internal sealed class RecordingModelDownloadSource : IModelDownloadSource
{
	private readonly byte[] _bytes = Encoding.UTF8.GetBytes(new string('m', 4_096));

	/// <summary>True once bytes have been requested — i.e. a network fetch would have happened.</summary>
	public bool WasCalled { get; private set; }

	public ValueTask<ModelDownload> OpenAsync(WhisperModelCatalogEntry entry, CancellationToken cancellationToken)
	{
		WasCalled = true;
		return ValueTask.FromResult(new ModelDownload(new MemoryStream(_bytes, writable: false), _bytes.Length));
	}
}
