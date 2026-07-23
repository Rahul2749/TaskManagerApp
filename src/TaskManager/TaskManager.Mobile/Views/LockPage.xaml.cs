using TaskManager.Mobile.ViewModels;

namespace TaskManager.Mobile.Views;

public partial class LockPage : ContentPage
{
    private readonly LockViewModel _viewModel;

    public LockPage(LockViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _viewModel.AppearingCommand.ExecuteAsync(null);
    }
}
