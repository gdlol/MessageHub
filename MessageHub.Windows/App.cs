using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MessageHub.Windows.Localization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace MessageHub.Windows;

public class App : Application
{
    private static string CreateLogFile(string logsPath, DateTimeOffset dateTime)
    {
        string logFilePath = Path.Combine(logsPath, $"{dateTime:yyyy-MM-dd-HH-mm-ss-fff}.log");
        using var file = File.Create(logFilePath);
        return logFilePath;
    }

    private static void LaunchUrl(string url)
    {
        using var _ = Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
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
        }
        return (new StringLocalizer(new Strings()), error);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string? executablePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (executablePath is null)
        {
            throw new InvalidOperationException(nameof(executablePath));
        }
        string applicationPath = new FileInfo(executablePath).Directory!.FullName;
        string json = File.ReadAllText(Path.Combine(applicationPath, "config.json"));
        var config = JsonSerializer.Deserialize<Config>(json)!;
        string clientUrl = "http://127.84.48.1:80";
        if (config.ClientListenAddress is not null)
        {
            clientUrl = $"http://{config.ClientListenAddress}";
        }

        string logsPath = Path.Combine(applicationPath, "Logs");
        Directory.CreateDirectory(logsPath);
        string logFilePath = CreateLogFile(logsPath, DateTimeOffset.Now);

        string resourcePath = Path.Combine(applicationPath, "Resources");
        var (localizer, error) = LoadLocalizer(CultureInfo.CurrentUICulture, resourcePath);
        if (error is not null)
        {
            File.AppendAllLines(logFilePath, new[] { error });
        }

        System.Windows.Forms.NotifyIcon notifyIcon;
        System.Windows.Forms.ContextMenuStrip contextMenu;
        System.Windows.Forms.ToolStripMenuItem elementItem;
        System.Windows.Forms.ToolStripMenuItem logItem;
        System.Windows.Forms.ToolStripMenuItem exitItem;

        notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(executablePath),
            Text = nameof(MessageHub),
            Visible = true,
            ContextMenuStrip = contextMenu = new System.Windows.Forms.ContextMenuStrip()
        };
        Exit += (sender, e) => notifyIcon.Dispose();
        contextMenu.Items.Add(elementItem = new System.Windows.Forms.ToolStripMenuItem("Element"));
        contextMenu.Items.Add(logItem = new System.Windows.Forms.ToolStripMenuItem(localizer[Strings.Logs]));
        contextMenu.Items.Add(exitItem = new System.Windows.Forms.ToolStripMenuItem(localizer[Strings.Exit]));
        elementItem.Click += (sender, e) => LaunchUrl(clientUrl);
        logItem.Click += (sender, e) => OpenWithDialog.Show(logFilePath);
        exitItem.Click += (sender, e) =>
        {
            notifyIcon.Dispose();
            Shutdown();
        };

        Task.Run(async () =>
        {
            using var logFile = new FileStream(logFilePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            using var logWriter = new StreamWriter(logFile)
            {
                AutoFlush = true
            };
            Console.SetOut(logWriter);

            var clientReady = new TaskCompletionSource();
            var serverReady = new TaskCompletionSource();

            var client = Task.Run(async () =>
            {
                string elementPath = Path.Combine(applicationPath, "Data", "Element");
                if (!Directory.Exists(elementPath))
                {
                    return;
                }
                var builder = WebApplication.CreateBuilder(new WebApplicationOptions
                {
                    ContentRootPath = applicationPath,
                    WebRootPath = elementPath
                });
                builder.WebHost.UseUrls(clientUrl);
                builder.WebHost.ConfigureLogging(builder => builder.ClearProviders());
                var app = builder.Build();
                app.Use(async (context, next) =>
                {
                    context.Response.Headers.Add("X-Frame-Options", "SAMEORIGIN");
                    context.Response.Headers.Add("Content-Security-Policy", "frame-ancestors 'none'");
                    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block;");
                    context.Response.Headers.Add("Cache-Control", "no-cache");
                    await next();
                });
                app.UseDefaultFiles();
                app.UseStaticFiles();
                var task = app.RunAsync();
                clientReady.SetResult();
                await task;
            });

            var server = Task.Run(async () =>
            {
                var task = MessageHub.Program.RunHomeServer(applicationPath);
                serverReady.SetResult();
                await task;
            });

            try
            {
                await Task.WhenAll(clientReady.Task, serverReady.Task);
                if (config.LaunchClientOnStart)
                {
                    LaunchUrl(clientUrl);
                }
                await Dispatcher.InvokeAsync(() =>
                {
                    notifyIcon.ShowBalloonTip(
                        3000,
                        nameof(MessageHub),
                        localizer[Strings.RunningInBackground, nameof(MessageHub)],
                        System.Windows.Forms.ToolTipIcon.None);
                });
                await Task.WhenAll(client, server);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                Environment.Exit(1);
            }
        });
    }

    public void RunSingleInstance()
    {
        string? assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
        if (assemblyName is null)
        {
            throw new InvalidOperationException(nameof(assemblyName));
        }
        using var handle = new EventWaitHandle(
            false,
            EventResetMode.AutoReset,
            assemblyName,
            out bool createdNew);
        if (createdNew)
        {
            Run();
        }
    }
}
