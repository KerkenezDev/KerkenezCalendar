using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KerkenezCalendar.Models;

namespace KerkenezCalendar.Services
{
    public static class AccountCryptoService
    {
        private static readonly byte[] PrimaryEntropy = Encoding.UTF8.GetBytes("Kerkenez.SecureAccounts.v1");
        private static readonly byte[] LegacyEmailSummarizerEntropy = Encoding.UTF8.GetBytes("EmailSummarizer.SecureAccounts.v1");
        private static readonly byte[] KerkenezMailEntropy = Encoding.UTF8.GetBytes("KerkenezMail.SecureAccounts.v1");

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNameCaseInsensitive = true
        };

        public static byte[] EncryptAccounts(List<EmailAccount> accounts)
        {
            if (accounts == null) accounts = new List<EmailAccount>();
            string json = JsonSerializer.Serialize(accounts, JsonOptions);
            byte[] plainBytes = Encoding.UTF8.GetBytes(json);
            return ProtectedData.Protect(plainBytes, PrimaryEntropy, DataProtectionScope.CurrentUser);
        }

        public static List<EmailAccount> DecryptAccounts(byte[] encryptedBytes)
        {
            if (encryptedBytes == null || encryptedBytes.Length == 0)
            {
                return new List<EmailAccount>();
            }

            // Try primary entropy first, then legacy entropies for 100% interoperability
            byte[][] candidateEntropies = { PrimaryEntropy, LegacyEmailSummarizerEntropy, KerkenezMailEntropy };

            foreach (var entropy in candidateEntropies)
            {
                try
                {
                    byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, entropy, DataProtectionScope.CurrentUser);
                    string json = Encoding.UTF8.GetString(plainBytes);
                    var accounts = JsonSerializer.Deserialize<List<EmailAccount>>(json, JsonOptions);
                    if (accounts != null)
                    {
                        return accounts;
                    }
                }
                catch
                {
                    // Continue to next entropy candidate
                }
            }

            return new List<EmailAccount>();
        }

        public static bool SaveToEncryptedFile(string filePath, List<EmailAccount> accounts)
        {
            try
            {
                string? dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                byte[] encryptedBytes = EncryptAccounts(accounts);
                string tempFile = filePath + ".tmp";
                File.WriteAllBytes(tempFile, encryptedBytes);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                File.Move(tempFile, filePath);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AccountCryptoService] Save error: {ex.Message}");
                return false;
            }
        }

        public static List<EmailAccount> LoadFromEncryptedFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return new List<EmailAccount>();
                }

                byte[] encryptedBytes = File.ReadAllBytes(filePath);
                return DecryptAccounts(encryptedBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AccountCryptoService] Read error: {ex.Message}");
                return new List<EmailAccount>();
            }
        }
    }
}
