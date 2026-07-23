using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TaskManager.Mobile.Services;
using TaskManager.Shared.Billing;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

public partial class TimesheetsViewModel : BaseViewModel
{
    private readonly IApiService _api;
    private readonly IEntitlementService _entitlements;

    public TimesheetsViewModel(IApiService api, IEntitlementService entitlements)
    {
        _api = api;
        _entitlements = entitlements;
        Title = "Timesheets";
        WeekStart = StartOfWeek(DateTime.UtcNow.Date);
    }

    public ObservableCollection<TimeEntryDto> Entries { get; } = new();
    public ObservableCollection<TaskDto> Tasks { get; } = new();

    [ObservableProperty] private bool _hasAccess;
    [ObservableProperty] private string _upgradeMessage = string.Empty;
    [ObservableProperty] private DateTime _weekStart;
    [ObservableProperty] private decimal _totalHours;
    [ObservableProperty] private TaskDto? _selectedTask;
    [ObservableProperty] private string _hoursText = "1";
    [ObservableProperty] private string? _notes;

    public string WeekLabel => $"Week of {WeekStart:MMM d, yyyy}";

    partial void OnWeekStartChanged(DateTime value) => OnPropertyChanged(nameof(WeekLabel));

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            ClearError();
            await _entitlements.EnsureLoadedAsync();
            HasAccess = _entitlements.HasFeature(FeatureKeys.TimeTracking);
            if (!HasAccess)
            {
                UpgradeMessage = "Time tracking requires Professional+.";
                return;
            }

            UpgradeMessage = string.Empty;
            if (Tasks.Count == 0)
            {
                var tasks = await _api.GetTasksAsync() ?? [];
                foreach (var t in tasks.Where(t => t.Id.HasValue))
                    Tasks.Add(t);
                SelectedTask ??= Tasks.FirstOrDefault();
            }

            var sheet = await _api.GetTimesheetAsync(WeekStart);
            Entries.Clear();
            TotalHours = sheet?.TotalHours ?? 0;
            if (sheet?.Entries is null) return;
            foreach (var e in sheet.Entries)
                Entries.Add(e);
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
    private async Task PrevWeekAsync()
    {
        WeekStart = WeekStart.AddDays(-7);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task NextWeekAsync()
    {
        WeekStart = WeekStart.AddDays(7);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task LogTimeAsync()
    {
        if (SelectedTask?.Id is not int taskId) return;
        if (!decimal.TryParse(HoursText, out var hours) || hours < 0.25m)
        {
            SetError("Enter hours (minimum 0.25).");
            return;
        }

        var (entry, error) = await _api.CreateTimeEntryAsync(new CreateTimeEntryDto
        {
            TaskId = taskId,
            WorkDate = DateTime.UtcNow.Date,
            Hours = hours,
            Notes = Notes
        });
        if (entry is null)
        {
            SetError(error ?? "Could not log time.");
            return;
        }

        Notes = null;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task GoToBillingAsync() => await Shell.Current.GoToAsync("//billing");

    private static DateTime StartOfWeek(DateTime day)
    {
        var diff = ((int)day.DayOfWeek + 6) % 7;
        return day.AddDays(-diff);
    }
}
