namespace TaskManager.Mobile.Helpers;

public static class UnsavedChangesGuard
{
    public const string Title = "Unsaved changes";
    public const string Message = "You have unsaved changes. Discard them and leave?";
    public const string Discard = "Discard";
    public const string KeepEditing = "Keep editing";

    /// <summary>
    /// Returns true when navigation away is allowed.
    /// </summary>
    public static async Task<bool> ConfirmDiscardAsync(Page page, bool hasUnsavedChanges)
    {
        if (!hasUnsavedChanges)
            return true;

        KeyboardHelper.Hide();

#pragma warning disable CS0618 // DisplayAlert is still widely used; DisplayAlertAsync exists on newer MAUI
        return await page.DisplayAlert(Title, Message, Discard, KeepEditing);
#pragma warning restore CS0618
    }
}
