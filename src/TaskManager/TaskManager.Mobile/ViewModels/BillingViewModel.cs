using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TaskManager.Mobile.Configuration;
using TaskManager.Mobile.Helpers;
using TaskManager.Mobile.Services;
using TaskManager.Shared.DTOs.Billing;

namespace TaskManager.Mobile.ViewModels;

public partial class BillingViewModel : BaseViewModel
{
    private readonly IApiService _api;
    private readonly IAuthService _auth;
    private readonly IEntitlementService _entitlements;

    public BillingViewModel(IApiService api, IAuthService auth, IEntitlementService entitlements)
    {
        _api = api;
        _auth = auth;
        _entitlements = entitlements;
        Title = "Billing";
    }

    public ObservableCollection<InvoiceDto> Invoices { get; } = new();
    public ObservableCollection<string> FeatureList { get; } = new();

    [ObservableProperty] private bool _canManageBilling;
    [ObservableProperty] private string _planName = string.Empty;
    [ObservableProperty] private string _planCode = string.Empty;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private string _seatsLabel = string.Empty;
    [ObservableProperty] private string _periodLabel = string.Empty;
    [ObservableProperty] private string _upgradeHint =
        "To change plans or pay, open Billing on the web app.";

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        try
        {
            if (!IsRefreshing) IsBusy = true;
            ClearError();

            var user = await _auth.GetCurrentUserAsync();
            CanManageBilling = AppRoles.IsOrgAdmin(user?.Role);

            await _entitlements.EnsureLoadedAsync(forceReload: true);
            var sub = _entitlements.Current ?? await _api.GetSubscriptionAsync();
            if (sub is null)
            {
                SetError("Could not load subscription.");
                return;
            }

            PlanName = sub.PlanName;
            PlanCode = sub.PlanCode;
            Status = sub.Status;
            SeatsLabel = $"{sub.Seats} seat(s)";
            PeriodLabel = sub.CurrentPeriodEnd is DateTime end
                ? $"Period ends {end.ToLocalTime():d}"
                : (sub.TrialEndsAt is DateTime trial ? $"Trial ends {trial.ToLocalTime():d}" : string.Empty);

            FeatureList.Clear();
            foreach (var f in sub.Features.OrderBy(f => f))
                FeatureList.Add(f);

            Invoices.Clear();
            if (CanManageBilling)
            {
                try
                {
                    var invoices = await _api.GetInvoicesAsync() ?? [];
                    foreach (var inv in invoices.Take(20))
                        Invoices.Add(inv);
                }
                catch
                {
                    // Non-admins or empty invoice set — ignore.
                }
            }
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
    private async Task OpenWebBillingAsync()
    {
        var url = $"{ApiSettings.ProductionBaseUrl.TrimEnd('/')}/billing";
#if DEBUG
        // Prefer the same host the app is talking to when debugging.
        url = $"{ApiSettings.BaseUrl.TrimEnd('/')}/billing";
#endif
        try
        {
            await Browser.Default.OpenAsync(url, BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception ex)
        {
            SetError($"Could not open browser: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task OpenWebPricingAsync()
    {
        var url = $"{ApiSettings.ProductionBaseUrl.TrimEnd('/')}/pricing";
#if DEBUG
        url = $"{ApiSettings.BaseUrl.TrimEnd('/')}/pricing";
#endif
        try
        {
            await Browser.Default.OpenAsync(url, BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception ex)
        {
            SetError($"Could not open browser: {ex.Message}");
        }
    }
}
