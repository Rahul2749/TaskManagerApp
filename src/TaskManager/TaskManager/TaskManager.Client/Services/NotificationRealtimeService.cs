using System.Net.Http.Json;
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
        var state = await _authStateProvider.GetAuthenticationStateAsync();
        if (state.User.Identity?.IsAuthenticated != true)
        {
            await DisconnectAsync();
            return;
        }

        await RefreshUnreadCountAsync();

        if (_hub is { State: HubConnectionState.Connected or HubConnectionState.Connecting })
            return;

        await DisconnectAsync();

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

    private async void OnAuthStateChanged(Task<AuthenticationState> task)
    {
        try
        {
            var state = await task;
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
