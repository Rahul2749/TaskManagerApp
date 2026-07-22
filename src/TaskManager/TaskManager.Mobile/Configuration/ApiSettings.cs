namespace TaskManager.Mobile.Configuration;

public static class ApiSettings
{
    public const string PreferenceKey = "api_base_url";

    /// <summary>
    /// Production Render host. Override at runtime via Preferences (<see cref="PreferenceKey"/>).
    /// </summary>
    public const string ProductionBaseUrl = "https://taskmanager-app-plt1.onrender.com/";

    /// <summary>
    /// Override at runtime via Preferences if needed (key: api_base_url).
    /// </summary>
    public static string BaseUrl
    {
        get
        {
            var overrideUrl = Preferences.Default.Get(PreferenceKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(overrideUrl))
                return Normalize(overrideUrl);

            return Normalize(GetDefaultBaseUrl());
        }
    }

    private static string GetDefaultBaseUrl()
    {
#if DEBUG
        return GetDebugBaseUrl();
#else
        return ProductionBaseUrl;
#endif
    }

    private static string GetDebugBaseUrl()
    {
#if ANDROID
        // Android emulator → host machine loopback
        return "http://10.0.2.2:5018/";
#elif IOS || MACCATALYST
        return "http://localhost:5018/";
#elif WINDOWS
        return "https://localhost:7294/";
#else
        return "http://localhost:5018/";
#endif
    }

    private static string Normalize(string url) =>
        url.EndsWith('/') ? url : url + "/";
}
