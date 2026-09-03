using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KerkenezCalendar.Models;

namespace KerkenezCalendar.Services
{
    public static class EventCryptoService
    {
        private static readonly byte[] PrimaryEntropy = Encoding.UTF8.GetBytes("Kerkenez.SecureEvents.v1");
        private static readonly byte[] FallbackEntropy = Encoding.UTF8.GetBytes("Kerkenez.SecureAccounts.v1");

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNameCaseInsensitive = true
        };

        public static byte[] EncryptEvents(List<CalendarEvent> events)
        {
            if (events == null) events = new List<CalendarEvent>();
            string json = JsonSerializer.Serialize(events, JsonOptions);
            byte[] plainBytes = Encoding.UTF8.GetBytes(json);
            return ProtectedData.Protect(plainBytes, PrimaryEntropy, DataProtectionScope.CurrentUser);
        }

        public static List<CalendarEvent> DecryptEvents(byte[] encryptedBytes)
        {
            if (encryptedBytes == null || encryptedBytes.Length == 0)
            {
                return new List<CalendarEvent>();
            }

            byte[][] candidateEntropies = { PrimaryEntropy, FallbackEntropy };

            foreach (var entropy in candidateEntropies)
            {
                try
                {
                    byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, entropy, DataProtectionScope.CurrentUser);
                    string json = Encoding.UTF8.GetString(plainBytes);
                    var events = JsonSerializer.Deserialize<List<CalendarEvent>>(json, JsonOptions);
                    if (events != null)
                    {
                        return events;
                    }
                }
                catch
                {
                    // Try next entropy
                }
            }

            return new List<CalendarEvent>();
        }

        public static bool SaveToEncryptedFile(string filePath, List<CalendarEvent> events)
        {
            try
            {
                string? dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                byte[] encryptedBytes = EncryptEvents(events);
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
                System.Diagnostics.Debug.WriteLine($"[EventCryptoService] Save error: {ex.Message}");
                return false;
            }
        }

        public static List<CalendarEvent> LoadFromEncryptedFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return new List<CalendarEvent>();
                }

                byte[] encryptedBytes = File.ReadAllBytes(filePath);
                return DecryptEvents(encryptedBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EventCryptoService] Load error: {ex.Message}");
                return new List<CalendarEvent>();
            }
        }
    }
}
