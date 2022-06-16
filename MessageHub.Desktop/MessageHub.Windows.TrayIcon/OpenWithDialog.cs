using System;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace MessageHub.Windows.TrayIcon;

[SupportedOSPlatform("windows7.0")]
public static class OpenWithDialog
{
    private unsafe static int OpenWith(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(nameof(string.IsNullOrWhiteSpace), nameof(filePath));
        }

        fixed (char* pFilePath = filePath)
        {
            var openAsInfo = new OPENASINFO
            {
                pcszFile = new PCWSTR(pFilePath),
                oaifInFlags = OPEN_AS_INFO_FLAGS.OAIF_EXEC
            };
            var result = PInvoke.SHOpenWithDialog(default, openAsInfo);
            return result;
        }
    }

    public static void Show(string filePath)
    {
        OpenWith(filePath);
    }
}
