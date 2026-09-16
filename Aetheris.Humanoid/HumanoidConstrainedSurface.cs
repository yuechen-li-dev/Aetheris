using System.Numerics;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Humanoid;

public sealed record RegionDistortion(HumanoidRegionKind Region, int Edges,
    double Median, double P95, double P99, double Maximum, int OrientationReversalProxies);
public sealed record ConstrainedSurfaceEvidence(bool IsAdmissible, bool Finite, int CollapsedTriangles,
    IReadOnlyList<string> FlaggedFaceIds, IReadOnlyList<RegionDistortion> Regions,
    IReadOnlyList<KinematicDiagnostic> Diagnostics);
public sealed record HumanoidSurfaceEvaluation(IReadOnlyList<Point3D> Positions, ConstrainedSurfaceEvidence Evidence);

/// <summary>
/// LBS remains interpolation only. A solved pose is required, and surface screening is independent
/// of pose validity. Failed positions are diagnostic evidence, never an accepted export mesh.
/// </summary>
public static class HumanoidConstrainedSurface
{
    // A screening bound, not an anatomical claim: catches X1's 16.44x catastrophe while
    // retaining smaller nonzero deformations for diagnosis. Any reversal also fails.
    public const double MaximumEdgeRatio = 4;

    public static HumanoidSurfaceEvaluation Evaluate(HumanoidSurface surface, HumanoidSkeleton skeleton,
        long shapeRevision, SolvedHumanoidPose solved)
    {
        if (solved.ShapeRevision != shapeRevision || skeleton.SkeletonId != solved.Skeleton.SkeletonId ||
            skeleton.RestPoseId != solved.Skeleton.RestPoseId || !skeleton.Joints.SequenceEqual(solved.Skeleton.Joints))
            throw new InvalidOperationException("HUM208: Solved pose belongs to a different rest skeleton or shape revision; solve again after morphing.");
        ValidateBindings(surface, skeleton);
        var positions = HumanoidPosing.EvaluateVertices(surface, solved.Skeleton, solved.State).ToArray();
        return new(Array.AsReadOnly(positions), Inspect(surface, solved.Skeleton, solved.GlobalTransforms, positions));
    }

    /// <summary>Explicit diagnostic inspector, also used to compare the historical unconstrained baseline.</summary>
    public static ConstrainedSurfaceEvidence Inspect(HumanoidSurface surface, HumanoidSkeleton skeleton,
        IReadOnlyList<Matrix4x4> globals, IReadOnlyList<Point3D> positions)
    {
        ValidateBindings(surface, skeleton);
        if (globals.Count != skeleton.Joints.Count || positions.Count != surface.Vertices.Count)
            throw new InvalidDataException("Surface inspection array size mismatch.");
        var finite = positions.All(Finite);
        var collapsed = 0;
        var flagged = new List<string>();
        var regions = new List<RegionDistortion>();
        foreach (var group in surface.Faces.GroupBy(f => f.Region).OrderBy(g => g.Key))
        {
            var edges = new SortedSet<(int, int)>();
            var reversed = 0;
            foreach (var f in group)
            {
                var original = (surface.Vertices[f.B].Position - surface.Vertices[f.A].Position)
                    .Cross(surface.Vertices[f.C].Position - surface.Vertices[f.A].Position);
                var normal = (positions[f.B] - positions[f.A]).Cross(positions[f.C] - positions[f.A]);
                var joint = new[] { f.A, f.B, f.C }.SelectMany(i => surface.SkinWeights[i].Weights)
                    .GroupBy(w => w.JointIndex).OrderByDescending(g => g.Sum(w => w.Weight)).ThenBy(g => g.Key).First().Key;
                var expected = Vector3.TransformNormal(new((float)original.X, (float)original.Y, (float)original.Z), skeleton.Joints[joint].InverseBind * globals[joint]);
                if (normal.LengthSquared < 4e-8) collapsed++;
                else if (normal.Dot(new(expected.X, expected.Y, expected.Z)) <= 0) { reversed++; flagged.Add(f.Id); }
                Add(f.A, f.B); Add(f.B, f.C); Add(f.C, f.A);
            }
            var ratios = edges.Select(e =>
            {
                var before = (surface.Vertices[e.Item1].Position - surface.Vertices[e.Item2].Position).Length;
                var after = (positions[e.Item1] - positions[e.Item2]).Length;
                // Keep serialized evidence finite while still unconditionally failing collapsed/nonfinite edges.
                return before > 0 && after > 0 && double.IsFinite(after) ? Math.Max(before / after, after / before) : double.MaxValue;
            }).Order().ToArray();
            regions.Add(new(group.Key, ratios.Length, Percentile(.5), Percentile(.95), Percentile(.99), ratios[^1], reversed));
            double Percentile(double p) => ratios[(int)Math.Ceiling(p * ratios.Length) - 1];
            void Add(int a, int b) => edges.Add(a < b ? (a, b) : (b, a));
        }
        var d = new List<KinematicDiagnostic>();
        if (!finite || collapsed > 0 || regions.Any(r => r.Maximum > MaximumEdgeRatio))
            d.Add(new("HUM206", "Surface is nonfinite, collapsed, or exceeds the 4x bidirectional edge-ratio screening bound."));
        if (flagged.Count > 0) d.Add(new("HUM207", "Dominant-influence transported-normal screen failed; this is an orientation proxy, not a self-intersection count."));
        return new(d.Count == 0, finite, collapsed, flagged.AsReadOnly(), regions.AsReadOnly(), d.AsReadOnly());
    }

    private static void ValidateBindings(HumanoidSurface surface, HumanoidSkeleton skeleton)
    {
        if (surface.Vertices.Count == 0 || surface.Faces.Count == 0 || surface.SkinWeights.Count != surface.Vertices.Count)
            throw new InvalidDataException("HUM005: Incomplete surface or skin bindings.");
        for (var i = 0; i < surface.Vertices.Count; i++)
        {
            var skin = surface.SkinWeights[i];
            if (!Finite(surface.Vertices[i].Position) || skin.VertexIndex != i || skin.Weights.Count == 0 ||
                skin.Weights.Any(w => w.JointIndex < 0 || w.JointIndex >= skeleton.Joints.Count || !double.IsFinite(w.Weight) || w.Weight < 0) ||
                Math.Abs(skin.Weights.Sum(w => w.Weight) - 1) > CanonicalAdultStandardV1.WeightTolerance)
                throw new InvalidDataException("HUM005: Invalid surface coordinate or normalized skin binding.");
        }
        if (surface.Faces.Any(f => new[] { f.A, f.B, f.C }.Any(i => i < 0 || i >= surface.Vertices.Count) || f.A == f.B || f.A == f.C || f.B == f.C))
            throw new InvalidDataException("HUM008: Invalid face binding.");
    }
    private static bool Finite(Point3D p) => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z);
}
