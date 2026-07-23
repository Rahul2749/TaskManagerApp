using TaskManager.Mobile.ViewModels;

namespace TaskManager.Mobile.Views;

public partial class TimelinePage : ContentPage
{
    private readonly TimelineViewModel _vm;

    public TimelinePage(TimelineViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.LoadCommand.CanExecute(null))
            _vm.LoadCommand.Execute(null);
    }
}
