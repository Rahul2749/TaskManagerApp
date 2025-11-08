// TaskManager.Client/Services/SidebarStateService.cs
namespace TaskManager.Client.Services
{
    public class SidebarStateService
    {
        public bool IsMobileSidebarOpen { get; set; } = false;

        public event Action? OnChange;

        public void ToggleMobileSidebar()
        {
            IsMobileSidebarOpen = !IsMobileSidebarOpen;
            NotifyStateChanged();
        }

        public void CloseMobileSidebar()
        {
            IsMobileSidebarOpen = false;
            NotifyStateChanged();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}