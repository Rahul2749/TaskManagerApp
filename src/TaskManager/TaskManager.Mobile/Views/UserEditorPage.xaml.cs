using TaskManager.Mobile.ViewModels;

namespace TaskManager.Mobile.Views;

public partial class UserEditorPage : ContentPage
{
    private readonly UserEditorViewModel _viewModel;

    public UserEditorPage(UserEditorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }
}
