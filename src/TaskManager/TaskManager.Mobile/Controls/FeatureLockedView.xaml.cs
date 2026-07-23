namespace TaskManager.Mobile.Controls;

public partial class FeatureLockedView : ContentView
{
    public static readonly BindableProperty MessageProperty =
        BindableProperty.Create(nameof(Message), typeof(string), typeof(FeatureLockedView),
            "This feature is not on your current plan.");

    public static readonly BindableProperty UpgradeCommandProperty =
        BindableProperty.Create(nameof(UpgradeCommand), typeof(System.Windows.Input.ICommand), typeof(FeatureLockedView));

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public System.Windows.Input.ICommand? UpgradeCommand
    {
        get => (System.Windows.Input.ICommand?)GetValue(UpgradeCommandProperty);
        set => SetValue(UpgradeCommandProperty, value);
    }

    public FeatureLockedView()
    {
        InitializeComponent();
    }
}
