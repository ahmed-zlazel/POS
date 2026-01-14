using Serilog;
using Serilog.Events;
using POS.Configuration;
using System.IO;

namespace POS.Logging
{
    public static class LoggerConfiguration
    {
        public static void ConfigureLogger()
        {
            var loggingSettings = ConfigurationManager.AppSettings.LoggingSettings;
            var logDirectory = loggingSettings.GetLogDirectory();

            var logLevel = Enum.Parse<LogEventLevel>(loggingSettings.LogLevel, true);

            var logConfig = new Serilog.LoggerConfiguration()
                .MinimumLevel.Is(logLevel)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "POS")
                .WriteTo.File(
                    path: Path.Combine(logDirectory, "pos-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: loggingSettings.RetainDays,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                );

            // Transaction logging - separate file for critical operations
            if (loggingSettings.EnableTransactionLogging)
            {
                logConfig.WriteTo.File(
                    path: Path.Combine(logDirectory, "transactions-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: loggingSettings.RetainDays * 2, // Keep transaction logs longer
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                );
            }

            // Performance logging
            if (loggingSettings.EnablePerformanceLogging)
            {
                logConfig.WriteTo.File(
                    path: Path.Combine(logDirectory, "performance-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    restrictedToMinimumLevel: LogEventLevel.Debug,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Message:lj}{NewLine}"
                );
            }

            // Error logging - separate file for easy monitoring
            logConfig.WriteTo.File(
                path: Path.Combine(logDirectory, "errors-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: loggingSettings.RetainDays * 3, // Keep errors longer
                restrictedToMinimumLevel: LogEventLevel.Error,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}{NewLine}---{NewLine}"
            );

            Log.Logger = logConfig.CreateLogger();

            Log.Information("POS Application logging initialized");
        }

        public static void CloseLogger()
        {
            Log.CloseAndFlush();
        }
    }

    public static class AppLogger
    {
        public static void LogTransaction(string operation, string details, object? data = null)
        {
            Log.Information("[TRANSACTION] {Operation}: {Details} | Data: {@Data}", operation, details, data);
        }

        public static void LogError(string operation, Exception ex, object? context = null)
        {
            Log.Error(ex, "[ERROR] {Operation} failed | Context: {@Context}", operation, context);
        }

        public static void LogWarning(string message, object? context = null)
        {
            Log.Warning("[WARNING] {Message} | Context: {@Context}", message, context);
        }

        public static void LogInfo(string message, object? data = null)
        {
            Log.Information("{Message} | Data: {@Data}", message, data);
        }

        public static void LogPerformance(string operation, long milliseconds, object? metrics = null)
        {
            Log.Debug("[PERFORMANCE] {Operation} took {Milliseconds}ms | Metrics: {@Metrics}", operation, milliseconds, metrics);
        }

        public static void LogCritical(string message, Exception? ex = null, object? context = null)
        {
            Log.Fatal(ex, "[CRITICAL] {Message} | Context: {@Context}", message, context);
        }
    }
}
