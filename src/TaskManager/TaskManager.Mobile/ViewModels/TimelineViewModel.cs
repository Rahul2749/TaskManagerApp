using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TaskManager.Mobile.Services;
using TaskManager.Shared.Billing;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

public partial class TimelineViewModel : BaseViewModel
{
    private readonly IApiService _api;
    private readonly IEntitlementService _entitlements;

    public TimelineViewModel(IApiService api, IEntitlementService entitlements)
    {
        _api = api;
        _entitlements = entitlements;
        Title = "Timeline";
    }

    public ObservableCollection<TimelineTaskDto> Tasks { get; } = new();
    public ObservableCollection<TaskDependencyDto> Dependencies { get; } = new();

    [ObservableProperty] private bool _hasAccess;
    [ObservableProperty] private string _upgradeMessage = string.Empty;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            ClearError();
            await _entitlements.EnsureLoadedAsync();
            HasAccess = _entitlements.HasFeature(FeatureKeys.TimelineGantt);
            if (!HasAccess)
            {
                UpgradeMessage = "Timeline requires Professional+ (timeline_gantt).";
                return;
            }

            UpgradeMessage = string.Empty;
            var data = await _api.GetTimelineAsync();
            Tasks.Clear();
            Dependencies.Clear();
            if (data is null) return;
            foreach (var t in data.Tasks)
                Tasks.Add(t);
            foreach (var d in data.Dependencies)
                Dependencies.Add(d);
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
    private async Task OpenTaskAsync(TimelineTaskDto? task)
    {
        if (task is null) return;
        await Shell.Current.GoToAsync($"taskdetail?id={task.Id}");
    }

    [RelayCommand]
    private async Task GoToBillingAsync() => await Shell.Current.GoToAsync("//billing");
}
