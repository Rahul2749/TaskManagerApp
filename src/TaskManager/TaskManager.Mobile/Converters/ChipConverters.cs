using System.Globalization;

namespace TaskManager.Mobile.Converters;

public sealed class StatusToChipBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value?.ToString() ?? string.Empty;
        return status switch
        {
            "Completed" or "Closed" or "Tested" => Color.FromArgb("#CDE2DB"),
            "InProgress" or "Assigned" => Color.FromArgb("#EAF4FB"),
            "NotAssigned" => Color.FromArgb("#E8E5F2"),
            _ => Color.FromArgb("#F2E5DD")
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class StatusToChipTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value?.ToString() ?? string.Empty;
        return status switch
        {
            "Completed" or "Closed" or "Tested" => Color.FromArgb("#2F6B5A"),
            "InProgress" or "Assigned" => Color.FromArgb("#3D6F94"),
            "NotAssigned" => Color.FromArgb("#5C568A"),
            _ => Color.FromArgb("#8A6550")
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class PriorityToChipBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var priority = value?.ToString() ?? string.Empty;
        return priority switch
        {
            "High" => Color.FromArgb("#FCE9EE"),
            "Medium" => Color.FromArgb("#F2E5DD"),
            _ => Color.FromArgb("#E6F0F2")
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class PriorityToChipTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var priority = value?.ToString() ?? string.Empty;
        return priority switch
        {
            "High" => Color.FromArgb("#9B4D5C"),
            "Medium" => Color.FromArgb("#8A6550"),
            _ => Color.FromArgb("#668091")
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
