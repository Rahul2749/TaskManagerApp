using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;

namespace TaskManager.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, Exported = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density,
    LaunchMode = LaunchMode.SingleTop,
    WindowSoftInputMode = SoftInput.AdjustResize)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "taskmanager")]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataSchemes = new[] { "https", "http" },
    DataHost = "taskmanager-app-plt1.onrender.com",
    DataPathPrefix = "/user/task")]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        HandleIntent(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleIntent(intent);
    }

    protected override void OnPause()
    {
        HideSoftKeyboard();
        base.OnPause();
    }

    /// <summary>
    /// Dismiss the soft keyboard when the user taps outside the focused text field
    /// (e.g. on empty space, lists, buttons that don't take focus).
    /// </summary>
    public override bool DispatchTouchEvent(MotionEvent? e)
    {
        if (e?.Action == MotionEventActions.Down && CurrentFocus is EditText editText)
        {
            var bounds = new Android.Graphics.Rect();
            editText.GetGlobalVisibleRect(bounds);
            if (!bounds.Contains((int)e.RawX, (int)e.RawY))
            {
                // Moving to another EditText should keep the keyboard; only hide when
                // the tap is outside any text input.
                var touched = FindEditTextAt(Window?.DecorView, (int)e.RawX, (int)e.RawY);
                if (touched is null)
                    HideSoftKeyboard(editText);
            }
        }

        return base.DispatchTouchEvent(e);
    }

    private void HideSoftKeyboard(Android.Views.View? focused = null)
    {
        try
        {
            var view = focused ?? CurrentFocus ?? Window?.DecorView;
            if (view is null) return;

            var imm = GetSystemService(InputMethodService) as InputMethodManager;
            imm?.HideSoftInputFromWindow(view.WindowToken, HideSoftInputFlags.None);
            view.ClearFocus();
        }
        catch
        {
            // ignore
        }
    }

    private static EditText? FindEditTextAt(Android.Views.View? root, int rawX, int rawY)
    {
        if (root is null) return null;
        if (root is EditText edit)
        {
            var bounds = new Android.Graphics.Rect();
            edit.GetGlobalVisibleRect(bounds);
            if (bounds.Contains(rawX, rawY))
                return edit;
        }

        if (root is ViewGroup group)
        {
            for (var i = 0; i < group.ChildCount; i++)
            {
                var found = FindEditTextAt(group.GetChildAt(i), rawX, rawY);
                if (found is not null)
                    return found;
            }
        }

        return null;
    }

    private static void HandleIntent(Intent? intent)
    {
        var data = intent?.DataString;
        if (string.IsNullOrWhiteSpace(data)) return;
        if (Microsoft.Maui.Controls.Application.Current is App app && Uri.TryCreate(data, UriKind.Absolute, out var uri))
            _ = app.HandleAppLinkAsync(uri);
    }
}
