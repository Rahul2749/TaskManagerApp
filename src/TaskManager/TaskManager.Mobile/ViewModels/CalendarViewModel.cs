using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TaskManager.Mobile.Services;
using TaskManager.Shared.Billing;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

public partial class CalendarViewModel : BaseViewModel
{
    private readonly IApiService _api;
    private readonly IEntitlementService _entitlements;
    private List<TaskDto> _allTasks = [];

    public CalendarViewModel(IApiService api, IEntitlementService entitlements)
    {
        _api = api;
        _entitlements = entitlements;
        Title = "Calendar";
        VisibleMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    }

    public ObservableCollection<CalendarDayCell> Days { get; } = new();
    public ObservableCollection<TaskDto> SelectedDayTasks { get; } = new();

    [ObservableProperty] private bool _hasAccess;
    [ObservableProperty] private string _upgradeMessage = string.Empty;
    [ObservableProperty] private DateTime _visibleMonth;
    [ObservableProperty] private string _monthLabel = string.Empty;
    [ObservableProperty] private DateTime? _selectedDate;
    [ObservableProperty] private string _selectedDayLabel = "Select a day";

    partial void OnVisibleMonthChanged(DateTime value) => RebuildGrid();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ClearError();

            await _entitlements.EnsureLoadedAsync();
            HasAccess = _entitlements.HasFeature(FeatureKeys.CalendarView);
            if (!HasAccess)
            {
                UpgradeMessage = "Calendar requires a Starter+ plan (calendar_view). Upgrade on the web Billing page.";
                return;
            }

            UpgradeMessage = string.Empty;
            _allTasks = await _api.GetTasksAsync() ?? [];
            RebuildGrid();
            if (SelectedDate is DateTime day)
                SelectDay(day);
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
    private void PreviousMonth() => VisibleMonth = VisibleMonth.AddMonths(-1);

    [RelayCommand]
    private void NextMonth() => VisibleMonth = VisibleMonth.AddMonths(1);

    [RelayCommand]
    private void SelectDayCell(CalendarDayCell? cell)
    {
        if (cell is null || !cell.InCurrentMonth)
            return;
        SelectDay(cell.Date);
    }

    [RelayCommand]
    private async Task OpenTaskAsync(TaskDto? task)
    {
        if (task?.Id is not int id)
            return;
        await Shell.Current.GoToAsync($"taskdetail?id={id}");
    }

    private void SelectDay(DateTime day)
    {
        SelectedDate = day.Date;
        SelectedDayLabel = day.ToString("ddd, MMM d");
        SelectedDayTasks.Clear();
        foreach (var t in _allTasks
                     .Where(t => t.DueDate?.Date == day.Date)
                     .OrderBy(t => t.Priority))
            SelectedDayTasks.Add(t);

        foreach (var cell in Days)
            cell.IsSelected = cell.Date.Date == day.Date;
    }

    private void RebuildGrid()
    {
        MonthLabel = VisibleMonth.ToString("MMMM yyyy");
        Days.Clear();

        var first = VisibleMonth;
        var start = first.AddDays(-(int)first.DayOfWeek);
        for (var i = 0; i < 42; i++)
        {
            var date = start.AddDays(i);
            var count = _allTasks.Count(t => t.DueDate?.Date == date.Date);
            Days.Add(new CalendarDayCell
            {
                Date = date,
                DayNumber = date.Day,
                InCurrentMonth = date.Month == VisibleMonth.Month,
                TaskCount = count,
                IsToday = date.Date == DateTime.Today,
                IsSelected = SelectedDate?.Date == date.Date
            });
        }
    }
}

public partial class CalendarDayCell : ObservableObject
{
    public DateTime Date { get; set; }
    public int DayNumber { get; set; }
    public bool InCurrentMonth { get; set; }
    public int TaskCount { get; set; }
    public bool IsToday { get; set; }

    [ObservableProperty]
    private bool _isSelected;

    public string CountLabel => TaskCount > 0 ? TaskCount.ToString() : string.Empty;
}
