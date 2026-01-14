using Serilog;

namespace POS.Persistence.Logging
{
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

        public static void LogCritical(string message, Exception? ex = null, object? context = null)
        {
            Log.Fatal(ex, "[CRITICAL] {Message} | Context: {@Context}", message, context);
        }
    }
}
