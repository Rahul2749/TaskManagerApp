namespace TaskManager.Mobile.Controls;

public partial class SkeletonCard : ContentView
{
    public static readonly BindableProperty IsLoadingProperty = BindableProperty.Create(
        nameof(IsLoading),
        typeof(bool),
        typeof(SkeletonCard),
        false,
        propertyChanged: OnIsLoadingChanged);

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public SkeletonCard()
    {
        InitializeComponent();
        IsVisible = false; // Hidden by default
    }

    private static void OnIsLoadingChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SkeletonCard skeleton)
        {
            var isLoading = (bool)newValue;
            skeleton.IsVisible = isLoading;
            if (isLoading)
            {
                skeleton.StartPulseAnimation();
            }
            else
            {
                skeleton.StopAnimation();
            }
        }
    }

    private void StartPulseAnimation()
    {
        var animation = new Animation(v => SkeletonFrame.Opacity = v, 0.4, 1.0, Easing.CubicInOut);
        animation.Commit(this, "PulseAnimation", length: 800, repeat: () => true);
    }

    private void StopAnimation()
    {
        this.AbortAnimation("PulseAnimation");
        SkeletonFrame.Opacity = 1.0;
    }
}
