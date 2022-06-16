using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace MessageHub.Windows;

public class Program
{
    [STAThread]
    static void Main()
    {
        try
        {
            using var worker = new BackgroundWorker
            {
                WorkerReportsProgress = true
            };
            using var app = new App(worker);
            string executablePath = Process.GetCurrentProcess().MainModule?.FileName!;
            using var handle = new EventWaitHandle(
                false,
                EventResetMode.AutoReset,
                Convert.ToHexString(Encoding.UTF8.GetBytes(executablePath)),
                out bool createdNew);
            if (createdNew)
            {
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
