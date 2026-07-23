using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TaskManager.Mobile.Services;
using TaskManager.Shared.Billing;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

public partial class AutomationsViewModel : BaseViewModel
{
    private readonly IApiService _api;
    private readonly IEntitlementService _entitlements;

    public AutomationsViewModel(IApiService api, IEntitlementService entitlements)
    {
        _api = api;
        _entitlements = entitlements;
        Title = "Automations";
    }

    public ObservableCollection<AutomationRuleDto> Rules { get; } = new();

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
            HasAccess = _entitlements.HasFeature(FeatureKeys.Automations);
            if (!HasAccess)
            {
                UpgradeMessage = "Automations require Professional+. Manage rules on web.";
                return;
            }

            UpgradeMessage = string.Empty;
            Rules.Clear();
            var items = await _api.GetAutomationRulesAsync() ?? [];
            foreach (var r in items)
                Rules.Add(r);
        }
        catch (Exception ex)
        {
            // Org members without Manager/Admin get 403 — show friendly message
            SetError("Automations are managed by admins/managers. Open Billing or the web app to upgrade or edit rules.");
            HasAccess = _entitlements.HasFeature(FeatureKeys.Automations);
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task GoToBillingAsync() => await Shell.Current.GoToAsync("//billing");
}
