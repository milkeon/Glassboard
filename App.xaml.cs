using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Glassboard;

public partial class App : Application
{
    public App()
    {
        LogStartup("App ctor");
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        LogStartup("OnStartup begin");

        try
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnMainWindowClose;

            LogStartup("Before MainWindow ctor");
            var window = new MainWindow();
            LogStartup("After MainWindow ctor");

            MainWindow = window;
            LogStartup("Before window.Show");
            window.Show();
            LogStartup("After window.Show");
        }
        catch (Exception exception)
        {
            LogCrash(exception);
            MessageBox.Show($"Glassboard 시작 중 오류가 발생했습니다.\n\n{exception}", "Glassboard", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash(e.Exception);
        MessageBox.Show($"Glassboard 시작 중 오류가 발생했습니다.\n\n{e.Exception}", "Glassboard", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
            LogCrash(exception);
    }

    private static void LogStartup(string message)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glassboard");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "startup.log");
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
        catch
        {
        }
    }

    private static void LogCrash(Exception exception)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glassboard");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "crash.log");
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}\n\n");
        }
        catch
        {
            // 로그 기록 실패는 무시합니다.
        }
    }
}
