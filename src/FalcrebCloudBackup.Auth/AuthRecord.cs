namespace FalcrebCloudBackup.Auth
{
    /// <summary>What's stored (DPAPI-encrypted as a whole) at AppPaths.AuthFilePath. Never holds plaintext secrets.</summary>
    internal class AuthRecord
    {
        public string PinHash { get; set; }
        public string PinSalt { get; set; }

        public string RecoveryCodeHash { get; set; }
        public string RecoveryCodeSalt { get; set; }

        /// <summary>Whether the PIN is checked at login. Can only be turned off while TOTP is enabled — see PinAuthProvider.SetPinEnabled.</summary>
        public bool PinEnabled { get; set; } = true;

        /// <summary>Base32 TOTP secret (the same one encoded into the setup QR code); null until 2FA is set up.</summary>
        public string TotpSecretBase32 { get; set; }

        /// <summary>Whether an authenticator-app code is checked at login. Can only be turned on once a secret has been confirmed working.</summary>
        public bool TotpEnabled { get; set; }
    }
}
