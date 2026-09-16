using System.Numerics;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Humanoid;

public enum SurfaceAttachmentClass { FixedToExistingEvaluation, BlendedTransition }
public sealed record DeformationPatchVertex(int SurfaceIndex, SurfaceAttachmentClass Attachment, bool Boundary);
public sealed record DeformationCandidate(string Id, IReadOnlyList<Point3D> PatchPositions, string Construction);

/// <summary>One closed face region with a fixed boundary. Only a solved X2 pose can create this context.</summary>
public sealed class HumanoidDeformationContext
{
    private HumanoidDeformationContext() { }
    public HumanoidSurface Surface { get; private init; } = null!;
    public HumanoidSkeleton Skeleton { get; private init; } = null!;
    public SolvedHumanoidPose Solved { get; private init; } = null!;
    public HumanoidJointKind Joint { get; private init; }
    public long ShapeRevision => Solved.ShapeRevision;
    public IReadOnlyList<Point3D> Baseline { get; private init; } = null!;
    public IReadOnlyList<DeformationPatchVertex> Vertices { get; private init; } = null!;
    public IReadOnlyList<HumanoidFace> Faces { get; private init; } = null!;
    public IReadOnlyList<(int A, int B)> Edges { get; private init; } = null!;
    internal IReadOnlyDictionary<int, int> LocalIndex { get; private init; } = null!;
    internal IReadOnlyList<Matrix4x4> SkinTransforms { get; private init; } = null!;

    public static HumanoidDeformationContext Create(HumanoidSurface surface, HumanoidSkeleton skeleton,
        long shapeRevision, SolvedHumanoidPose solved, HumanoidJointKind joint)
    {
        var left = joint.ToString().StartsWith("Left");
        var hip = joint is HumanoidJointKind.LeftHip or HumanoidJointKind.RightHip;
        var knee = joint is HumanoidJointKind.LeftKnee or HumanoidJointKind.RightKnee;
        if (!hip && !knee) throw new ArgumentException("HUM308: Only hip and knee patch contexts are implemented.");
        if (!skeleton.Joints.Any(j => j.Kind == joint)) throw new ArgumentException("Missing patch joint.");
        // X2 performs the revision/skeleton/binding checks before any candidate is generated.
        var baseline = HumanoidConstrainedSurface.Evaluate(surface, skeleton, shapeRevision, solved).Positions;
        surface = surface with
        {
            Vertices = Array.AsReadOnly(surface.Vertices.ToArray()), Faces = Array.AsReadOnly(surface.Faces.ToArray()),
            SkinWeights = Array.AsReadOnly(surface.SkinWeights.Select(w => w with { Weights = Array.AsReadOnly(w.Weights.ToArray()) }).ToArray())
        };
        skeleton = solved.Skeleton;
        var prefix = left ? "Left" : "Right";
        var parent = hip ? HumanoidJointKind.Pelvis : Enum.Parse<HumanoidJointKind>(prefix + "Hip");
        var regions = hip ? new[] { HumanoidRegionKind.Pelvis, Enum.Parse<HumanoidRegionKind>(prefix + "Thigh") }
            : new[] { Enum.Parse<HumanoidRegionKind>(prefix + "Thigh"), Enum.Parse<HumanoidRegionKind>(prefix + "Shin"), Enum.Parse<HumanoidRegionKind>(prefix + "Knee") };
        var faces = surface.Faces.Where(f => regions.Contains(f.Region)).ToArray();
        if (faces.Length == 0) throw new ArgumentException("HUM308: Empty transition patch.");
        var indices = faces.SelectMany(f => new[] { f.A, f.B, f.C }).Distinct().Order().ToArray();
        var boundary = surface.Faces.Where(f => !regions.Contains(f.Region)).SelectMany(f => new[] { f.A, f.B, f.C }).ToHashSet();
        var vertices = indices.Select(i =>
        {
            var weights = surface.SkinWeights[i].Weights;
            var activeWeight = weights.Where(w => skeleton.Joints[w.JointIndex].Kind == joint).Sum(w => w.Weight);
            var editable = !boundary.Contains(i) && activeWeight > 1e-8 && activeWeight < 1 - 1e-8 &&
                weights.All(w => skeleton.Joints[w.JointIndex].Kind == joint || skeleton.Joints[w.JointIndex].Kind == parent);
            return new DeformationPatchVertex(i, editable ? SurfaceAttachmentClass.BlendedTransition : SurfaceAttachmentClass.FixedToExistingEvaluation, boundary.Contains(i));
        }).ToArray();
        var edges = new SortedSet<(int, int)>();
        foreach (var f in faces) { Add(f.A, f.B); Add(f.B, f.C); Add(f.C, f.A); }
        void Add(int a, int b) => edges.Add(a < b ? (a, b) : (b, a));
        return new()
        {
            Surface = surface, Skeleton = skeleton, Solved = solved, Joint = joint,
            Baseline = baseline, Vertices = Array.AsReadOnly(vertices), Faces = Array.AsReadOnly(faces),
            Edges = Array.AsReadOnly(edges.ToArray()),
            LocalIndex = new System.Collections.ObjectModel.ReadOnlyDictionary<int, int>(indices.Select((v, i) => (v, i)).ToDictionary(x => x.v, x => x.i)),
            SkinTransforms = Array.AsReadOnly(skeleton.Joints.Select((j, i) => j.InverseBind * solved.GlobalTransforms[i]).ToArray())
        };
    }

    /// <summary>Diagnostic assembly helper. Callers must not treat rejected candidate positions as admitted output.</summary>
    public IReadOnlyList<Point3D> AssembleDiagnostic(DeformationCandidate candidate)
    {
        if (candidate.PatchPositions.Count != Vertices.Count) throw new ArgumentException("Patch position count mismatch.");
        var result = Baseline.ToArray();
        for (var i = 0; i < Vertices.Count; i++) result[Vertices[i].SurfaceIndex] = candidate.PatchPositions[i];
        return Array.AsReadOnly(result);
    }
}

public enum TransitionBlendMethod { Linear, DualQuaternion }
public sealed record TransitionCandidateSpec(string Id, TransitionBlendMethod Method, double WeightExponent);

public static class HumanoidDeformationCandidates
{
    public static IReadOnlyList<TransitionCandidateSpec> Default { get; } = Array.AsReadOnly(new TransitionCandidateSpec[]
    {
        new("lbs.baseline", TransitionBlendMethod.Linear, 1),
        new("lbs.weights-softened", TransitionBlendMethod.Linear, .5),
        new("lbs.weights-sharpened", TransitionBlendMethod.Linear, 2),
        new("dqs.baseline-weights", TransitionBlendMethod.DualQuaternion, 1),
        new("dqs.weights-softened", TransitionBlendMethod.DualQuaternion, .5),
        new("dqs.weights-sharpened", TransitionBlendMethod.DualQuaternion, 2)
    });

    public static DeformationCandidate Generate(HumanoidDeformationContext context, TransitionCandidateSpec spec)
    {
        if (!Enum.IsDefined(spec.Method) || !double.IsFinite(spec.WeightExponent) || spec.WeightExponent is < .5 or > 2)
            throw new ArgumentException("Bounded transition generators support exponents [0.5,2].");
        var output = new Point3D[context.Vertices.Count];
        for (var i = 0; i < output.Length; i++)
        {
            var patch = context.Vertices[i];
            var index = patch.SurfaceIndex;
            if (patch.Attachment == SurfaceAttachmentClass.FixedToExistingEvaluation ||
                spec.Method == TransitionBlendMethod.Linear && spec.WeightExponent == 1)
            { output[i] = context.Baseline[index]; continue; }
            var weights = context.Surface.SkinWeights[index].Weights;
            var total = weights.Sum(w => Math.Pow(w.Weight, spec.WeightExponent));
            var modified = weights.Select(w => w with { Weight = Math.Pow(w.Weight, spec.WeightExponent) / total }).ToArray();
            var p = context.Surface.Vertices[index].Position;
            var source = new Vector3((float)p.X, (float)p.Y, (float)p.Z);
            Vector3 transformed;
            if (spec.Method == TransitionBlendMethod.Linear)
                transformed = modified.Aggregate(Vector3.Zero, (sum, w) => sum + Vector3.Transform(source, context.SkinTransforms[w.JointIndex]) * (float)w.Weight);
            else transformed = DualQuaternionPoint(source, modified, context.SkinTransforms);
            output[i] = new(transformed.X, transformed.Y, transformed.Z);
        }
        return new(spec.Id, Array.AsReadOnly(output), $"{spec.Method}; normalized generated X1 weights exponent={spec.WeightExponent:R}; fixed patch boundary; no source-weight claim");
    }

    private static Vector3 DualQuaternionPoint(Vector3 point, IReadOnlyList<JointWeight> weights, IReadOnlyList<Matrix4x4> transforms)
    {
        var real = new Quaternion(0, 0, 0, 0); var dual = real;
        var reference = Quaternion.CreateFromRotationMatrix(transforms[weights[0].JointIndex]);
        foreach (var w in weights)
        {
            var m = transforms[w.JointIndex];
            var q = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(m));
            var d = Quaternion.Multiply(new Quaternion(m.Translation, 0), q) * .5f;
            var sign = Quaternion.Dot(reference, q) < 0 ? -1 : 1;
            real += q * (float)(w.Weight * sign); dual += d * (float)(w.Weight * sign);
        }
        var length = real.Length();
        if (length < 1e-8f) throw new InvalidOperationException("Degenerate dual-quaternion blend.");
        real *= 1 / length; dual *= 1 / length;
        dual -= real * Quaternion.Dot(real, dual);
        var translation = Quaternion.Multiply(dual, Quaternion.Conjugate(real)) * 2;
        return Vector3.Transform(point, real) + new Vector3(translation.X, translation.Y, translation.Z);
    }
}
