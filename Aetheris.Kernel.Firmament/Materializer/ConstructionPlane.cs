using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>
/// Immutable material-authoring frame traced from a resolved Concept plane.  It is
/// deliberately independent of BRep faces and is never recomputed from topology.
/// </summary>
public sealed record ConstructionPlane(
    string StableId,
    string SourceConceptId,
    Point3D Origin,
    Direction3D AxisX,
    Direction3D AxisY,
    Direction3D AxisZ,
    string SourceSpan,
    string Provenance)
{
    public const string RightHanded = "RightHanded";
    public string Handedness => RightHanded;
    public double Determinant => AxisX.ToVector().Cross(AxisY.ToVector()).Dot(AxisZ.ToVector());

    public Point3D ToWorld((double X, double Y) point, double localZ = 0d)
        => Origin + AxisX.ToVector() * point.X + AxisY.ToVector() * point.Y + AxisZ.ToVector() * localZ;

    public Vector3D ToWorldVector((double X, double Y) vector)
        => AxisX.ToVector() * vector.X + AxisY.ToVector() * vector.Y;

    public Vector3D ToWorldDirection(Vector3D local)
        => AxisX.ToVector() * local.X + AxisY.ToVector() * local.Y + AxisZ.ToVector() * local.Z;

    public (double X, double Y, double Z) ToLocal(Point3D point)
    {
        var displacement = point - Origin;
        return (displacement.Dot(AxisX.ToVector()), displacement.Dot(AxisY.ToVector()), displacement.Dot(AxisZ.ToVector()));
    }

    public static ConstructionPlane WorldXY { get; } = new(
        "construction:world-xy", "concept:world-xy", new Point3D(0, 0, 0),
        Direction3D.Create(new Vector3D(1, 0, 0)), Direction3D.Create(new Vector3D(0, 1, 0)), Direction3D.Create(new Vector3D(0, 0, 1)),
        "compatibility-default", "DefaultConstructionPlane");

    public static bool TryTrace(string stableId, ConceptIrPlaneValue source, string sourceSpan, out ConstructionPlane? plane, out string? diagnostic)
    {
        plane = null; diagnostic = null;
        if (!double.IsFinite(source.Origin.X) || !double.IsFinite(source.Origin.Y) || !double.IsFinite(source.Origin.Z))
        { diagnostic = "ConstructionPlaneFrameInvalid: origin must be finite"; return false; }
        if (!Direction3D.TryCreate(new Vector3D(source.Normal.X, source.Normal.Y, source.Normal.Z), out var z))
        { diagnostic = "ConceptPlaneNormalDegenerate: normal must be nonzero"; return false; }

        var hintValue = source.OrientationHint is { } hint
            ? new Vector3D(hint.X, hint.Y, hint.Z)
            : DeterministicHint(z.ToVector());
        var projected = hintValue - z.ToVector() * hintValue.Dot(z.ToVector());
        if (!Direction3D.TryCreate(projected, out var y))
        { diagnostic = "ConceptPlaneOrientationDegenerate: orientation hint must not be parallel to normal"; return false; }
        if (!Direction3D.TryCreate(y.ToVector().Cross(z.ToVector()), out var x))
        { diagnostic = "ConstructionPlaneFrameInvalid: unable to derive AxisX"; return false; }
        // Recompute Y so roundoff cannot make a nearly-right-handed frame drift.
        y = Direction3D.Create(z.ToVector().Cross(x.ToVector()));
        var result = new ConstructionPlane(stableId, source.StableId, new(source.Origin.X, source.Origin.Y, source.Origin.Z), x, y, z, sourceSpan, source.Provenance);
        if (Math.Abs(result.Determinant - 1d) > 1e-10)
        { diagnostic = $"ConstructionPlaneNotRightHanded: determinant={result.Determinant:R}"; return false; }
        plane = result;
        return true;
    }

    private static Vector3D DeterministicHint(Vector3D normal)
    {
        // Fixed axis order makes compilation stable without camera-relative terminology.
        var candidates = new[] { new Vector3D(0, 0, 1), new Vector3D(0, 1, 0), new Vector3D(1, 0, 0) };
        return candidates.OrderBy(v => Math.Abs(v.Dot(normal))).First();
    }
}
