using TaskManager.Mobile.Helpers;

namespace TaskManager.Mobile.ViewModels;

/// <summary>
/// Snapshot-based dirty tracking for create/edit forms.
/// Call <see cref="MarkClean"/> after load (and after resetting to baseline).
/// Call <see cref="AllowLeaveWithoutPrompt"/> before navigating away after a successful save/delete.
/// </summary>
public abstract partial class DirtyFormViewModel : BaseViewModel, IUnsavedChangesForm
{
    private string? _baseline;
    private bool _allowLeaveWithoutPrompt;

    public bool HasUnsavedChanges =>
        !_allowLeaveWithoutPrompt
        && _baseline is not null
        && !string.Equals(_baseline, BuildSnapshot(), StringComparison.Ordinal);

    protected void MarkClean()
    {
        _baseline = BuildSnapshot();
        _allowLeaveWithoutPrompt = false;
    }

    protected void AllowLeaveWithoutPrompt() => _allowLeaveWithoutPrompt = true;

    protected abstract string BuildSnapshot();

    protected static string SnapshotDate(DateTime? value) =>
        value?.Date.ToString("yyyy-MM-dd") ?? string.Empty;
}
