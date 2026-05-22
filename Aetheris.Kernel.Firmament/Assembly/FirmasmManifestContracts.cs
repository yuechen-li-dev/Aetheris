using Aetheris.Kernel.Core.Brep;

namespace Aetheris.Kernel.Firmament.Assembly;

public sealed record FirmasmManifest(
    string ManifestVersion,
    FirmasmAssemblyMetadata Assembly,
    IReadOnlyDictionary<string, FirmasmPartDefinition> Parts,
    IReadOnlyList<FirmasmInstanceDefinition> Instances);

public sealed record FirmasmAssemblyMetadata(string Name, string Units);

public sealed record FirmasmPartDefinition(FirmasmPartKind Kind, string Source);

public enum FirmasmPartKind
{
    Firmament,
    Step,
}

public sealed record FirmasmInstanceDefinition(
    string Id,
    string Part,
    FirmasmRigidTransform Transform);

public sealed record FirmasmRigidTransform(
    IReadOnlyList<double> Translate,
    IReadOnlyList<double>? RotateDegXyz);

public sealed record FirmasmLoadedAssembly(
    string SourcePath,
    FirmasmManifest Manifest,
    IReadOnlyDictionary<string, FirmasmLoadedPartSource> LoadedParts);

public abstract record FirmasmLoadedPartSource(string SourcePath, string ResolvedPath);

public sealed record FirmasmLoadedNativeFirmamentPart(
    string SourcePath,
    string ResolvedPath)
    : FirmasmLoadedPartSource(SourcePath, ResolvedPath);

public sealed record FirmasmLoadedOpaqueStepPart(
    string SourcePath,
    string ResolvedPath,
    BrepBody ImportedBody)
    : FirmasmLoadedPartSource(SourcePath, ResolvedPath);
