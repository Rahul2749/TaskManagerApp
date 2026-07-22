using Microsoft.AspNetCore.SignalR.Client;
using TaskManager.Mobile.Configuration;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.Services;

public interface INotificationRealtimeService : IAsyncDisposable
{
    int UnreadCount { get; }
    event Action? Changed;
    Task EnsureConnectedAsync();
    Task RefreshUnreadCountAsync();
    Task DisconnectAsync();
}

public sealed class NotificationRealtimeService : INotificationRealtimeService
{
    private readonly ISecureTokenStorage _storage;
    private readonly IApiService _api;
    private HubConnection? _hub;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public NotificationRealtimeService(ISecureTokenStorage storage, IApiService api)
    {
        _storage = storage;
        _api = api;
    }

    public int UnreadCount { get; private set; }

    public event Action? Changed;

    public async Task EnsureConnectedAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var token = await _storage.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                await DisconnectCoreAsync();
                UnreadCount = 0;
                Changed?.Invoke();
                return;
            }

            await RefreshUnreadCountAsync();

            if (_hub is { State: HubConnectionState.Connected or HubConnectionState.Connecting })
                return;

            await DisconnectCoreAsync();

            var hubUrl = new Uri(new Uri(ApiSettings.BaseUrl), "hubs/tasks");
            _hub = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.AccessTokenProvider = async () =>
                        await _storage.GetAccessTokenAsync();
                })
                .WithAutomaticReconnect()
                .Build();

            _hub.On<AppNotificationDto>("NotificationReceived", _ =>
            {
                UnreadCount++;
                MainThread.BeginInvokeOnMainThread(() => Changed?.Invoke());
            });

            _hub.Reconnected += async _ =>
            {
                await RefreshUnreadCountAsync();
            };

            try
            {
                await _hub.StartAsync();
            }
            catch
            {
                // REST unread count still works without the hub.
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RefreshUnreadCountAsync()
    {
        try
        {
            UnreadCount = await _api.GetUnreadNotificationCountAsync();
        }
        catch
        {
            UnreadCount = 0;
        }

        Changed?.Invoke();
    }

    public async Task DisconnectAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await DisconnectCoreAsync();
            UnreadCount = 0;
            Changed?.Invoke();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task DisconnectCoreAsync()
    {
        if (_hub is null)
            return;

        try
        {
            await _hub.DisposeAsync();
        }
        catch
        {
            // ignore
        }

        _hub = null;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _gate.Dispose();
    }
}
