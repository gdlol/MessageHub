using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Windows;
using Microsoft.Web.WebView2.Wpf;

namespace MessageHub.Windows.WPF;

public class MainWindow : Window
{
    private readonly WebView2? webView;

    public MainWindow(ElementServer.Config elementConfig)
    {
        ArgumentNullException.ThrowIfNull(elementConfig);

        Title = nameof(MessageHub);
        if (elementConfig.ElementListenAddress is not null)
        {
            var clientUri = new Uri($"http://{elementConfig.ElementListenAddress}/#/login");
            var serverEndpoint = IPEndPoint.Parse(elementConfig.ListenAddress);
            Content = webView = new WebView2
            {
                CreationProperties = new CoreWebView2CreationProperties
                {
                    UserDataFolder = Path.Combine(AppContext.BaseDirectory, "Data", nameof(WebView2))
                },
                Source = clientUri
            };
            webView.CoreWebView2InitializationCompleted += (sender, e) =>
            {
                webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
                webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                webView.CoreWebView2.Settings.AreHostObjectsAllowed = false;
                webView.NavigationStarting += (sender, e) =>
                {
                    e.Cancel = true;
                    if (e.Uri == clientUri.ToString()
                        || webView.Source.Equals(clientUri)
                        || (webView.Source.Host == serverEndpoint.Address.ToString()
                            && webView.Source.Port == serverEndpoint.Port))
                    {
                        e.Cancel = false;
                    }
                };
                webView.CoreWebView2.NewWindowRequested += (sender, e) =>
                {
                    e.Handled = true;
                    try
                    {
                        using var _ = Process.Start(new ProcessStartInfo
                        {
                            FileName = e.Uri,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception) { }
                };
            };
        }
    }
}
