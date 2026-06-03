using TaskManager.Mobile.ViewModels;

namespace TaskManager.Mobile.Views;

public partial class ProjectEditorPage : ContentPage
{
    private readonly ProjectEditorViewModel _viewModel;

    public ProjectEditorPage(ProjectEditorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.LoadCommand.CanExecute(null))
            _viewModel.LoadCommand.Execute(null);
    }
}
