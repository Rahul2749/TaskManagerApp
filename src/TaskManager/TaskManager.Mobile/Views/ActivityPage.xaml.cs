using TaskManager.Mobile.ViewModels;

namespace TaskManager.Mobile.Views;

public partial class ActivityPage : ContentPage
{
    private readonly ActivityViewModel _viewModel;

    public ActivityPage(ActivityViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.LoadCommand.CanExecute(null))
            _viewModel.LoadCommand.Execute(null);
    }
}
