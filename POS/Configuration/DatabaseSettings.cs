using System.IO;

namespace POS.Configuration
{
    public class DatabaseSettings
    {
        public string Provider { get; set; } = "SQLite";
        public Dictionary<string, string> ConnectionStrings { get; set; } = new();
        public string DataDirectory { get; set; } = string.Empty;
        public string BackupDirectory { get; set; } = string.Empty;
        public bool EnableAutoBackup { get; set; } = true;
        public int BackupIntervalMinutes { get; set; } = 5;

        public string GetConnectionString()
        {
            if (!ConnectionStrings.ContainsKey(Provider))
            {
                throw new InvalidOperationException($"Connection string for provider '{Provider}' not found.");
            }

            var connectionString = ConnectionStrings[Provider];
            var dataDirectory = Environment.ExpandEnvironmentVariables(DataDirectory);

            // Ensure directory exists
            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }

            return connectionString.Replace("{DataDirectory}", dataDirectory);
        }

        public string GetBackupDirectory()
        {
            var backupDir = Environment.ExpandEnvironmentVariables(BackupDirectory);
            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }
            return backupDir;
        }
    }

    public class GoogleDriveSettings
    {
        public bool Enabled { get; set; }
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string BackupFolderName { get; set; } = "POS_Backups";
        public int RetentionDays { get; set; } = 365;
    }

    public class LoggingSettings
    {
        public string LogLevel { get; set; } = "Information";
        public string LogDirectory { get; set; } = string.Empty;
        public int RetainDays { get; set; } = 30;
        public bool EnableTransactionLogging { get; set; } = true;
        public bool EnablePerformanceLogging { get; set; } = false;

        public string GetLogDirectory()
        {
            var logDir = Environment.ExpandEnvironmentVariables(LogDirectory);
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }
            return logDir;
        }
    }

    public class SecuritySettings
    {
        public bool EnableAuditLog { get; set; } = true;
        public bool RequireStrongPassword { get; set; } = false;
        public int SessionTimeoutMinutes { get; set; } = 480;
        public int MaxLoginAttempts { get; set; } = 5;
    }

    public class AppSettings
    {
        public DatabaseSettings DatabaseSettings { get; set; } = new();
        public GoogleDriveSettings GoogleDriveSettings { get; set; } = new();
        public LoggingSettings LoggingSettings { get; set; } = new();
        public SecuritySettings SecuritySettings { get; set; } = new();
    }
}
