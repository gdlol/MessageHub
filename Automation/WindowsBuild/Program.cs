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
        string outputPath = Path.Combine(buildPath, "MessageHub");
        string wpfOutputPath = Path.Combine(buildPath, "MessageHub (WebView2)");
        Directory.CreateDirectory(buildPath);
        if (Directory.Exists(buildPath))
        {
            Directory.Delete(buildPath, recursive: true);
        }
        string desktopProjectsPath = Path.Combine(projectPath, "MessageHub.Desktop");
        string winProjectPath = Path.Combine(desktopProjectsPath, "MessageHub.Windows");
        string wpfProjectPath = Path.Combine(desktopProjectsPath, "MessageHub.Windows.WPF");
        using (var iconFile = File.OpenWrite(Path.Combine(winProjectPath, "Resources", "icon.ico")))
        using (var wpfIconFile = File.OpenWrite(Path.Combine(wpfProjectPath, "Resources", "icon.ico")))
        {
            ApplicationIcon.SaveIcon(iconFile);
            ApplicationIcon.SaveIcon(wpfIconFile);
        }
        Run("dotnet", "publish",
            Path.Combine(winProjectPath, "MessageHub.Windows.csproj"),
            "--configuration", "Release",
            "-property:OutputLibrary=true",
            "--output", outputPath);
        Run("dotnet", "publish",
            Path.Combine(wpfProjectPath, "MessageHub.Windows.WPF.csproj"),
            "--configuration", "Release",
            "-property:OutputLibrary=true",
            "--output", wpfOutputPath);
        Run("docker", "build",
            "--force-rm",
            "--tag", "messagehub-windows",
            "--file", "Automation/Docker/Windows.Dockerfile",
            projectPath);
        Run("docker", "run",
            "--rm",
            "--volume", $"{outputPath}:/root/build/",
            "messagehub-windows");
        Run("docker", "run",
            "--rm",
            "--volume", $"{wpfOutputPath}:/root/build/",
            "messagehub-windows");

        void MoveDllFiles(string baseDirectory)
        {
            string dllPath = Path.Combine(baseDirectory, "runtimes", "win-x64", "native");
            Directory.CreateDirectory(dllPath);
            foreach (var file in new DirectoryInfo(baseDirectory).EnumerateFiles())
            {
                if (file.Extension == ".dll")
                {
                    string sourcePath = file.FullName;
                    string targetPath = Path.Combine(dllPath, file.Name);
                    Console.WriteLine($"Move {sourcePath} -> {targetPath}");
                    file.MoveTo(targetPath, overwrite: true);
                }
            }
        }
        MoveDllFiles(outputPath);
        MoveDllFiles(wpfOutputPath);

        Console.WriteLine("Done.");
    }
}
