// Single source of truth for the app's per-user data locations. The model cache, logs, and
// settings database all hang off this helper so the layout is consistent and pinned by one test.
//
// Why the folder is NOT named after the product/PackId: Velopack installs the app to
// %LOCALAPPDATA%\<PackId> (PackId = "Whisper.Net", see build/pack.ps1). The data folder used to be
// "whisper.net", which on case-insensitive Windows IS that install root — so the installer's
// "remove existing application directory" step tripped over user data, and an open rolling-log handle
// inside the install dir blocked updates while the app ran. Keeping the data root's name distinct from
// the PackId guarantees the installer never touches user data. Logs + model cache stay machine-local
// (LocalApplicationData, not roaming); the settings DB stays roaming (ApplicationData) as before.

namespace Infrastructure.DependencyInjection;

public static class WhisperAppData
{
	/// <summary>The Velopack PackId (mirrors <c>build/pack.ps1</c>). The data root MUST never equal this,
	/// because Velopack installs to <c>%LOCALAPPDATA%\{PackId}</c>.</summary>
	public const string VelopackPackId = "Whisper.Net";

	/// <summary>The per-user data folder name. Deliberately distinct from <see cref="VelopackPackId"/>
	/// (even case-insensitively) so the data dir and the Velopack install dir never collide.</summary>
	public const string FolderName = "whisper-net";

	/// <summary>The machine-local data root: <c>%LOCALAPPDATA%\whisper-net</c> (logs + model cache).</summary>
	public static string LocalRoot => Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FolderName);

	/// <summary>The roaming data root: <c>%APPDATA%\whisper-net</c> (settings database).</summary>
	public static string RoamingRoot => Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), FolderName);

	/// <summary>The per-user logs directory: <c>%LOCALAPPDATA%\whisper-net\logs</c>.</summary>
	public static string LogsDirectory => Path.Combine(LocalRoot, "logs");

	/// <summary>The per-user model cache directory: <c>%LOCALAPPDATA%\whisper-net\models</c>.</summary>
	public static string ModelCacheDirectory => Path.Combine(LocalRoot, "models");

	/// <summary>The per-user settings database file: <c>%APPDATA%\whisper-net\whisper.db</c>.</summary>
	public static string DatabasePath => Path.Combine(RoamingRoot, "whisper.db");

	/// <summary>The Velopack install root (<c>%LOCALAPPDATA%\{PackId}</c>). No data path may live here.</summary>
	public static string VelopackInstallRoot => Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), VelopackPackId);
}
