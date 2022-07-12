using System.ComponentModel;
using System.Runtime.CompilerServices;
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

Parallel.ForEach(Directory.EnumerateFiles(projectPath, "*.csproj", SearchOption.AllDirectories), csProjectPath =>
{
    Run("dotnet", "restore", csProjectPath);
});
Directory.SetCurrentDirectory(Path.Combine(projectPath, "messagehub-libp2p"));
Run("go", "mod", "download");
