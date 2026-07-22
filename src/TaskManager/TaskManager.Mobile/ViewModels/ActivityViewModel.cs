using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TaskManager.Mobile.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

public partial class ActivityViewModel : BaseViewModel
{
    private readonly IApiService _api;

    public ActivityViewModel(IApiService api)
    {
        _api = api;
        Title = "Activity";
    }

    public ObservableCollection<ActivityItemDto> Items { get; } = new();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        try
        {
            if (!IsRefreshing) IsBusy = true;
            ClearError();

            var items = await _api.GetActivityFeedAsync() ?? [];
            Items.Clear();
            foreach (var item in items)
                Items.Add(item);
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
    private async Task OpenAsync(ActivityItemDto? item)
    {
        if (item?.TaskId is not int id)
            return;
        await Shell.Current.GoToAsync($"taskdetail?id={id}");
    }
}
