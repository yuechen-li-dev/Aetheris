using System.Diagnostics;

namespace Aetheris.CLI;

public sealed record CadmataDiscoveryResult(string Path, string Source);

public static class CadmataDiscovery
{
    public static CadmataDiscoveryResult? Resolve(
        string? explicitPath,
        string cliBaseDirectory,
        Func<string, string?>? environment = null,
        string? pathValue = null)
    {
        environment ??= Environment.GetEnvironmentVariable;

        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            var fullPath = Path.GetFullPath(explicitPath);
            return File.Exists(fullPath)
                ? new CadmataDiscoveryResult(fullPath, "explicit --cadmata-path")
                : null;
        }

        var configured = new[]
        {
            (Value: environment("AETHERIS_CADMATA_PATH"), Source: "AETHERIS_CADMATA_PATH"),
            (Value: environment("AETHERIS_CAD_ASSISTANT_PATH"), Source: "AETHERIS_CAD_ASSISTANT_PATH compatibility setting"),
        };

        foreach (var candidate in configured)
        {
            if (string.IsNullOrWhiteSpace(candidate.Value)) continue;
            var fullPath = Path.GetFullPath(candidate.Value);
            if (File.Exists(fullPath)) return new CadmataDiscoveryResult(fullPath, candidate.Source);
        }

        var packagedCandidates = new[]
        {
            (Path: Path.Combine(cliBaseDirectory, "Cadmata.exe"), Source: "sibling Cadmata executable"),
            (Path: Path.Combine(cliBaseDirectory, "cadmata", "Cadmata.exe"), Source: "package-relative cadmata directory"),
            (Path: Path.Combine(cliBaseDirectory, "tools", "cadmata", "Cadmata.exe"), Source: "package-relative tools/cadmata directory"),
        };

        foreach (var candidate in packagedCandidates)
        {
            if (File.Exists(candidate.Path)) return new CadmataDiscoveryResult(Path.GetFullPath(candidate.Path), candidate.Source);
        }

        pathValue ??= environment("PATH");
        if (!string.IsNullOrWhiteSpace(pathValue))
        {
            foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                foreach (var name in OperatingSystem.IsWindows() ? new[] { "Cadmata.exe", "cadmata.exe" } : new[] { "cadmata", "Cadmata" })
                {
                    var candidate = Path.Combine(directory, name);
                    if (File.Exists(candidate)) return new CadmataDiscoveryResult(Path.GetFullPath(candidate), "PATH");
                }
            }
        }

        return ResolveDevelopmentBuild(cliBaseDirectory)
            ?? ResolveDevelopmentBuild(Directory.GetCurrentDirectory());
    }

    private static CadmataDiscoveryResult? ResolveDevelopmentBuild(string startingPath)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startingPath));
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null) return null;
        foreach (var configuration in new[] { "Release", "Debug" })
        {
            var candidate = Path.Combine(directory.FullName, "Aetheris.Server", "bin", configuration, "net10.0", "Cadmata.exe");
            if (File.Exists(candidate)) return new CadmataDiscoveryResult(candidate, "development build fallback");
        }

        return null;
    }
}

internal interface ICadmataProcessLauncher
{
    Process? Launch(string executablePath, string stepPath);
}

internal sealed class SystemCadmataProcessLauncher : ICadmataProcessLauncher
{
    public Process? Launch(string executablePath, string stepPath)
    {
        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory,
        };
        startInfo.ArgumentList.Add(stepPath);
        var process = Process.Start(startInfo);
        if (process is not null && process.WaitForExit(250))
        {
            throw new InvalidOperationException($"Cadmata exited during startup with code {process.ExitCode}.");
        }

        return process;
    }
}
