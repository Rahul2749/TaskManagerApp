using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace TaskManager.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, Exported = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density,
    LaunchMode = LaunchMode.SingleTop)]
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

    private static void HandleIntent(Intent? intent)
    {
        var data = intent?.DataString;
        if (string.IsNullOrWhiteSpace(data)) return;
        if (Application.Current is App app && Uri.TryCreate(data, UriKind.Absolute, out var uri))
            _ = app.HandleAppLinkAsync(uri);
    }
}
