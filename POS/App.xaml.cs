using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Configuration;
using POS.Logging;
using AppLogger = POS.Logging.AppLogger;
using POS.Persistence.Context;
using POS.Persistence.Models;
using POS.Persistence.Transaction;
using POS.Services.Backup;
using POS.Views;
using Serilog;
using System.Windows;

namespace POS
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {

        public static IServiceProvider ServiceProvider { get; private set; }
        private IBackupService? _backupService;

        public App()
        {
            // Initialize configuration first
            try
            {
                _ = ConfigurationManager.Configuration;
                POS.Logging.LoggerConfiguration.ConfigureLogger();
                AppLogger.LogInfo("=== POS Application Starting ===");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize configuration: {ex.Message}\n\nThe application will now exit.", 
                    "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }

            // Handle unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            DispatcherUnhandledException += OnDispatcherUnhandledException;
        }

        //protected override void OnStartup(StartupEventArgs e)
        //{
        //    base.OnStartup(e);

        //    var services = new ServiceCollection();

        //    // Add services required for authentication
        //    services.AddDbContext<AppDbContext>();
        //    services.AddIdentity<ApplicationUser, IdentityRole>()
        //        .AddEntityFrameworkStores<AppDbContext>()
        //        .AddDefaultTokenProviders();

        //    services.AddLogging();
        //    // Add AuthenticationService
        //    services.AddScoped<AuthenticationService>();

        //    // Build the service provider
        //    ServiceProvider = services.BuildServiceProvider();


        //}

        private void OnStartup(object sender, StartupEventArgs e)
        {
            try
            {
                AppLogger.LogInfo("Initializing services...");

                var services = new ServiceCollection();

                // Get database configuration
                var dbSettings = ConfigurationManager.AppSettings.DatabaseSettings;
                var connectionString = dbSettings.GetConnectionString();

                // Add DbContext with configuration
                services.AddDbContext<AppDbContext>(options =>
                {
                    if (dbSettings.Provider == "SQLite")
                    {
                        options.UseSqlite(connectionString);
                    }
                    else
                    {
                        options.UseSqlServer(connectionString);
                    }

                    #if DEBUG
                    options.EnableSensitiveDataLogging();
                    options.EnableDetailedErrors();
                    #endif
                });

                // Add Identity services
                services.AddIdentity<ApplicationUser, IdentityRole>()
                    .AddEntityFrameworkStores<AppDbContext>()
                    .AddDefaultTokenProviders();

                // Configure Identity options
                var securitySettings = ConfigurationManager.AppSettings.SecuritySettings;
                services.Configure<IdentityOptions>(options =>
                {
                    if (securitySettings.RequireStrongPassword)
                    {
                        options.Password.RequireDigit = true;
                        options.Password.RequireLowercase = true;
                        options.Password.RequireUppercase = true;
                        options.Password.RequireNonAlphanumeric = true;
                        options.Password.RequiredLength = 8;
                        options.Password.RequiredUniqueChars = 4;
                    }
                    else
                    {
                        options.Password.RequireDigit = false;
                        options.Password.RequireLowercase = false;
                        options.Password.RequireUppercase = false;
                        options.Password.RequireNonAlphanumeric = false;
                        options.Password.RequiredLength = 3;
                        options.Password.RequiredUniqueChars = 0;
                    }

                    options.Lockout.MaxFailedAccessAttempts = securitySettings.MaxLoginAttempts;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                });

                services.AddLogging(builder =>
                {
                    builder.AddSerilog();
                });

                // Add authentication services
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                }).AddCookie();

                services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
                services.AddScoped<AuthenticationService>();

                // Add transaction manager
                services.AddScoped<ITransactionManager>(sp => 
                    new TransactionManager(sp.GetRequiredService<AppDbContext>()));

                // Add backup service as singleton
                services.AddSingleton<IBackupService, BackupService>();

                // Build the service provider
                ServiceProvider = services.BuildServiceProvider();

                // Initialize backup service
                _backupService = ServiceProvider.GetRequiredService<IBackupService>();

                AppLogger.LogInfo("Services initialized successfully");

                // Show login window
                var loginView = new LoginView();
                loginView.Show();

                AppLogger.LogInfo("Application started successfully");
            }
            catch (Exception ex)
            {
                AppLogger.LogCritical("Application startup failed", ex);
                MessageBox.Show($"Failed to start application: {ex.Message}\n\nCheck logs for details.", 
                    "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            AppLogger.LogInfo("=== POS Application Shutting Down ===");
            
            (_backupService as BackupService)?.Dispose();
            POS.Logging.LoggerConfiguration.CloseLogger();
            
            base.OnExit(e);
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            AppLogger.LogCritical("Unhandled exception occurred", exception);
            
            MessageBox.Show($"A critical error occurred: {exception?.Message}\n\nThe application will now exit.", 
                "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            AppLogger.LogError("Unhandled UI exception", e.Exception);
            
            MessageBox.Show($"An error occurred: {e.Exception.Message}\n\nPlease check the logs for details.", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            
            e.Handled = true; // Prevent application crash
        }
        //private IServiceProvider _serviceProvider;


        //private void ConfigureServices()
        //{
        //    var services = new ServiceCollection();
        //    services.AddApplicationDependencies()
        //            .AddInfrustructureDependencies()
        //            .AddPersistenceDependencies();
        //    services.AddIdentity<ApplicationUser, IdentityRole>()
        //              .AddEntityFrameworkStores<AppDbContext>()
        //              .AddDefaultTokenProviders();


        //    // Register MainWindow as a service
        //    services.AddSingleton<MainWindow>();

        //    _serviceProvider = services.BuildServiceProvider();
        //}


        //private void OnStartup(object sender, StartupEventArgs e)
        //{


        //    // Register dependencies
        //    ConfigureServices();



        //    // Build the service provider
        //    //UserManager<ApplicationUser> userManager = _serviceProvider.GetService<UserManager<ApplicationUser>>();
        //    //SignInManager<ApplicationUser> signInManager = _serviceProvider.GetService<SignInManager<ApplicationUser>>();
        //    var signInManager = _serviceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
        //    signInManager.Context = new DefaultHttpContext { RequestServices = _serviceProvider };
        //    var loginView = new LoginView(signInManager);
        //    // var loginView = new LoginView(_serviceProvider.GetService<SignInManager<ApplicationUser>>());
        //    loginView.Show();
        //    loginView.IsVisibleChanged += (s, ev) =>
        //    {
        //        if (loginView.IsVisible == false && loginView.IsLoaded)
        //        {
        //            var mainView = _serviceProvider.GetRequiredService<MainWindow>();
        //            mainView.Show();
        //            loginView.Close();
        //        }
        //    };
        //}
    }
}
