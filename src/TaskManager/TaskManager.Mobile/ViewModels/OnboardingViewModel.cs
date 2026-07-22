using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskManager.Mobile.Helpers;
using TaskManager.Mobile.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.ViewModels;

public partial class OnboardingViewModel : BaseViewModel
{
    private readonly IApiService _api;
    private readonly IAuthService _auth;
    private readonly IAppNavigationService _navigation;
    private readonly ISecureTokenStorage _storage;

    public OnboardingViewModel(
        IApiService api,
        IAuthService auth,
        IAppNavigationService navigation,
        ISecureTokenStorage storage)
    {
        _api = api;
        _auth = auth;
        _navigation = navigation;
        _storage = storage;
        Title = "Set up workspace";
        InviteRoles = new[] { AppRoles.User, AppRoles.Manager };
        SelectedInviteRole = AppRoles.User;
    }

    public IReadOnlyList<string> InviteRoles { get; }

    [ObservableProperty] private string _organizationName = string.Empty;
    [ObservableProperty] private string _projectName = string.Empty;
    [ObservableProperty] private string _inviteEmail = string.Empty;
    [ObservableProperty] private string _selectedInviteRole = AppRoles.User;
    [ObservableProperty] private string _statusSummary = string.Empty;
    [ObservableProperty] private string _successMessage = string.Empty;
    [ObservableProperty] private int _projectCount;
    [ObservableProperty] private int _pendingInviteCount;
    [ObservableProperty] private int _memberCount;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            ClearError();
            var status = await _api.GetOnboardingStatusAsync();
            if (status is null)
            {
                SetError("Could not load onboarding status.");
                return;
            }

            OrganizationName = status.OrganizationName;
            ProjectCount = status.ProjectCount;
            PendingInviteCount = status.PendingInviteCount;
            MemberCount = status.MemberCount;
            StatusSummary =
                $"{status.MemberCount} member(s) · {status.ProjectCount} project(s) · {status.PendingInviteCount} pending invite(s)";

            if (status.Completed)
                await FinishLocalAsync();
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
    private async Task SaveWorkspaceAsync()
    {
        if (IsBusy) return;
        ClearError();
        SuccessMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(OrganizationName))
        {
            SetError("Workspace name is required.");
            return;
        }

        try
        {
            IsBusy = true;
            var current = await _api.GetOrganizationSettingsAsync();
            var dto = new UpdateOrganizationSettingsDto
            {
                Name = OrganizationName.Trim(),
                Description = current?.Description,
                LogoUrl = current?.LogoUrl,
                TimeZoneId = current?.TimeZoneId ?? "Asia/Kolkata",
                BrandPrimaryColor = current?.BrandPrimaryColor
            };

            var (settings, error) = await _api.UpdateOrganizationSettingsAsync(dto);
            if (settings is null)
            {
                SetError(error ?? "Could not save workspace.");
                return;
            }

            SuccessMessage = "Workspace name saved.";
            await RefreshStatusQuietAsync();
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
    private async Task SendInviteAsync()
    {
        if (IsBusy) return;
        ClearError();
        SuccessMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(InviteEmail))
        {
            SetError("Enter an email to invite.");
            return;
        }

        try
        {
            IsBusy = true;
            var (invite, error) = await _api.CreateInviteAsync(new CreateInviteDto
            {
                Email = InviteEmail.Trim(),
                Role = SelectedInviteRole
            });

            if (invite is null)
            {
                SetError(error ?? "Invite failed.");
                return;
            }

            InviteEmail = string.Empty;
            SuccessMessage = $"Invite sent to {invite.Email}.";
            await RefreshStatusQuietAsync();
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
    private async Task CreateProjectAsync()
    {
        if (IsBusy) return;
        ClearError();
        SuccessMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            SetError("Enter a project name.");
            return;
        }

        try
        {
            IsBusy = true;
            var created = await _api.CreateProjectAsync(new ProjectDto
            {
                Name = ProjectName.Trim(),
                Description = "Created during onboarding",
                Status = "Active",
                StartDate = DateTime.UtcNow.Date
            });

            if (created is null)
            {
                SetError("Could not create project.");
                return;
            }

            ProjectName = string.Empty;
            SuccessMessage = $"Project “{created.Name}” created.";
            await RefreshStatusQuietAsync();
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
    private async Task CompleteAsync()
    {
        if (IsBusy) return;
        ClearError();

        try
        {
            IsBusy = true;
            var status = await _api.CompleteOnboardingAsync();
            if (status is null)
            {
                SetError("Could not finish onboarding.");
                return;
            }

            await FinishLocalAsync();
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
    private async Task SkipAsync() => await CompleteAsync();

    private async Task RefreshStatusQuietAsync()
    {
        var status = await _api.GetOnboardingStatusAsync();
        if (status is null) return;
        ProjectCount = status.ProjectCount;
        PendingInviteCount = status.PendingInviteCount;
        MemberCount = status.MemberCount;
        OrganizationName = status.OrganizationName;
        StatusSummary =
            $"{status.MemberCount} member(s) · {status.ProjectCount} project(s) · {status.PendingInviteCount} pending invite(s)";
    }

    private async Task FinishLocalAsync()
    {
        var user = await _auth.GetCurrentUserAsync();
        if (user is not null)
        {
            user.NeedsOnboarding = false;
            // Re-save via a lightweight refresh if possible; otherwise update stored user JSON.
            var access = await _storage.GetAccessTokenAsync();
            var refresh = await _storage.GetRefreshTokenAsync();
            if (!string.IsNullOrEmpty(access) && !string.IsNullOrEmpty(refresh))
            {
                await _storage.SaveSessionAsync(new TokenDto
                {
                    AccessToken = access,
                    RefreshToken = refresh,
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                    User = user
                });
            }
        }

        await _navigation.GoToMainAsync();
    }
}
