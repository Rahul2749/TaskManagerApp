using TaskManager.Mobile.ViewModels;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.Views;

public partial class TasksPage : ContentPage
{
    private readonly TasksViewModel _viewModel;

    public TasksPage(TasksViewModel viewModel)
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

    private async void OnTaskSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is TaskDto task)
        {
            ((CollectionView)sender!).SelectedItem = null;
            if (_viewModel.OpenTaskCommand.CanExecute(task))
                await _viewModel.OpenTaskCommand.ExecuteAsync(task);
        }
    }
}
