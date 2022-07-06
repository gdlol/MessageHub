
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

Environment.SetEnvironmentVariable("DOCKER_BUILDKIT", "1");

bool insideContainer = new EnvironmentProvider().GetEnvironmentVariableAsBool("DOTNET_RUNNING_IN_CONTAINER", false);
if (!insideContainer)
{
    Console.WriteLine("Build and launch container:");

    string filePath = GetFilePath();
    string projectPath = new FileInfo(filePath).Directory?.Parent?.Parent?.FullName!;
    Console.WriteLine($"Project path: {projectPath}");

    string tag = "complement-push-messagehub";
    Run("docker", "build",
        "--force-rm",
        "--tag", tag,
        "--file", "Automation/Docker/Complement.PushImage.Dockerfile",
        projectPath);
    Run("docker", "run",
        "--rm",
        "--env", "DOCKER_HOST=tcp://dind:2375",
        "--network", "complement",
        tag);
}
else
{
    Console.WriteLine("Inside container, build MessageHub.Complement and push to registry:");

    string projectPath = "/root/project";
    string dockerFilePath = Path.Combine(projectPath, "Automation", "Docker", "Complement.Dockerfile");
    string tag = "registry:5000/homeserver";
    string alias = "registry:5000/homeserver:messagehub";
    Run("docker", "build",
        "--force-rm",
        "--tag", tag,
        "--file", dockerFilePath,
        projectPath);
    Run("docker", "image", "tag", tag, alias);
    Run("docker", "image", "push", tag);
    Run("docker", "image", "push", alias);
}
