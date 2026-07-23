using TaskManager.Mobile.ViewModels;

namespace TaskManager.Mobile.Views;

public partial class TimesheetsPage : ContentPage
{
    private readonly TimesheetsViewModel _vm;

    public TimesheetsPage(TimesheetsViewModel vm)
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
