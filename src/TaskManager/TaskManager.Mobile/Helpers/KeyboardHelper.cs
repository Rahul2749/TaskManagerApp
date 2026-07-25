namespace TaskManager.Mobile.Helpers;

/// <summary>
/// Hides the platform soft keyboard. Android is the primary target;
/// other platforms no-op safely.
/// </summary>
public static class KeyboardHelper
{
    public static void Hide()
    {
#if ANDROID
        try
        {
            var activity = Platform.CurrentActivity;
            if (activity is null) return;

            var token = activity.CurrentFocus?.WindowToken
                        ?? activity.Window?.DecorView?.WindowToken;
            if (token is null) return;

            var imm = activity.GetSystemService(Android.Content.Context.InputMethodService)
                as Android.Views.InputMethods.InputMethodManager;
            imm?.HideSoftInputFromWindow(token, Android.Views.InputMethods.HideSoftInputFlags.None);
            activity.CurrentFocus?.ClearFocus();
        }
        catch
        {
            // Ignore — never block UI for keyboard dismiss failures.
        }
#endif
    }
}
