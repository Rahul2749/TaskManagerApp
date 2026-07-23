namespace TaskManager.Mobile.Controls;

public partial class AppCard : ContentView
{
    public static readonly BindableProperty CardContentProperty =
        BindableProperty.Create(nameof(CardContent), typeof(View), typeof(AppCard));

    public View? CardContent
    {
        get => (View?)GetValue(CardContentProperty);
        set => SetValue(CardContentProperty, value);
    }

    public AppCard()
    {
        InitializeComponent();
    }
}
