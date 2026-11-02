namespace FalcrebCloudBackup.Core.Abstractions
{
    public enum AuthSetupState
    {
        /// <summary>No PIN configured yet — app should show the "set a PIN" screen.</summary>
        NotConfigured,
        Configured
    }

    /// <summary>
    /// Gate for opening the app. Today the only implementation is a local PIN
    /// (FalcrebCloudBackup.Auth.PinAuthProvider). A future EmailPasswordAuthProvider can implement
    /// this same interface to add a real membership system later without touching the UI or the
    /// rest of the app — everything here talks to IAuthProvider, never to PinAuthProvider directly.
    /// </summary>
    public interface IAuthProvider
    {
        AuthSetupState State { get; }

        /// <summary>First-run setup. Returns a one-time recovery code the user must save; it is never stored in the clear.</summary>
        string SetupPin(string pin);

        bool TryUnlock(string pin);

        /// <summary>Resets the PIN using the recovery code generated at setup time.</summary>
        bool TryResetWithRecoveryCode(string recoveryCode, string newPin);
    }
}
