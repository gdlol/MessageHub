using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace MessageHub.Windows;

public class Program
{
    private static void ResolveUnmanagedDlls()
    {
        string dllPath = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native");
        AssemblyLoadContext.Default.ResolvingUnmanagedDll += (_, dllName) =>
        {
            if (!dllName.EndsWith(".dll"))
            {
                dllName += ".dll";
            }
            string path = Path.Combine(dllPath, dllName);
            if (File.Exists(path) && NativeLibrary.TryLoad(path, out var handle))
            {
                return handle;
            }
            else
            {
                return IntPtr.Zero;
            }
        };
    }

    [STAThread]
    static void Main()
    {
        ResolveUnmanagedDlls();
        try
        {
            string executablePath = Process.GetCurrentProcess().MainModule?.FileName!;
            using var handle = new EventWaitHandle(
                false,
                EventResetMode.AutoReset,
                Convert.ToHexString(Encoding.UTF8.GetBytes(executablePath)),
                out bool createdNew);
            if (createdNew)
            {
                using var worker = new BackgroundWorker
                {
                    WorkerReportsProgress = true
                };
                using var app = new App(worker);
                Application.Run(app);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString());
            Environment.Exit(1);
        }
    }
}
