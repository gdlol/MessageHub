using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MessageHub.Windows.TrayIcon.Localization;

namespace MessageHub.Windows.TrayIcon;

public static class MessageHubTrayIcon
{
    private static string CreateLogFile(string logsPath, DateTimeOffset dateTime)
    {
        string logFilePath = Path.Combine(logsPath, $"{dateTime:yyyy-MM-dd-HH-mm-ss-fff}.log");
        using var file = File.Create(logFilePath);
        return logFilePath;
    }

    private static (StringLocalizer localizer, string? error) LoadLocalizer(CultureInfo culture, string resourcePath)
    {
        string? error = null;
        while (!culture.Equals(CultureInfo.InvariantCulture))
        {
            string filePath = Path.Combine(resourcePath, $"{culture.Name}.json");
            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    var mapping = JsonSerializer.Deserialize<ImmutableDictionary<string, string>>(json);
                    if (mapping is null)
                    {
                        break;
                    }
                    return (new StringLocalizer(mapping), error);
                }
                catch (Exception ex)
                {
                    error = $"Error loading string resource {filePath}: {ex}";
                }
                break;
            }
            culture = culture.Parent;
        }
        return (new StringLocalizer(new Strings()), error);
    }

    public static void CreateAndLaunch(
        Action<Action> dispatcher,
        HomeServer.P2p.Config homeServerConfig,
        ElementServer.Config elementConfig,
        Action onLaunch,
        Action onExit,
        Action<Exception> onError)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(elementConfig);
        ArgumentNullException.ThrowIfNull(onLaunch);
        ArgumentNullException.ThrowIfNull(onExit);
        ArgumentNullException.ThrowIfNull(onError);

        dispatcher.Invoke(async () =>
        {
            try
            {
                string executablePath = Process.GetCurrentProcess().MainModule?.FileName!;
                string applicationPath = new FileInfo(executablePath).Directory!.FullName;

                string logsPath = Path.Combine(applicationPath, "Logs");
                Directory.CreateDirectory(logsPath);
                string logFilePath = CreateLogFile(logsPath, DateTimeOffset.Now);

                string resourcePath = Path.Combine(applicationPath, "Resources", "Localization");
                var (localizer, error) = LoadLocalizer(CultureInfo.CurrentUICulture, resourcePath);
                if (error is not null)
                {
                    File.AppendAllLines(logFilePath, new[] { error });
                }

                using var cts = new CancellationTokenSource();
                using var icon = Icon.ExtractAssociatedIcon(executablePath);
                using var contextMenu = new ContextMenuStrip();
                using var logItem = new ToolStripMenuItem(localizer[Strings.Logs]);
                using var exitItem = new ToolStripMenuItem(localizer[Strings.Exit]);
                using var notifyIcon = new NotifyIcon
                {
                    Icon = icon,
                    Text = nameof(MessageHub),
                    Visible = true,
                    ContextMenuStrip = contextMenu
                };
                logItem.Click += (sender, e) => OpenWithDialog.Show(logFilePath);
                exitItem.Click += (sender, e) => cts.Cancel();
                contextMenu.Items.Add(logItem);
                contextMenu.Items.Add(exitItem);

                using var logFile = new FileStream(logFilePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
                using var logWriter = new StreamWriter(logFile)
                {
                    AutoFlush = true
                };
                Console.SetOut(logWriter);

                _ = Task.Run(async () =>
                {
                    var server = Program.RunAsync(applicationPath, homeServerConfig, cts.Token);

                    var client = Task.CompletedTask;
                    if (elementConfig.ElementListenAddress is not null)
                    {
                        client = ElementServer.Program.RunAsync(applicationPath, elementConfig, cts.Token);
                    }

                    dispatcher.Invoke(() =>
                    {
                        notifyIcon.DoubleClick += (sender, e) => onLaunch();
                        onLaunch();
                        notifyIcon.ShowBalloonTip(
                            3000,
                            nameof(MessageHub),
                            localizer[Strings.RunningInBackground, nameof(MessageHub)],
                            ToolTipIcon.None);
                    });

                    try
                    {
                        await Task.WhenAny(client, server);
                        cts.Cancel();
                        await Task.WhenAll(client, server);
                    }
                    catch (Exception ex)
                    {
                        onError(ex);
                    }
                });

                var tcs = new TaskCompletionSource();
                using var exit = cts.Token.Register(tcs.SetResult);
                await tcs.Task;
            }
            catch (Exception ex)
            {
                onError(ex);
            }
            finally
            {
                dispatcher.Invoke(onExit);
            }
        });
    }
}
