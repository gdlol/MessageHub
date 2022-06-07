using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli.Utils;

namespace WindowsBuild;

public class Program
{
    private static string GetFilePath([CallerFilePath] string? path = null)
    {
        if (path is null)
        {
            throw new InvalidOperationException(nameof(path));
        }
        return path;
    }

    private static void Run(string commandName, params string[] args)
    {
        var command = Command.Create(commandName, args);
        Console.WriteLine($"{commandName} {command.CommandArgs}");
        var result = command.Execute();
        if (result.ExitCode != 0)
        {
            throw new Win32Exception(result.ExitCode);
        }
    }

    [STAThread]
    static void Main()
    {
        string filePath = GetFilePath();
        string projectPath = new FileInfo(filePath).Directory?.Parent?.Parent?.FullName!;
        Console.WriteLine($"Project path: {projectPath}");

        string buildPath = Path.Combine(projectPath, "Build", "Windows");
        Directory.CreateDirectory(buildPath);
        if (Directory.Exists(buildPath))
        {
            Directory.Delete(buildPath, recursive: true);
        }
        string winProjectPath = Path.Combine(projectPath, "MessageHub.Windows");
        using (var iconFile = File.OpenWrite(Path.Combine(winProjectPath, "icon.ico")))
        {
            ApplicationIcon.SaveIcon(iconFile);
        }
        Run("dotnet", "publish",
            Path.Combine(projectPath, "MessageHub.Windows", "MessageHub.Windows.csproj"),
            "--configuration", "Release",
            "--output", Path.Combine(buildPath, "MessageHub"));
        Run("docker", "build",
            "--force-rm",
            "--tag", "messagehub",
            "--file", "Automation/Docker/Windows.Dockerfile",
            projectPath);
        Run("docker", "run",
            "--rm",
            "--volume", $"{buildPath}:/root/build/",
            "messagehub");

        Console.WriteLine("Done.");
    }
}
