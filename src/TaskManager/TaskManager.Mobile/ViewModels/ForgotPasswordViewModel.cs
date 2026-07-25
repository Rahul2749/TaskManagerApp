using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.Mobile.Services;

namespace TaskManager.Mobile.ViewModels;

public partial class ForgotPasswordViewModel : DirtyFormViewModel
{
    private readonly IAuthService _authService;

    public ForgotPasswordViewModel(IAuthService authService)
    {
        _authService = authService;
        Title = "Forgot password";
        MarkClean();
    }

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _successMessage = string.Empty;

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (IsBusy) return;
        ClearError();
        SuccessMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Email))
        {
            SetError("Enter your email address.");
            return;
        }

        try
        {
            IsBusy = true;
            var (ok, message) = await _authService.ForgotPasswordAsync(Email.Trim());
            if (ok)
            {
                SuccessMessage = message;
                AllowLeaveWithoutPrompt();
            }
            else
                SetError(message);
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected override string BuildSnapshot() => Email?.Trim() ?? string.Empty;
}
