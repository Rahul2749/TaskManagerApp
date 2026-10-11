using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;
using TaskManager.Shared.DTOs;

namespace TaskManager.Client.Services;

public interface INotificationRealtimeService : IAsyncDisposable
{
    int UnreadCount { get; }
    event Action? Changed;
    Task EnsureConnectedAsync();
    Task RefreshUnreadCountAsync();
    Task DisconnectAsync();
}

/// <summary>
/// Keeps unread notification count in sync via REST + SignalR NotificationReceived events.
/// </summary>
public sealed class NotificationRealtimeService : INotificationRealtimeService
{
    private readonly NavigationManager _navigation;
    private readonly LocalStorageService _localStorage;
    private readonly IApiService _api;
    private readonly AuthenticationStateProvider _authStateProvider;
    private HubConnection? _hub;
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private int _sessionVersion;
    private string? _hubUserId;
    private string? _activeUserId;

    public NotificationRealtimeService(
        NavigationManager navigation,
        LocalStorageService localStorage,
        IApiService api,
        AuthenticationStateProvider authStateProvider)
    {
        _navigation = navigation;
        _localStorage = localStorage;
        _api = api;
        _authStateProvider = authStateProvider;
        _authStateProvider.AuthenticationStateChanged += OnAuthStateChanged;
    }

    public int UnreadCount { get; private set; }

    public event Action? Changed;

    public async Task EnsureConnectedAsync()
    {
        await _connectionGate.WaitAsync();
        try
        {
            var version = _sessionVersion;
            var state = await _authStateProvider.GetAuthenticationStateAsync();
            if (version != _sessionVersion) return;
            var userId = state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            _activeUserId = userId;
            if (state.User.Identity?.IsAuthenticated != true)
            {
                await DisconnectCoreAsync();
                return;
            }

            if (_hubUserId == userId && _hub is { State: HubConnectionState.Connected or HubConnectionState.Connecting })
                return;

            await RefreshUnreadCountAsync();
            if (version != _sessionVersion) return;
            await DisconnectCoreAsync();
            _hubUserId = userId;

            _hub = new HubConnectionBuilder()
                .WithUrl(_navigation.ToAbsoluteUri("/hubs/tasks"), options =>
                {
                    options.AccessTokenProvider = async () =>
                        await _localStorage.GetItemAsync<string>("accessToken");
                })
                .WithAutomaticReconnect()
                .Build();

            _hub.On<AppNotificationDto>("NotificationReceived", async _ =>
            {
                if (_activeUserId != userId) return;
                UnreadCount++;
                Changed?.Invoke();
                await Task.CompletedTask;
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
                // Hub may be unavailable offline; unread count still works via REST.
            }
        }
        finally { _connectionGate.Release(); }
    }

    public async Task RefreshUnreadCountAsync()
    {
        var version = _sessionVersion;
        int count;
        try { count = await _api.GetUnreadNotificationCountAsync(); }
        catch { count = 0; }
        if (version != _sessionVersion) return;
        UnreadCount = count;
        Changed?.Invoke();
    }

    public async Task DisconnectAsync()
    {
        await _connectionGate.WaitAsync();
        try
        {
            await DisconnectCoreAsync();
            UnreadCount = 0;
            Changed?.Invoke();
        }
        finally { _connectionGate.Release(); }
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
        _hubUserId = null;
    }

    private async void OnAuthStateChanged(Task<AuthenticationState> task)
    {
        var version = ++_sessionVersion;
        try
        {
            var state = await task;
            if (version != _sessionVersion) return;
            _activeUserId = state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (state.User.Identity?.IsAuthenticated == true)
                await EnsureConnectedAsync();
            else
            {
                UnreadCount = 0;
                Changed?.Invoke();
                await DisconnectAsync();
            }
        }
        catch
        {
            // ignore
        }
    }

    public async ValueTask DisposeAsync()
    {
        _authStateProvider.AuthenticationStateChanged -= OnAuthStateChanged;
        await DisconnectAsync();
    }
}
