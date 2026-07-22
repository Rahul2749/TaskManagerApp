using TaskManager.Mobile.ViewModels;

namespace TaskManager.Mobile.Views;

public partial class AcceptInvitePage : ContentPage
{
    public AcceptInvitePage(AcceptInviteViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
