namespace TaskManager.Mobile.Controls;

public partial class AppPageHeader : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(AppPageHeader), string.Empty);

    public static readonly BindableProperty SubtitleProperty =
        BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(AppPageHeader), string.Empty);

    public static readonly BindableProperty ActionsProperty =
        BindableProperty.Create(nameof(Actions), typeof(View), typeof(AppPageHeader));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public View? Actions
    {
        get => (View?)GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    public AppPageHeader()
    {
        InitializeComponent();
    }
}
