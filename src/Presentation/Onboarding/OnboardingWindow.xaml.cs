// The first-run onboarding window (WHISPER-51): a thin guided flow bound to OnboardingViewModel. Pure
// view glue — every step dispatches through the WPF-free view-model — so it is verified by smoke, not by
// the specs. It closes itself once the view-model reports setup complete, handing control back to the
// tray-resident app.

using System.ComponentModel;
using Logic.AppManagement.Shell;

namespace Presentation.Onboarding;

public partial class OnboardingWindow : System.Windows.Window
{
	private readonly OnboardingViewModel _viewModel;

	public OnboardingWindow(OnboardingViewModel viewModel)
	{
		_viewModel = viewModel;
		InitializeComponent();
		DataContext = viewModel;
		_viewModel.PropertyChanged += OnViewModelPropertyChanged;
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(OnboardingViewModel.IsComplete) && _viewModel.IsComplete)
		{
			Close();
		}
	}

	protected override void OnClosed(System.EventArgs e)
	{
		_viewModel.PropertyChanged -= OnViewModelPropertyChanged;
		base.OnClosed(e);
	}
}
