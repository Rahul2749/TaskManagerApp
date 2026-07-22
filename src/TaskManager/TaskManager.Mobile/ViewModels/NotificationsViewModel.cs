using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TaskManager.Mobile.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

public partial class NotificationsViewModel : BaseViewModel
{
    private readonly IApiService _api;
    private readonly INotificationRealtimeService _realtime;

    public NotificationsViewModel(IApiService api, INotificationRealtimeService realtime)
    {
        _api = api;
        _realtime = realtime;
        Title = "Notifications";
    }

    public ObservableCollection<AppNotificationDto> Items { get; } = new();

    [ObservableProperty] private string _successMessage = string.Empty;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        try
        {
            if (!IsRefreshing) IsBusy = true;
            ClearError();
            SuccessMessage = string.Empty;

            await _realtime.EnsureConnectedAsync();
            var items = await _api.GetNotificationsAsync() ?? [];
            Items.Clear();
            foreach (var item in items)
                Items.Add(item);

            await _realtime.RefreshUnreadCountAsync();
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task MarkAllReadAsync()
    {
        try
        {
            IsBusy = true;
            if (await _api.MarkAllNotificationsReadAsync())
            {
                foreach (var item in Items)
                    item.IsRead = true;
                SuccessMessage = "All marked as read.";
                await _realtime.RefreshUnreadCountAsync();
                // Force UI refresh for IsRead bindings
                var snapshot = Items.ToList();
                Items.Clear();
                foreach (var item in snapshot)
                    Items.Add(item);
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenAsync(AppNotificationDto? item)
    {
        if (item is null) return;

        try
        {
            if (!item.IsRead)
            {
                await _api.MarkNotificationReadAsync(item.Id);
                item.IsRead = true;
                await _realtime.RefreshUnreadCountAsync();
            }

            if (!string.IsNullOrWhiteSpace(item.LinkUrl) && item.TaskId is int taskId)
                await Shell.Current.GoToAsync($"taskdetail?id={taskId}");
            else if (item.TaskId is int id)
                await Shell.Current.GoToAsync($"taskdetail?id={id}");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
    }
}
