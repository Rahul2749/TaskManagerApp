using TaskManager.Mobile.ViewModels;

namespace TaskManager.Mobile.Views;

public partial class BillingPage : ContentPage
{
    private readonly BillingViewModel _viewModel;

    public BillingPage(BillingViewModel viewModel)
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
