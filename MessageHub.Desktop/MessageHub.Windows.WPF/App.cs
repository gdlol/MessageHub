using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MessageHub.Windows.TrayIcon;

namespace MessageHub.Windows.WPF;

public class App : Application
{
    private readonly EventWaitHandle handle;

    public App(EventWaitHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);

        this.handle = handle;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "config.json"));
        var homeServerConfig = JsonSerializer.Deserialize<HomeServer.P2p.Config>(json)!;
        var elementConfig = JsonSerializer.Deserialize<ElementServer.Config>(json)!;

        MainWindow = new MainWindow(elementConfig);

        bool isInitialized = false;
        void showWidow()
        {
            if (!isInitialized)
            {
                isInitialized = true;
                Task.Run(() =>
                {
                    while (true)
                    {
                        handle.WaitOne();
                        Dispatcher.Invoke(showWidow);
                    }
                });
            }
            if (elementConfig.ElementListenAddress is not null)
            {
                MainWindow.Show();
                if (MainWindow.WindowState == WindowState.Minimized)
                {
                    SystemCommands.RestoreWindow(MainWindow);
                }
                MainWindow.Activate();
            }
        }

        void hideWindow(object? sender, CancelEventArgs e)
        {
            e.Cancel = true;
            MainWindow.Hide();
        }
        MainWindow.Closing += hideWindow;

        MessageHubTrayIcon.CreateAndLaunch(
            Dispatcher.Invoke,
            homeServerConfig,
            elementConfig,
            onLaunch: showWidow,
            onExit: Shutdown,
            onError: ex =>
            {
                MessageBox.Show(ex.ToString());
                Environment.Exit(1);
            });
    }
}
