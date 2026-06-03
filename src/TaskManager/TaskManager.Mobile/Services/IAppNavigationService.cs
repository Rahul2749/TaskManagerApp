namespace TaskManager.Mobile.Services;

public interface IAppNavigationService
{
    Task GoToLoginAsync();
    Task GoToMainAsync();
}
