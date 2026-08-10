namespace Aetheris.Server.Startup;

public sealed class CadmataLaunchException(string message) : Exception(message);

public sealed record CadmataStartupStepContent(string Path, string FileName, string StepText, string Kind = "step");

public sealed record CadmataLaunchOptions(CadmataStartupStepContent? Step, bool NoBrowser, bool HasExplicitUrls)
{
    public void ValidateProductionAssets(string applicationDirectory)
    {
        if (Step is null)
        {
            return;
        }

        var indexPath = Path.Combine(Path.GetFullPath(applicationDirectory), "wwwroot", "index.html");
        if (!File.Exists(indexPath))
        {
            throw new CadmataLaunchException(
                $"Production frontend assets were not found beside Cadmata: {indexPath}. " +
                "Reinstall the Cadmata package or build aetheris.client before building the host.");
        }
    }

    public static CadmataLaunchOptions Parse(string[] args)
    {
        string? stepPath = null;
        var noBrowser = false;
        var hasExplicitUrls = false;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--no-browser", StringComparison.Ordinal))
            {
                noBrowser = true;
                continue;
            }

            if (string.Equals(argument, "--urls", StringComparison.OrdinalIgnoreCase))
            {
                hasExplicitUrls = true;
                index++;
                continue;
            }

            if (argument.StartsWith("--urls=", StringComparison.OrdinalIgnoreCase))
            {
                hasExplicitUrls = true;
                continue;
            }

            if (argument.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            if (stepPath is not null)
            {
                throw new CadmataLaunchException("Only one STEP path may be supplied.");
            }

            stepPath = argument;
        }

        if (stepPath is null)
        {
            return new CadmataLaunchOptions(null, noBrowser, hasExplicitUrls);
        }

        var fullPath = Path.GetFullPath(stepPath);
        var extension = Path.GetExtension(fullPath);
        var isAssembly = string.Equals(extension, ".firmasm", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".firmament", StringComparison.OrdinalIgnoreCase);
        if (!string.Equals(extension, ".step", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".stp", StringComparison.OrdinalIgnoreCase)
            && !isAssembly)
        {
            throw new CadmataLaunchException($"Startup file must use .step, .stp, .firmament, or .firmasm: {fullPath}");
        }

        if (!File.Exists(fullPath))
        {
            throw new CadmataLaunchException($"STEP file was not found: {fullPath}");
        }

        string stepText;
        try
        {
            stepText = File.ReadAllText(fullPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new CadmataLaunchException($"STEP file could not be read: {exception.Message}");
        }

        if (string.IsNullOrWhiteSpace(stepText))
        {
            throw new CadmataLaunchException($"STEP file is empty: {fullPath}");
        }

        return new CadmataLaunchOptions(
            new CadmataStartupStepContent(fullPath, Path.GetFileName(fullPath), stepText, isAssembly ? "assembly" : "step"),
            noBrowser,
            hasExplicitUrls);
    }
}
