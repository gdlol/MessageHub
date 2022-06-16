using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using MessageHub.Windows.TrayIcon;

namespace MessageHub.Windows;

public class App : ApplicationContext
{
    private static void LaunchUrl(string? url)
    {
        if (url is null)
        {
            return;
        }
        using var _ = Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    public App(BackgroundWorker worker)
    {
        ArgumentNullException.ThrowIfNull(worker);

        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "config.json"));
        var elementConfig = JsonSerializer.Deserialize<ElementServer.Config>(json)!;
        string? clientUrl = null;
        if (elementConfig.ElementListenAddress is not null)
        {
            clientUrl = $"http://{elementConfig.ElementListenAddress}/#/login";
        }

        worker.ProgressChanged += (sender, e) =>
        {
            var callback = (Action)e.UserState!;
            callback();
        };

        MessageHubTrayIcon.CreateAndLaunch(
            callback => worker.ReportProgress(0, callback),
            elementConfig,
            onLaunch: () => LaunchUrl(clientUrl),
            onExit: Application.Exit,
            onError: ex =>
            {
                MessageBox.Show(ex.ToString());
                Environment.Exit(1);
            });
    }
}
