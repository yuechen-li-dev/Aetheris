using Aetheris.Forge.Host;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Forge.Testing;

public static class ForgeExtensionAssertions
{
    public static void RequireSuccessfulCompilation(ForgeCompilationResult result)
    {
        if (!result.IsSuccess || result.Artifact is null)
            throw new InvalidOperationException("Forge compilation failed: " + string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Code + ":" + diagnostic.Message)));
    }

    public static void RequireValidBrep(BrepBody body)
    {
        var validation = BrepBindingValidator.Validate(body, true);
        if (!validation.IsSuccess)
            throw new InvalidOperationException("BRep validation failed: " + string.Join("; ", validation.Diagnostics.Select(diagnostic => diagnostic.Message)));
    }

    public static void RequireStepRoundTrip(string stepText)
    {
        var imported = Step242Importer.ImportBody(stepText);
        if (!imported.IsSuccess || imported.Value is null)
            throw new InvalidOperationException("STEP round trip failed: " + string.Join("; ", imported.Diagnostics.Select(diagnostic => diagnostic.Message)));
        RequireValidBrep(imported.Value);
    }

    public static void RequireDeterministic(ForgeCompilationResult first, ForgeCompilationResult second)
    {
        RequireSuccessfulCompilation(first);
        RequireSuccessfulCompilation(second);
        if (!string.Equals(first.Artifact!.ArtifactHash, second.Artifact!.ArtifactHash, StringComparison.Ordinal)
            || !string.Equals(first.Artifact.StepText, second.Artifact.StepText, StringComparison.Ordinal))
            throw new InvalidOperationException("Repeated Forge compilation did not preserve artifact identity.");
    }

    public static void RequireCompleteProvenance(ForgeCompilationArtifact artifact)
    {
        string[] stages = ["host", "template", "capability", "construction", "artifact"];
        var missing = stages.Where(stage => artifact.Provenance.All(entry => entry.Stage != stage)).ToArray();
        if (missing.Length > 0) throw new InvalidOperationException("Forge provenance is missing: " + string.Join(", ", missing));
    }

    public static void RequireCirAssociation(ForgeCompilationArtifact artifact)
    {
        if (artifact.Cir is null || !artifact.Cir.Consistency.Passed)
            throw new InvalidOperationException("Forge CIR/BRep association is absent or invalid.");
    }
}
