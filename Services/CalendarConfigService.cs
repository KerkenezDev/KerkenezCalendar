using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using KerkenezCalendar.Models;

namespace KerkenezCalendar.Services
{
    public class CalendarConfigService
    {
        public static readonly string AppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        // Core Kerkenez folder for shared data (accounts.dat)
        public static readonly string KerkenezRootFolder = Path.Combine(AppDataFolder, "Kerkenez");
        public static readonly string AccountsFilePath = Path.Combine(KerkenezRootFolder, "accounts.dat");

        // Calendar configuration & data folder
        public static readonly string CalendarFolder = Path.Combine(KerkenezRootFolder, "calendar");

        public static readonly string ConfigFilePath = Path.Combine(CalendarFolder, "config.json");
        public static readonly string EventsFilePath = Path.Combine(CalendarFolder, "events.dat");
        public static readonly string LegacyEventsJsonPath = Path.Combine(CalendarFolder, "events.json");

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public CalendarSettings Settings { get; private set; }
        public bool IsFirstInstallation { get; private set; }
        public event Action? SettingsChanged;

        public CalendarConfigService()
        {
            IsFirstInstallation = !File.Exists(ConfigFilePath);
            EnsureDirectoriesAndMigrations();
            Settings = LoadConfig();
        }

        private static void EnsureDirectoriesAndMigrations()
        {
            try
            {
                if (!Directory.Exists(KerkenezRootFolder))
                {
                    Directory.CreateDirectory(KerkenezRootFolder);
                }

                if (!Directory.Exists(CalendarFolder))
                {
                    Directory.CreateDirectory(CalendarFolder);
                }

                // Auto-migrate accounts.dat from EmailSummarizer if not present in %APPDATA%\Kerkenez\accounts.dat
                if (!File.Exists(AccountsFilePath))
                {
                    string legacySummarizerAccounts = Path.Combine(AppDataFolder, "EmailSummarizer", "accounts.dat");
                    string legacyMailAccounts = Path.Combine(AppDataFolder, "KerkenezMail", "accounts.dat");

                    if (File.Exists(legacySummarizerAccounts))
                    {
                        File.Copy(legacySummarizerAccounts, AccountsFilePath, true);
                        System.Diagnostics.Debug.WriteLine("[CalendarConfigService] Migrated accounts.dat from EmailSummarizer to Kerkenez.");
                    }
                    else if (File.Exists(legacyMailAccounts))
                    {
                        File.Copy(legacyMailAccounts, AccountsFilePath, true);
                        System.Diagnostics.Debug.WriteLine("[CalendarConfigService] Migrated accounts.dat from KerkenezMail to Kerkenez.");
                    }
                }

                // Auto-migrate events.json to encrypted events.dat (DPAPI)
                if (!File.Exists(EventsFilePath) && File.Exists(LegacyEventsJsonPath))
                {
                    try
                    {
                        string json = File.ReadAllText(LegacyEventsJsonPath);
                        var loaded = JsonSerializer.Deserialize<List<CalendarEvent>>(json, JsonOptions);
                        if (loaded != null && loaded.Count > 0)
                        {
                            EventCryptoService.SaveToEncryptedFile(EventsFilePath, loaded);
                            System.Diagnostics.Debug.WriteLine($"[CalendarConfigService] Encrypted and migrated {loaded.Count} events from events.json to events.dat.");
                        }
                        File.Delete(LegacyEventsJsonPath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CalendarConfigService] events.json migration error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CalendarConfigService] Directory init error: {ex.Message}");
            }
        }

        public CalendarSettings LoadConfig()
        {
            try
            {
                EnsureDirectoriesAndMigrations();

                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var loaded = JsonSerializer.Deserialize<CalendarSettings>(json, JsonOptions);
                    if (loaded != null)
                    {
                        Settings = loaded;
                        return Settings;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CalendarConfigService] Error loading config: {ex.Message}");
            }

            var defaults = CalendarSettings.CreateDefault();
            SaveConfig(defaults);
            return defaults;
        }

        public bool SaveConfig(CalendarSettings? settingsToSave = null)
        {
            try
            {
                EnsureDirectoriesAndMigrations();
                if (settingsToSave != null)
                {
                    Settings = settingsToSave;
                }

                string json = JsonSerializer.Serialize(Settings, JsonOptions);
                File.WriteAllText(ConfigFilePath, json);

                // Sync Windows logon run key
                StartupRegistrationService.SetStartupEnabled(Settings.StartWithWindows);

                CalendarEventService.SignalDataChanged();
                SettingsChanged?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CalendarConfigService] Error saving config: {ex.Message}");
                return false;
            }
        }

        public List<EmailAccount> GetAccounts()
        {
            try
            {
                EnsureDirectoriesAndMigrations();
                if (File.Exists(AccountsFilePath))
                {
                    return AccountCryptoService.LoadFromEncryptedFile(AccountsFilePath);
                }

                return new List<EmailAccount>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CalendarConfigService] Error getting accounts: {ex.Message}");
                return new List<EmailAccount>();
            }
        }

        public bool SaveAccounts(List<EmailAccount> accounts)
        {
            try
            {
                EnsureDirectoriesAndMigrations();
                bool saved = AccountCryptoService.SaveToEncryptedFile(AccountsFilePath, accounts);
                if (saved)
                {
                    Settings.AccountIds = accounts.Select(a => a.Id).ToList();
                    SaveConfig();
                    SettingsChanged?.Invoke();
                }
                return saved;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CalendarConfigService] Error saving accounts: {ex.Message}");
                return false;
            }
        }
    }
}
