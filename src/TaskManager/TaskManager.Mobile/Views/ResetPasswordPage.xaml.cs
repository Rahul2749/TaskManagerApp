using TaskManager.Mobile.ViewModels;

namespace TaskManager.Mobile.Views;

public partial class ResetPasswordPage : UnsavedChangesPage
{
    public ResetPasswordPage(ResetPasswordViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
