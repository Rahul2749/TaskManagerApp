namespace TaskManager.Mobile.Controls;

public partial class AppStatCard : ContentView
{
    public static readonly BindableProperty LabelProperty =
        BindableProperty.Create(nameof(Label), typeof(string), typeof(AppStatCard), string.Empty);

    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(nameof(Value), typeof(string), typeof(AppStatCard), "0");

    public static readonly BindableProperty AccentProperty =
        BindableProperty.Create(nameof(Accent), typeof(string), typeof(AppStatCard), "primary");

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>primary | success | warning | danger</summary>
    public string Accent
    {
        get => (string)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    public AppStatCard()
    {
        InitializeComponent();
    }
}
