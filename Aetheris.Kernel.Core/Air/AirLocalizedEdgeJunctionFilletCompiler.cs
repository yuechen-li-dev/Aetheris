using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Air;

/// <summary>
/// Bounded geometric investigation for two adjacent constant-radius fillet intents.
/// This is deliberately a Construction-AIR admission stage: it never emits topology
/// unless a patch closes against the selected replacements and retained supports.
/// </summary>
internal sealed record AirLocalizedEdgeJunctionFilletCompileRequest(
    string BodyId, string FeatureId, string FeatureName, double Width, double Depth, double Height,
    string FaceA, string TargetA, string FaceB, string TargetB, double RadiusA, double RadiusB,
    AirSourceSpan SourceSpan, bool HistoryKnown = true);

internal enum FilletJunctionErrorKind
{
    EdgesDoNotShareEndpoint,
    UnsupportedJunctionValence,
    RadiusMismatch,
    RadiusTooLarge,
    UnsupportedSurfaceCombination,
    CornerPatchSurfaceRequired,
    CornerPatchConstructionFailed,
    ConstructionWitnessRequired,
}

internal sealed record FilletJunctionError(
    FilletJunctionErrorKind Kind, string Code, string Message, string Stage,
    IReadOnlyList<string> Evidence);

/// <summary>
/// Exact witness for the only simple analytic candidate.  The third boundary is
/// retained as evidence because it proves why this candidate cannot be emitted as
/// a two-edge junction.
/// </summary>
internal sealed record SphericalFilletCornerCandidateWitness(
    Point3D Center,
    double Radius,
    SphereSurface Surface,
    CylinderSurface ReplacementACylinder,
    CylinderSurface ReplacementBCylinder,
    Circle3Curve ThirdBoundary,
    ParameterInterval ThirdBoundaryTrim,
    Point3D SupportTangentOnPlusX,
    Point3D SupportTangentOnPlusY,
    Point3D SupportTangentOnPlusZ,
    string RequiredAdjacentSurface,
    double CylinderTangencyDeviation,
    double ThirdBoundaryLength);

internal sealed record LocalizedFilletJunctionConstruction(
    string ConstructionId,
    AirEdgeFinishFeature ReplacementA,
    AirEdgeFinishFeature ReplacementB,
    Point3D SharedEndpoint,
    SphericalFilletCornerCandidateWitness Candidate,
    string MaterialSide,
    string Provenance);

internal sealed record AirLocalizedEdgeJunctionFilletCompileResult(
    bool Succeeded,
    LocalizedFilletJunctionConstruction? Construction,
    FilletJunctionError? Error,
    IReadOnlyList<string> Diagnostics);

internal static class AirLocalizedEdgeJunctionFilletCompiler
{
    private const double Tol = 1e-9;

    public static AirLocalizedEdgeJunctionFilletCompileResult Compile(AirLocalizedEdgeJunctionFilletCompileRequest input)
    {
        FilletJunctionError Fail(FilletJunctionErrorKind kind, string code, string message, params string[] evidence) =>
            new(kind, code, message, "FeatureAIR->ConstructionAIR", evidence);

        if (!input.HistoryKnown)
            return Rejected(Fail(FilletJunctionErrorKind.UnsupportedSurfaceCombination, "localized-fillet-junction-unsupported-history", "Localized fillet junctions require history-known axis-aligned box support planes.", "construction-history=imported/no-history"));
        if (!FinitePositive(input.Width) || !FinitePositive(input.Depth) || !FinitePositive(input.Height))
            return Rejected(Fail(FilletJunctionErrorKind.UnsupportedSurfaceCombination, "localized-fillet-junction-invalid-box-dimensions", "Box dimensions must be finite and positive."));
        if (!Matches(input.FaceA, input.TargetA, "+X") || !Matches(input.FaceB, input.TargetB, "+Y"))
            return Rejected(Fail(FilletJunctionErrorKind.EdgesDoNotShareEndpoint, "localized-fillet-junction-edges-do-not-share-canonical-endpoint", "M4 investigates SharedEdge(+X,+Z) and SharedEdge(+Y,+Z) only."));
        if (!FinitePositive(input.RadiusA) || !FinitePositive(input.RadiusB))
            return Rejected(Fail(FilletJunctionErrorKind.RadiusTooLarge, "localized-fillet-junction-radius-must-be-positive", "Both fillet radii must be finite and positive."));
        if (double.Abs(input.RadiusA - input.RadiusB) > Tol)
            return Rejected(Fail(FilletJunctionErrorKind.RadiusMismatch, "localized-fillet-junction-radius-mismatch", "The canonical candidate requires equal radii."));
        if (input.RadiusA >= input.Width - Tol || input.RadiusA >= input.Depth - Tol || input.RadiusA >= input.Height - Tol)
            return Rejected(Fail(FilletJunctionErrorKind.RadiusTooLarge, "localized-fillet-junction-radius-too-large", "The radius must remain within all three incident support extents."));

        var r = input.RadiusA;
        var hx = input.Width / 2d;
        var hy = input.Depth / 2d;
        var hz = input.Height / 2d;
        var center = new Point3D(hx - r, hy - r, hz - r);
        var x = Direction3D.Create(new Vector3D(1, 0, 0));
        var y = Direction3D.Create(new Vector3D(0, 1, 0));
        var z = Direction3D.Create(new Vector3D(0, 0, 1));
        var replacementA = new CylinderSurface(center, y, r, x);
        var replacementB = new CylinderSurface(center, x, r, y);
        var sphere = new SphereSurface(center, z, r, x);
        // This circle is the forced third spherical boundary.  It is exactly the
        // radius-R cylinder about the +Z axis, i.e. a fillet of SharedEdge(+X,+Y).
        var thirdBoundary = new Circle3Curve(center, z, r, x);
        var tangentX = new Point3D(hx, hy - r, hz - r);
        var tangentY = new Point3D(hx - r, hy, hz - r);
        var tangentZ = new Point3D(hx - r, hy - r, hz);
        var candidate = new SphericalFilletCornerCandidateWitness(
            center, r, sphere, replacementA, replacementB, thirdBoundary, new ParameterInterval(0d, double.Pi / 2d), tangentX, tangentY, tangentZ,
            "CylindricalFillet(SharedEdge(+X,+Y))", 0d, (double.Pi * r) / 2d);
        var construction = new LocalizedFilletJunctionConstruction(
            $"construction:{input.FeatureId}:fillet-junction",
            Feature(input, "A", "+X", "SharedEdge(+X,+Z)"),
            Feature(input, "B", "+Y", "SharedEdge(+Y,+Z)"),
            new Point3D(hx, hy, hz), candidate,
            "inside:x<=max,y<=max,z<=max; convex exterior removal",
            "history-known-axis-aligned-rectangular-box; exact-spherical-candidate-derived");

        return Rejected(Fail(
            FilletJunctionErrorKind.CornerPatchSurfaceRequired,
            "localized-fillet-junction-corner-patch-surface-required",
            "The exact spherical candidate is tangent to the two selected cylinders, but its remaining boundary requires the unselected +X/+Y cylindrical fillet; it cannot close against retained +X, +Y, or +Z support regions.",
            $"sphere-center=({center.X:R},{center.Y:R},{center.Z:R});radius={r:R}",
            "candidate-cylinder-tangency-deviation=0",
            $"unowned-third-boundary-length={(double.Pi * r / 2d):R}",
            "required-adjacent-surface=CylindricalFillet(SharedEdge(+X,+Y))",
            "support-plane-intersections=three tangent points, not trim curves"), construction);
    }

    private static AirLocalizedEdgeJunctionFilletCompileResult Rejected(FilletJunctionError error, LocalizedFilletJunctionConstruction? construction = null) =>
        new(false, construction, error, [error.Code, .. error.Evidence]);

    private static AirEdgeFinishFeature Feature(AirLocalizedEdgeJunctionFilletCompileRequest input, string suffix, string face, string edge) => new(
        $"{input.FeatureId}.{suffix}", $"{input.FeatureName}.{suffix}", input.BodyId,
        new AirFaceBoundarySelection(face, edge, false), AirLocalizedEdgeFinishKind.Fillet,
        new AirConstantRadiusEdgeFinishRule(input.RadiusA), input.SourceSpan,
        "generated/history-known-axis-aligned-rectangular-prism", AirFeatureAdmissionStatus.Deferred,
        "localized-fillet-junction-corner-patch-surface-required");

    private static bool FinitePositive(double value) => double.IsFinite(value) && value > Tol;
    private static bool Matches(string face, string target, string expectedFace) =>
        string.Equals(face, expectedFace, StringComparison.Ordinal) && string.Equals(target, "SharedEdgePlusZ", StringComparison.Ordinal);
}
