using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Windows;

namespace MessageHub.Windows.WPF;

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
            if (dllName == "vcruntime140.dll")
            {
                dllName = "vcruntime140_cor3.dll";
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
                var app = new App(handle);
                app.Run();
            }
            else
            {
                handle.Set();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString());
            Environment.Exit(1);
        }
    }
}
