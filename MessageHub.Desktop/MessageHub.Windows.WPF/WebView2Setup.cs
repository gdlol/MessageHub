using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Web.WebView2.Core;

namespace MessageHub.Windows.WPF;

public static class WebView2Setup
{
    private static bool IsWebView2Installed()
    {
        try
        {
            CoreWebView2Environment.GetAvailableBrowserVersionString();
            return true;
        }
        catch (WebView2RuntimeNotFoundException)
        {
            return false;
        }
    }

    public static string GetInstallerPath() => Path.Combine(AppContext.BaseDirectory, "Data", "WebView2", "setup.exe");

    public static bool EnsureWebView2Installed()
    {
        if (IsWebView2Installed())
        {
            return true;
        }
        using var install = Process.Start(GetInstallerPath());
        install.WaitForExit();
        return IsWebView2Installed();
    }
}
