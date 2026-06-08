// One row in the model picker (WHISPER-27): a model's identity, size, and ratings (immutable, from the
// ListModelsQuery projection) plus the live, observable state the picker mutates — whether it is
// downloaded, whether it is the active model, and its download progress/outcome. Built on
// CommunityToolkit.Mvvm and WPF-free so the picker behavior is driven for real in specs.

using Application.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Logic.AppManagement.Shell;

public sealed partial class ModelItemViewModel : ObservableObject
{
	public ModelItemViewModel(ModelListItemDto dto)
	{
		Id = dto.Id;
		DisplayName = dto.DisplayName;
		SizeBytes = dto.SizeBytes;
		Speed = dto.Speed;
		Accuracy = dto.Accuracy;
		Memory = dto.Memory;
		_isDownloaded = dto.IsDownloaded;
		_isActive = dto.IsActive;
	}

	public string Id { get; }

	public string DisplayName { get; }

	public long SizeBytes { get; }

	public ModelRating Speed { get; }

	public ModelRating Accuracy { get; }

	public ModelRating Memory { get; }

	/// <summary>Whether the model file is already in the local cache.</summary>
	[ObservableProperty]
	private bool _isDownloaded;

	/// <summary>Whether this is the currently active model.</summary>
	[ObservableProperty]
	private bool _isActive;

	/// <summary>Live download completion in [0, 100]; meaningful while <see cref="DownloadState"/> is in progress.</summary>
	[ObservableProperty]
	private double _downloadPercent;

	/// <summary>The terminal-aware state of this row's download.</summary>
	[ObservableProperty]
	private ModelDownloadState _downloadState;
}
