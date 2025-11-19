using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Windows;
using UnityPerfProfilerWPF.Services;
using UnityPerfProfilerWPF.ViewModels;

namespace UnityPerfProfilerWPF;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        // 配置Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File("Logs/upr-.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            // 创建主机
            _host = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices(ConfigureServices)
                .Build();

            await _host.StartAsync();

            // 获取主窗口
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "应用程序启动失败");
            MessageBox.Show($"应用程序启动失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // 注册数据存储服务
        services.AddSingleton<IDataStorageService, DataStorageService>();
        services.AddSingleton<IRenderDocService, RenderDocService>();
        
        // 注册核心Unity服务
        services.AddSingleton<UnityProfilerService>();
        services.AddSingleton<DirectUnityConnection>();
        
        // 注册服务
        services.AddSingleton<IConnectionService, UnityConnectionService>();
        services.AddSingleton<IPerformanceDataService, PerformanceDataService>();
        
        // 注册ViewModels
        services.AddTransient<MainViewModel>();
        
        // 注册窗口
        services.AddTransient<MainWindow>();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_host != null)
            {
                await _host.StopAsync(TimeSpan.FromSeconds(5));
                _host.Dispose();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "应用程序关闭时出错");
        }
        finally
        {
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}

