using System.Collections.Generic;

namespace MessageHub.Windows.TrayIcon.Localization;

public class Strings : Dictionary<string, string>
{
    public const string Logs = "Logs";
    public const string Exit = "Exit";
    public const string RunningInBackground = "{0} is running in the background.";

    public Strings()
    {
        this[Logs] = Logs;
        this[Exit] = Exit;
        this[RunningInBackground] = RunningInBackground;
    }
}
