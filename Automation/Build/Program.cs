using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;

static string GetFilePath([CallerFilePath] string? path = null)
{
    if (path is null)
    {
        throw new InvalidOperationException(nameof(path));
    }
    return path;
}

static void Run(string commandName, params string[] args)
{
    var command = Command.Create(commandName, args);
    Console.WriteLine($"{commandName} {command.CommandArgs}");
    var result = command.Execute();
    if (result.ExitCode != 0)
    {
        throw new Win32Exception(result.ExitCode);
    }
}

string filePath = GetFilePath();
string projectPath = new FileInfo(filePath).Directory?.Parent?.Parent?.FullName!;
Console.WriteLine($"Project path: {projectPath}");

if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    string buildPath = Path.Combine(projectPath, "Build");
    if (Directory.Exists(buildPath))
    {
        Directory.Delete(buildPath, recursive: true);
    }
    Run("docker", "build",
        "--force-rm",
        "--tag", "messagehub",
        "--file", "Automation/Docker/Windows.Dockerfile",
        projectPath);
    Run("docker", "run",
        "--rm",
        "--volume", $"{buildPath}:/root/build/",
        "messagehub");
}
else
{
    Run("docker", "build",
        "--force-rm",
        "--tag", "messagehub",
        "--file", "Automation/Docker/Dockerfile",
        projectPath);
}

Console.WriteLine("Done.");
