using System;
using System.Text.Json.Serialization;

namespace KerkenezCalendar.Models
{
    public class EmailAccount
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Account";
        public string Email { get; set; } = "";
        public string AppPassword { get; set; } = "";
        public string Host { get; set; } = "imap.gmail.com";
        public int Port { get; set; } = 993;
        public bool UseSsl { get; set; } = true;
        public bool IsEnabled { get; set; } = true;

        // Provider & OAuth
        public string Provider { get; set; } = "Custom";
        public string EncryptedRefreshToken { get; set; } = "";
        public string EncryptedAccessToken { get; set; } = "";
        public DateTime? AccessTokenExpiresUtc { get; set; }
        public DateTime? LastRefreshedUtc { get; set; }

        [JsonIgnore]
        public bool IsOutlookOAuth => string.Equals(Provider, "OutlookOAuth", StringComparison.OrdinalIgnoreCase);

        // SMTP Settings
        public string SmtpHost { get; set; } = "";
        public int SmtpPort { get; set; } = 587;
        public bool SmtpUseSsl { get; set; } = false;

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Email) ? Name : $"{Name} ({Email})";
        }
    }
}
