using System;
using System.Windows;

namespace MessageHub.Windows.WPF;

public class Program
{
    [STAThread]
    static void Main()
    {
        try
        {
            var app = new App();
            app.RunSingleInstance();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString());
            Environment.Exit(1);
        }
    }
}
