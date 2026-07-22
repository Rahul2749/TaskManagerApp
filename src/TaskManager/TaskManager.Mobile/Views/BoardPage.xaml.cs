using TaskManager.Mobile.ViewModels;

namespace TaskManager.Mobile.Views;

public partial class BoardPage : ContentPage
{
    private readonly BoardViewModel _viewModel;

    public BoardPage(BoardViewModel viewModel)
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
