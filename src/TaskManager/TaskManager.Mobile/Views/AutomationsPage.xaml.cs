using TaskManager.Mobile.ViewModels;

namespace TaskManager.Mobile.Views;

public partial class AutomationsPage : ContentPage
{
    private readonly AutomationsViewModel _vm;

    public AutomationsPage(AutomationsViewModel vm)
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
