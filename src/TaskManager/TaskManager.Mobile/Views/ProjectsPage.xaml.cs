using TaskManager.Mobile.ViewModels;

namespace TaskManager.Mobile.Views;

public partial class ProjectsPage : ContentPage
{
    private readonly ProjectsViewModel _viewModel;

    public ProjectsPage(ProjectsViewModel viewModel)
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

    private void OnProjectSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is TaskManager.Shared.DTOs.ProjectDto project && BindingContext is ProjectsViewModel vm)
        {
            vm.OpenProjectCommand.Execute(project);
        }
        
        if (sender is CollectionView cv)
        {
            cv.SelectedItem = null;
        }
    }
}
