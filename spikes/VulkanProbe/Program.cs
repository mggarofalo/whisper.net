// DISPOSABLE SPIKE (WHISPER-65). Answers the Module 3 go/no-go question: does Whisper.net transcribe
// via the Vulkan runtime on this machine's RTX 5080, is the GPU actually engaged, and does CPU
// fallback work? Run it twice:
//   dotnet run -- gpu   (prefer Vulkan, fall back to CPU)
//   dotnet run -- cpu   (force CPU only — simulates no compatible GPU runtime)
// It downloads the ggml base.en model once (cached) and transcribes the bundled jfk.wav. Not part of
// Whisper.slnx, not built by CI. Delete once findings are recorded.

using System.Diagnostics;
using System.Text;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.LibraryLoader;
using Whisper.net.Logger;

string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "gpu";

// Capture native (whisper.cpp / ggml) log lines — this is where the backend announces which device
// it initialized, the strongest evidence of whether the GPU is actually engaged.
List<string> nativeLogs = [];
LogProvider.AddLogger((level, message) =>
{
	if (!string.IsNullOrWhiteSpace(message))
	{
		nativeLogs.Add($"[{level}] {message.TrimEnd()}");
	}
});

// Choose the runtime order BEFORE any factory loads the native library.
RuntimeOptions.RuntimeLibraryOrder = mode == "cpu"
	? [RuntimeLibrary.Cpu]
	: [RuntimeLibrary.Vulkan, RuntimeLibrary.Cpu];

Console.WriteLine($"=== VulkanProbe ({mode}) ===");
Console.WriteLine($"Requested runtime order: {string.Join(", ", RuntimeOptions.RuntimeLibraryOrder)}");

string modelDir = Path.Combine(AppContext.BaseDirectory, "models");
Directory.CreateDirectory(modelDir);
string modelPath = Path.Combine(modelDir, "ggml-base.en.bin");
if (!File.Exists(modelPath))
{
	Console.WriteLine("Downloading ggml base.en model (~142 MB, one-time, from Hugging Face)...");
	await using Stream model = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(GgmlType.BaseEn);
	await using FileStream fileStream = File.Create(modelPath);
	await model.CopyToAsync(fileStream);
}

// The canonical whisper.cpp sample clip (11s of JFK). Downloaded on demand so no binary is committed.
string wavPath = Path.Combine(AppContext.BaseDirectory, "jfk.wav");
if (!File.Exists(wavPath))
{
	Console.WriteLine("Downloading jfk.wav sample (one-time)...");
	using HttpClient http = new();
	byte[] wavBytes = await http.GetByteArrayAsync("https://raw.githubusercontent.com/ggml-org/whisper.cpp/master/samples/jfk.wav");
	await File.WriteAllBytesAsync(wavPath, wavBytes);
}

using WhisperFactory factory = WhisperFactory.FromPath(modelPath);

// Two passes so warmup (first inference: GPU shader compilation, model upload) is separated from
// steady-state throughput. The "warm" number is the fair GPU-vs-CPU comparison.
string finalTranscript = string.Empty;
for (int pass = 1; pass <= 2; pass++)
{
	await using WhisperProcessor processor = factory.CreateBuilder().WithLanguage("en").Build();

	StringBuilder transcript = new();
	Stopwatch stopwatch = Stopwatch.StartNew();
	await using (FileStream wav = File.OpenRead(wavPath))
	{
		await foreach (SegmentData segment in processor.ProcessAsync(wav))
		{
			transcript.Append(segment.Text);
		}
	}
	stopwatch.Stop();

	Console.WriteLine($"Pass {pass} ({(pass == 1 ? "cold" : "warm")}): {stopwatch.ElapsedMilliseconds} ms");
	finalTranscript = transcript.ToString().Trim();
}

Console.WriteLine($"Loaded runtime library: {RuntimeOptions.LoadedLibrary}");
Console.WriteLine($"Transcript: {finalTranscript}");

Console.WriteLine("--- native log lines mentioning device / vulkan / gpu ---");
foreach (string line in nativeLogs.Where(l =>
	l.Contains("vulkan", StringComparison.OrdinalIgnoreCase)
	|| l.Contains("device", StringComparison.OrdinalIgnoreCase)
	|| l.Contains("gpu", StringComparison.OrdinalIgnoreCase)
	|| l.Contains("RTX", StringComparison.OrdinalIgnoreCase)))
{
	Console.WriteLine(line);
}
