using TaskManager.Mobile.ViewModels;

namespace TaskManager.Mobile.Views;

public partial class RegisterPage : UnsavedChangesPage
{
    public RegisterPage(RegisterViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
