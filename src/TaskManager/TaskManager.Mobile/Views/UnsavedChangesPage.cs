using TaskManager.Mobile.Helpers;

namespace TaskManager.Mobile.Views;

/// <summary>
/// ContentPage that prompts when leaving with unsaved form edits
/// (toolbar back, Android hardware back, and Shell pop).
/// BindingContext should implement <see cref="IUnsavedChangesForm"/>.
/// </summary>
public class UnsavedChangesPage : ContentPage
{
    private bool _navigatingAway;
    private bool _shellHooked;

    protected override void OnAppearing()
    {
        base.OnAppearing();

        Shell.SetBackButtonBehavior(this, new BackButtonBehavior
        {
            Command = new Command(async () => await TryNavigateBackAsync())
        });

        if (!_shellHooked && Shell.Current is not null)
        {
            Shell.Current.Navigating += OnShellNavigating;
            _shellHooked = true;
        }
    }

    protected override void OnDisappearing()
    {
        if (_shellHooked && Shell.Current is not null)
        {
            Shell.Current.Navigating -= OnShellNavigating;
            _shellHooked = false;
        }

        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        _ = TryNavigateBackAsync();
        return true;
    }

    private async void OnShellNavigating(object? sender, ShellNavigatingEventArgs e)
    {
        if (_navigatingAway || e.Source is not (ShellNavigationSource.Pop or ShellNavigationSource.PopToRoot))
            return;

        if (BindingContext is not IUnsavedChangesForm form || !form.HasUnsavedChanges)
            return;

        // Cancel the automatic pop, then ask; if discarded, pop ourselves.
        e.Cancel();
        await TryNavigateBackAsync();
    }

    protected async Task TryNavigateBackAsync()
    {
        if (_navigatingAway)
            return;

        KeyboardHelper.Hide();

        if (BindingContext is IUnsavedChangesForm form &&
            !await UnsavedChangesGuard.ConfirmDiscardAsync(this, form.HasUnsavedChanges))
        {
            return;
        }

        _navigatingAway = true;
        try
        {
            if (Navigation.NavigationStack.Count > 1)
                await Navigation.PopAsync();
            else if (Shell.Current is not null)
                await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Navigate back failed: {ex.Message}");
        }
        finally
        {
            _navigatingAway = false;
        }
    }
}
