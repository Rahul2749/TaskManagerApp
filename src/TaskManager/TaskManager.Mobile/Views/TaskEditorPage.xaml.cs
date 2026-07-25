using TaskManager.Mobile.ViewModels;

namespace TaskManager.Mobile.Views;

public partial class TaskEditorPage : UnsavedChangesPage
{
    private readonly TaskEditorViewModel _viewModel;
    public TaskEditorPage(TaskEditorViewModel viewModel)
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
