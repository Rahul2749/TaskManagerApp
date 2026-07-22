using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.Mobile.Services;

namespace TaskManager.Mobile.ViewModels;

[QueryProperty(nameof(Token), nameof(Token))]
public partial class ResetPasswordViewModel : BaseViewModel
{
    private readonly IAuthService _authService;

    public ResetPasswordViewModel(IAuthService authService)
    {
        _authService = authService;
        Title = "Reset password";
    }

    [ObservableProperty] private string _token = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private string _successMessage = string.Empty;

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (IsBusy) return;
        ClearError();
        SuccessMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Token) || string.IsNullOrWhiteSpace(Password))
        {
            SetError("Paste the reset token from your email and choose a new password.");
            return;
        }

        if (Password.Length < 8)
        {
            SetError("Password must be at least 8 characters.");
            return;
        }

        if (Password != ConfirmPassword)
        {
            SetError("Passwords do not match.");
            return;
        }

        try
        {
            IsBusy = true;
            var (ok, message) = await _authService.ResetPasswordAsync(Token.Trim(), Password);
            if (ok)
                SuccessMessage = message;
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
}
