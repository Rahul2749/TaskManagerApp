using TaskManager.Mobile.ViewModels;

namespace TaskManager.Mobile.Views;

public partial class ForgotPasswordPage : UnsavedChangesPage
{
    public ForgotPasswordPage(ForgotPasswordViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
