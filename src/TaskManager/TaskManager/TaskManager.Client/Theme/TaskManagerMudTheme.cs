using MudBlazor;

namespace TaskManager.Client.Theme
{
    /// <summary>
    /// MudBlazor theme aligned with the app's obsidian + indigo design tokens.
    /// </summary>
    public static class TaskManagerMudTheme
    {
        public static MudTheme Create() => new()
        {
            PaletteDark = new PaletteDark
            {
                Primary = "#6366F1",
                Secondary = "#06B6D4",
                Tertiary = "#818CF8",
                Background = "#050508",
                Surface = "#121219",
                AppbarBackground = "rgba(10, 10, 15, 0.85)",
                DrawerBackground = "rgba(10, 10, 15, 0.6)",
                DrawerText = "#94A3B8",
                TextPrimary = "#FFFFFF",
                TextSecondary = "#94A3B8",
                ActionDefault = "#94A3B8",
                Divider = "rgba(255,255,255,0.08)",
                Success = "#10B981",
                Warning = "#F59E0B",
                Error = "#EF4444",
                Info = "#06B6D4",
                TableLines = "rgba(255,255,255,0.06)",
                LinesDefault = "rgba(255,255,255,0.08)",
                OverlayDark = "rgba(0,0,0,0.65)"
            },
            Typography = new Typography
            {
                Default = new DefaultTypography
                {
                    FontFamily = new[] { "Plus Jakarta Sans", "Roboto", "Helvetica", "Arial", "sans-serif" }
                },
                H4 = new H4Typography { FontWeight = "700" },
                H5 = new H5Typography { FontWeight = "700" },
                H6 = new H6Typography { FontWeight = "600" },
                Button = new ButtonTypography { TextTransform = "none", FontWeight = "600" }
            },
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = "12px"
            }
        };
    }
}
