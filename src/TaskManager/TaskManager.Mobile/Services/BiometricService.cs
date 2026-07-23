namespace TaskManager.Mobile.Services;

public interface IBiometricService
{
    Task<bool> IsAvailableAsync();
    Task<bool> AuthenticateAsync(string reason);
}

/// <summary>
/// Best-effort biometric gate. Uses platform APIs when available; otherwise fails closed
/// so callers can fall back to password sign-in.
/// </summary>
public sealed class BiometricService : IBiometricService
{
    private const string EnabledKey = "biometric_unlock_enabled";

    public static bool IsEnabled
    {
        get => Preferences.Default.Get(EnabledKey, false);
        set => Preferences.Default.Set(EnabledKey, value);
    }

    public Task<bool> IsAvailableAsync()
    {
#if ANDROID || IOS || MACCATALYST
        return Task.FromResult(true);
#else
        return Task.FromResult(false);
#endif
    }

    public async Task<bool> AuthenticateAsync(string reason)
    {
        try
        {
#if ANDROID
            return await AuthenticateAndroidAsync(reason);
#elif IOS || MACCATALYST
            return await AuthenticateAppleAsync(reason);
#else
            await Task.CompletedTask;
            return false;
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Biometric auth failed: {ex.Message}");
            return false;
        }
    }

#if ANDROID
    private static Task<bool> AuthenticateAndroidAsync(string reason)
    {
        var tcs = new TaskCompletionSource<bool>();
        var activity = Platform.CurrentActivity;
        if (activity is null)
        {
            tcs.TrySetResult(false);
            return tcs.Task;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                if (activity is not AndroidX.Fragment.App.FragmentActivity fragmentActivity)
                {
                    tcs.TrySetResult(false);
                    return;
                }

                var executor = Java.Util.Concurrent.Executors.NewSingleThreadExecutor()
                               ?? throw new InvalidOperationException("No executor");
                var callback = new BiometricCallback(tcs);
                var promptInfo = new AndroidX.Biometric.BiometricPrompt.PromptInfo.Builder()
                    .SetTitle("Unlock TaskManager")
                    .SetSubtitle(reason)
                    .SetNegativeButtonText("Cancel")
                    .Build();
                var prompt = new AndroidX.Biometric.BiometricPrompt(fragmentActivity, executor, callback);
                prompt.Authenticate(promptInfo);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                tcs.TrySetResult(false);
            }
        });

        return tcs.Task;
    }

    private sealed class BiometricCallback : AndroidX.Biometric.BiometricPrompt.AuthenticationCallback
    {
        private readonly TaskCompletionSource<bool> _tcs;

        public BiometricCallback(TaskCompletionSource<bool> tcs) => _tcs = tcs;

        public override void OnAuthenticationSucceeded(AndroidX.Biometric.BiometricPrompt.AuthenticationResult result) =>
            _tcs.TrySetResult(true);

        public override void OnAuthenticationError(int errorCode, Java.Lang.ICharSequence errString) =>
            _tcs.TrySetResult(false);

        public override void OnAuthenticationFailed()
        {
            // Keep waiting for another attempt / cancel.
        }
    }
#endif

#if IOS || MACCATALYST
    private static Task<bool> AuthenticateAppleAsync(string reason)
    {
        var tcs = new TaskCompletionSource<bool>();
        var context = new LocalAuthentication.LAContext();
        if (!context.CanEvaluatePolicy(LocalAuthentication.LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out _))
        {
            // Fall back to device passcode.
            if (!context.CanEvaluatePolicy(LocalAuthentication.LAPolicy.DeviceOwnerAuthentication, out _))
            {
                tcs.TrySetResult(false);
                return tcs.Task;
            }

            context.EvaluatePolicy(LocalAuthentication.LAPolicy.DeviceOwnerAuthentication, reason, (success, error) =>
            {
                tcs.TrySetResult(success);
            });
            return tcs.Task;
        }

        context.EvaluatePolicy(LocalAuthentication.LAPolicy.DeviceOwnerAuthenticationWithBiometrics, reason, (success, error) =>
        {
            tcs.TrySetResult(success);
        });
        return tcs.Task;
    }
#endif
}
