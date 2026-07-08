namespace TaskManager.Mobile.Configuration;

public static class ApiSettings
{
    /// <summary>
    /// Override at runtime via Preferences if needed (key: api_base_url).
    /// </summary>
    public static string BaseUrl
    {
        get
        {
            var overrideUrl = Preferences.Default.Get("api_base_url", string.Empty);
            if (!string.IsNullOrWhiteSpace(overrideUrl))
                return Normalize(overrideUrl);

            return Normalize(GetDefaultBaseUrl());
        }
    }

    private static string GetDefaultBaseUrl()
    {
        return "http://taskboard.runasp.net/";
    }

    private static string Normalize(string url) =>
        url.EndsWith('/') ? url : url + "/";
}
