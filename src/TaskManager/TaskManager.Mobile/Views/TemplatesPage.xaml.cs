using TaskManager.Mobile.ViewModels;

namespace TaskManager.Mobile.Views;

public partial class TemplatesPage : ContentPage
{
    private readonly TemplatesViewModel _viewModel;

    public TemplatesPage(TemplatesViewModel viewModel)
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
