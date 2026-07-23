using TaskManager.Mobile.Configuration;

namespace TaskManager.Mobile.Services;

public interface IDeepLinkService
{
    /// <summary>Store a link to open after login/shell is ready.</summary>
    void SetPending(Uri uri);

    /// <summary>Try to navigate from a pending or provided URI. Returns true if handled.</summary>
    Task<bool> TryHandleAsync(Uri? uri = null);

    /// <summary>Build a shareable web URL for a task.</summary>
    string GetTaskShareUrl(int taskId);
}

public sealed class DeepLinkService : IDeepLinkService
{
    private readonly ISecureTokenStorage _storage;
    private Uri? _pending;

    public DeepLinkService(ISecureTokenStorage storage) => _storage = storage;

    public void SetPending(Uri uri) => _pending = uri;

    public string GetTaskShareUrl(int taskId)
    {
        var baseUrl = ApiSettings.ProductionBaseUrl.TrimEnd('/');
#if DEBUG
        // Prefer the configured API host in debug so local shares still resolve.
        baseUrl = ApiSettings.BaseUrl.TrimEnd('/');
#endif
        return $"{baseUrl}/user/task/{taskId}";
    }

    public async Task<bool> TryHandleAsync(Uri? uri = null)
    {
        uri ??= _pending;
        if (uri is null) return false;

        if (!await _storage.HasSessionAsync())
        {
            _pending = uri;
            return false;
        }

        if (!TryParseTaskId(uri, out var taskId))
        {
            if (ReferenceEquals(uri, _pending))
                _pending = null;
            return false;
        }

        _pending = null;

        try
        {
            // Ensure shell is current before navigating.
            if (Shell.Current is null)
            {
                _pending = uri;
                return false;
            }

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Shell.Current.GoToAsync($"taskdetail?id={taskId}");
            });
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Deep link navigation failed: {ex.Message}");
            return false;
        }
    }

    public static bool TryParseTaskId(Uri uri, out int taskId)
    {
        taskId = 0;
        var path = uri.AbsolutePath.Trim('/');
        // Custom scheme: taskmanager://task/12  → host=task, path=/12 or AbsolutePath empty
        if (string.Equals(uri.Scheme, "taskmanager", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(uri.Host, "task", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(uri.AbsolutePath.Trim('/'), out taskId) && taskId > 0)
                return true;

            // taskmanager:///user/task/12
            path = string.IsNullOrEmpty(path) ? uri.Host + uri.AbsolutePath : path;
        }

        // /user/task/12 or user/task/12
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (string.Equals(segments[i], "task", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(segments[i + 1], out taskId) && taskId > 0)
                return true;
        }

        // /tasks/12
        if (segments.Length >= 2 &&
            string.Equals(segments[^2], "tasks", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(segments[^1], out taskId) && taskId > 0)
            return true;

        return false;
    }
}
