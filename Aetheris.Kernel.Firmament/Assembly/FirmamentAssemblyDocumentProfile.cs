using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Firmament.Assembly;

public enum FirmamentDocumentProfile
{
    General,
    Assembly
}

public sealed record FirmamentAssemblyDocumentResult(
    FirmamentDocumentProfile Profile,
    string SourcePath,
    string EffectiveFirmamentSource,
    bool MigratedLegacyJson,
    AssemblyM1CompilationResult Compilation)
{
    public bool IsSuccess => Compilation.IsSuccess;
}

/// <summary>
/// Selects document-profile validation by extension, while always sending current
/// source through the ordinary Firmament assembly parser/compiler. JSON is admitted
/// only as an explicitly named legacy migration input.
/// </summary>
public sealed class FirmamentAssemblyDocumentCompiler
{
    public FirmamentAssemblyDocumentResult CompileFile(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var source = File.ReadAllText(fullPath);
        var profile = string.Equals(Path.GetExtension(fullPath), ".firmasm", StringComparison.OrdinalIgnoreCase)
            ? FirmamentDocumentProfile.Assembly
            : FirmamentDocumentProfile.General;
        var migrated = false;
        if (profile == FirmamentDocumentProfile.Assembly && LooksLikeJson(source))
        {
            var legacy = new FirmasmManifestLoader().LoadFromFile(fullPath);
            if (!legacy.IsSuccess)
            {
                var diagnostics = legacy.Diagnostics.Select(diagnostic => new AssemblyDiagnostic(
                    "assembly-profile-legacy-json-invalid", diagnostic.Message)).ToArray();
                return new(profile, fullPath, source, true, new(null, null, diagnostics));
            }
            source = LegacyFirmasmMigration.GenerateCurrentSource(legacy.Value);
            migrated = true;
        }

        var profileDiagnostics = ValidateProfile(source, profile);
        if (profileDiagnostics.Any(diagnostic => diagnostic.Severity == AssemblyDiagnosticSeverity.Error))
            return new(profile, fullPath, source, migrated, new(null, null, profileDiagnostics));

        var temporaryPath = fullPath;
        string? stagedPath = null;
        if (migrated)
        {
            stagedPath = Path.Combine(Path.GetDirectoryName(fullPath)!, $".{Path.GetFileNameWithoutExtension(fullPath)}.migrated-{Guid.NewGuid():N}.firmasm");
            File.WriteAllText(stagedPath, source, new UTF8Encoding(false));
            temporaryPath = stagedPath;
        }
        try
        {
            var compilation = new AssemblyM1Pipeline().CompileFile(temporaryPath);
            return new(profile, fullPath, source, migrated, compilation with
            {
                Diagnostics = [.. profileDiagnostics, .. compilation.Diagnostics]
            });
        }
        finally
        {
            if (stagedPath is not null && File.Exists(stagedPath)) File.Delete(stagedPath);
        }
    }

    public static IReadOnlyList<AssemblyDiagnostic> ValidateProfile(string source, FirmamentDocumentProfile profile)
    {
        if (profile == FirmamentDocumentProfile.General) return [];
        var assemblies = Regex.Matches(source, @"(?m)^\s*Assembly\s+[A-Za-z_]\w*\s*\{", RegexOptions.CultureInvariant).Count;
        // A Template-produced Assembly is a reusable definition, not an exported
        // product root.  It intentionally shares the existing Template keyword.
        var templateAssemblies = Regex.Matches(source, @"\bTemplate\s*<[^>]+>\s*Assembly\s+[A-Za-z_]\w*\s*\{", RegexOptions.CultureInvariant).Count;
        var roots = assemblies - templateAssemblies;
        return roots switch
        {
            0 => [new("assembly-profile-no-root", ".firmasm requires exactly one exported/root Assembly product; none was found.")],
            > 1 => [new("assembly-profile-multiple-roots", $".firmasm requires exactly one exported/root Assembly product; found {roots}.")],
            _ => []
        };
    }

    private static bool LooksLikeJson(string source) => source.AsSpan().TrimStart().StartsWith("{".AsSpan(), StringComparison.Ordinal);
}

public static class LegacyFirmasmMigration
{
    public static string GenerateCurrentSource(FirmasmLoadedAssembly legacy)
    {
        var rootName = Identifier(legacy.Manifest.Assembly.Name);
        var builder = new StringBuilder();
        builder.AppendLine("// Migrated from legacy JSON-shaped .firmasm syntax.");
        builder.AppendLine("// The .firmasm extension is the current Firmament V2 Assembly document profile.");
        builder.AppendLine($"Assembly {rootName} {{");
        builder.AppendLine($"  <Assembly {rootName}>");
        foreach (var instance in legacy.Manifest.Instances.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            var part = legacy.Manifest.Parts[instance.Part];
            var definition = part.Kind == FirmasmPartKind.Step
                ? $"ExternalStep<\"{part.Source.Replace('\\', '/')}\">"
                : instance.Part;
            var transform = FirmasmAssemblyExecutor.BuildRigidTransform(instance.Transform).ToRowMajor();
            builder.AppendLine($"    <Part {Identifier(instance.Id)} = {definition}>");
            builder.AppendLine("      // Placement authority: compatibility evidence from legacy JSON; no Mate is inferred.");
            builder.AppendLine($"      Placement LegacyExplicit = [{string.Join(", ", transform.Select(Number))}];");
            builder.AppendLine("    </Part>");
        }
        builder.AppendLine("  </Assembly>");
        builder.AppendLine($"  Anchor: {rootName};");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string Identifier(string value)
    {
        var sanitized = Regex.Replace(value, "[^A-Za-z0-9_]", "_");
        if (sanitized.Length == 0) return "Imported";
        return char.IsLetter(sanitized[0]) || sanitized[0] == '_' ? sanitized : "_" + sanitized;
    }

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
