namespace Aetheris.Forge.KernelSDK;

/// <summary>
/// Identifies the advanced Forge extension-authoring SDK. Product configurators
/// that only invoke existing Firmament Templates should reference
/// Aetheris.Forge.Host instead.
/// </summary>
public static class KernelSdk
{
    public const string MigrationFrom = "Aetheris.Forge.Sdk";

    /// <summary>Extension-author convenience over the public, independently owned geometry query.</summary>
    public static Aetheris.Geometry.SignedSideResult QuerySignedSide(
        Aetheris.Geometry.BoundedParametricPatch3 patch,
        Aetheris.Geometry.Plane3 plane,
        Aetheris.Geometry.SignedSidePolicy policy) =>
        Aetheris.Geometry.SignedSideQuery.Query(patch, plane, policy);
}
