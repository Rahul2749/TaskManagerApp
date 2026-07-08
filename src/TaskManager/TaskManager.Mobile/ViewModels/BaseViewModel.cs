using CommunityToolkit.Mvvm.ComponentModel;

namespace TaskManager.Mobile.ViewModels;

public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    protected void SetError(string message) => ErrorMessage = message;

    protected void ClearError() => ErrorMessage = string.Empty;
}
