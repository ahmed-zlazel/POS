using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using System.IO;

namespace POS.Configuration
{
    public static class ConfigurationManager
    {
        private static IConfiguration? _configuration;
        private static AppSettings? _appSettings;

        public static IConfiguration Configuration
        {
            get
            {
                if (_configuration == null)
                {
                    Initialize();
                }
                return _configuration!;
            }
        }

        public static AppSettings AppSettings
        {
            get
            {
                if (_appSettings == null)
                {
                    Initialize();
                }
                return _appSettings!;
            }
        }

        private static void Initialize()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
                .AddEnvironmentVariables();

            _configuration = builder.Build();

            _appSettings = new AppSettings();
            _configuration.Bind(_appSettings);
        }

        public static void Reload()
        {
            _configuration = null;
            _appSettings = null;
            Initialize();
        }
    }
}
