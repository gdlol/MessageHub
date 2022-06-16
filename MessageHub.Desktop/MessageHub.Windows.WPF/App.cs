using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows;
using MessageHub.Windows.TrayIcon;

namespace MessageHub.Windows.WPF;

public class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "config.json"));
        var elementConfig = JsonSerializer.Deserialize<ElementServer.Config>(json)!;

        MainWindow = new MainWindow(elementConfig);

        void showWidow()
        {
            if (elementConfig.ElementListenAddress is not null)
            {
                MainWindow.Show();
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
            elementConfig,
            onLaunch: showWidow,
            onExit: () =>
            {
                MainWindow.Closing -= hideWindow;
                Shutdown();
            },
            onError: ex =>
            {
                MessageBox.Show(ex.ToString());
                Environment.Exit(1);
            });
    }

    public void RunSingleInstance()
    {
        string? executablePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (executablePath is null)
        {
            throw new InvalidOperationException($"{nameof(executablePath)}: {executablePath}");
        }
        using var handle = new EventWaitHandle(
            false,
            EventResetMode.AutoReset,
            Convert.ToHexString(Encoding.UTF8.GetBytes(executablePath)),
            out bool createdNew);
        if (createdNew)
        {
            Run();
        }
    }
}
