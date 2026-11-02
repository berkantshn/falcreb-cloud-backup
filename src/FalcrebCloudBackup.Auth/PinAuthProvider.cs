using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using FalcrebCloudBackup.Core.Abstractions;
using FalcrebCloudBackup.Core.Localization;
using FalcrebCloudBackup.Core.Storage;
using Newtonsoft.Json;
using OtpNet;
using QRCoder;

namespace FalcrebCloudBackup.Auth
{
    /// <summary>
    /// Local login: a PIN and/or an authenticator-app code (TOTP, RFC 6238), user-selectable, with
    /// at least one always required. The auth.dat file is a DPAPI-protected (CurrentUser scope) blob
    /// of an AuthRecord; PIN/recovery code are additionally PBKDF2-hashed inside it and the TOTP
    /// secret never leaves this machine — nothing resembling a plaintext secret touches disk outside
    /// the DPAPI envelope. Only gates the interactive UI — the silent, Task-Scheduler-triggered
    /// backup run never calls into this class.
    /// </summary>
    public class PinAuthProvider : IAuthProvider
    {
        private const string Issuer = "FalcrebCloudBackup";

        /// <summary>Holds a freshly generated secret between BeginTotpSetup and ConfirmTotpSetup — not persisted until confirmed.</summary>
        private string _pendingTotpSecret;

        public AuthSetupState State => File.Exists(AppPaths.AuthFilePath)
            ? AuthSetupState.Configured
            : AuthSetupState.NotConfigured;

        public string SetupPin(string pin)
        {
            var (pinHash, pinSalt) = SecretHasher.Hash(pin);
            var recoveryCode = RecoveryCodeGenerator.Generate();
            var (recoveryHash, recoverySalt) = SecretHasher.Hash(recoveryCode);

            var record = new AuthRecord
            {
                PinHash = pinHash,
                PinSalt = pinSalt,
                RecoveryCodeHash = recoveryHash,
                RecoveryCodeSalt = recoverySalt,
                PinEnabled = true
            };

            WriteRecord(record);
            return recoveryCode;
        }

        public bool TryUnlock(string pin)
        {
            if (!TryReadRecord(out var record))
            {
                return false;
            }

            return SecretHasher.Verify(pin, record.PinHash, record.PinSalt);
        }

        /// <summary>What the unlock screen should ask for right now.</summary>
        public (bool PinRequired, bool TotpRequired) GetLoginRequirements()
        {
            if (!TryReadRecord(out var record))
            {
                return (true, false);
            }

            return (record.PinEnabled, record.TotpEnabled);
        }

        /// <summary>The real unlock check — respects whichever factor(s) are currently enabled.</summary>
        public bool TryUnlockFull(string pin, string totpCode)
        {
            if (!TryReadRecord(out var record))
            {
                return false;
            }

            if (record.PinEnabled && !SecretHasher.Verify(pin ?? string.Empty, record.PinHash, record.PinSalt))
            {
                return false;
            }

            if (record.TotpEnabled && !VerifyTotpCode(record.TotpSecretBase32, totpCode))
            {
                return false;
            }

            return true;
        }

        public bool TryResetWithRecoveryCode(string recoveryCode, string newPin)
        {
            if (!TryReadRecord(out var record))
            {
                return false;
            }

            if (!SecretHasher.Verify(recoveryCode, record.RecoveryCodeHash, record.RecoveryCodeSalt))
            {
                return false;
            }

            // Recovery always lands back on "PIN only" — the surest way to guarantee this never
            // leaves someone locked out, regardless of what was enabled before (e.g. they lost the
            // phone that had their authenticator app).
            var (pinHash, pinSalt) = SecretHasher.Hash(newPin);
            record.PinHash = pinHash;
            record.PinSalt = pinSalt;
            record.PinEnabled = true;
            record.TotpEnabled = false;
            record.TotpSecretBase32 = null;
            WriteRecord(record);
            return true;
        }

        public bool IsTotpEnabled => TryReadRecord(out var record) && record.TotpEnabled;

        /// <summary>Generates a new (not-yet-active) secret and its setup QR code. Nothing is saved until ConfirmTotpSetup succeeds.</summary>
        public byte[] BeginTotpSetup(out string manualEntryKey)
        {
            var secretBytes = KeyGeneration.GenerateRandomKey(20);
            manualEntryKey = Base32Encoding.ToString(secretBytes);
            _pendingTotpSecret = manualEntryKey;

            var otpAuthUri = $"otpauth://totp/{Issuer}:{Uri.EscapeDataString(Environment.UserName)}?secret={manualEntryKey}&issuer={Issuer}";

            using (var qrGenerator = new QRCodeGenerator())
            {
                var qrData = qrGenerator.CreateQrCode(otpAuthUri, QRCodeGenerator.ECCLevel.Q);
                var pngQrCode = new PngByteQRCode(qrData);
                return pngQrCode.GetGraphic(10);
            }
        }

        /// <summary>Verifies the code against the pending secret from BeginTotpSetup and, if it matches, turns 2FA on.</summary>
        public bool ConfirmTotpSetup(string code)
        {
            if (_pendingTotpSecret == null || !VerifyTotpCode(_pendingTotpSecret, code))
            {
                return false;
            }

            var record = TryReadRecord(out var existing) ? existing : new AuthRecord();
            record.TotpSecretBase32 = _pendingTotpSecret;
            record.TotpEnabled = true;
            WriteRecord(record);
            _pendingTotpSecret = null;
            return true;
        }

        /// <summary>Throws if this would leave both factors off — PIN must be re-enabled first.</summary>
        public void DisableTotp()
        {
            if (!TryReadRecord(out var record))
            {
                return;
            }

            if (!record.PinEnabled)
            {
                throw new InvalidOperationException(Loc.T("Auth_ErrDisableTotp"));
            }

            record.TotpEnabled = false;
            record.TotpSecretBase32 = null;
            WriteRecord(record);
        }

        /// <summary>Throws if disabling PIN would leave both factors off — enable TOTP first.</summary>
        public void SetPinEnabled(bool enabled)
        {
            var record = TryReadRecord(out var existing) ? existing : new AuthRecord();
            if (!enabled && !record.TotpEnabled)
            {
                throw new InvalidOperationException(Loc.T("Auth_ErrDisablePin"));
            }

            record.PinEnabled = enabled;
            WriteRecord(record);
        }

        private static bool VerifyTotpCode(string secretBase32, string code)
        {
            if (string.IsNullOrWhiteSpace(secretBase32) || string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            var totp = new Totp(Base32Encoding.ToBytes(secretBase32));
            return totp.VerifyTotp(code.Trim(), out _, new VerificationWindow(previous: 1, future: 1));
        }

        private void WriteRecord(AuthRecord record)
        {
            AppPaths.EnsureCreated();
            var json = JsonConvert.SerializeObject(record);
            var plainBytes = Encoding.UTF8.GetBytes(json);
            var protectedBytes = ProtectedData.Protect(plainBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(AppPaths.AuthFilePath, protectedBytes);
        }

        private bool TryReadRecord(out AuthRecord record)
        {
            record = null;
            if (!File.Exists(AppPaths.AuthFilePath))
            {
                return false;
            }

            var protectedBytes = File.ReadAllBytes(AppPaths.AuthFilePath);
            var plainBytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(plainBytes);
            record = JsonConvert.DeserializeObject<AuthRecord>(json);
            return record != null;
        }
    }
}
