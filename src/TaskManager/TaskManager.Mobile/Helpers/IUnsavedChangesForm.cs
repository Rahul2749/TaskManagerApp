namespace TaskManager.Mobile.Helpers;

/// <summary>
/// Forms that track edits and should prompt before discarding.
/// </summary>
public interface IUnsavedChangesForm
{
    bool HasUnsavedChanges { get; }
}
