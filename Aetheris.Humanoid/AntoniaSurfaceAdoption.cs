using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Humanoid;

public sealed record AntoniaSourcePolygon(int SourceFaceIndex, int[] SourceVertexIndices, string Group, string Material);
public sealed record AntoniaPreparationEvidence(bool Finite, int OrientationReversalProxies, int CollapsedTriangles,
    double MaximumEdgeLengthRatio, double BindReconstructionMaximumMm,
    IReadOnlyDictionary<HumanoidRegionKind, int> ReversalsByRegion, IReadOnlyList<string> ReversalFaceIds,
    IReadOnlyList<string> WorstEdgeVertexIds);
public sealed record AntoniaAdoptionCandidate(string Status, HumanoidSurface SourcePoseSurface,
    HumanoidSkeleton SourcePoseSkeleton, HumanoidPoseState PreparationPose,
    IReadOnlyList<Point3D> PreparedPositions, IReadOnlyList<AntoniaSourcePolygon> SourcePolygons,
    IReadOnlyList<int> SourceVertexIndices, double ScaleToMm, double SourceSoleY,
    double SourcePelvisZ, double SymmetryMaximumMm, double PreparedSymmetryMaximumMm, AntoniaPreparationEvidence PreparationEvidence,
    HumanoidSkeleton PreparedSkeleton, IReadOnlyDictionary<string, AntoniaPreparationEvidence> PoseSweep,
    HumanoidProvenance Provenance);

/// <summary>
/// Bounded, hash-pinned adoption experiment for the original CC-BY Antonia 1.2 figure.
/// A candidate is deliberately not a CanonicalHumanoid: source-pose preparation must pass
/// geometric and human review before landmarks, measurements and canonical admission exist.
/// No source rig, weights, textures, morphs, Blender data or CharMorph code are consumed.
/// </summary>
public static class AntoniaSurfaceAdoption
{
    public const string Revision = "08c9767691daad1382dfc6980ee83e31514b4879";
    public const string SourceSha256 = "0d9e918f19a497adebb51dc8f4ac921c41753b157c50809a1d760dd45ec01194";
    public const string NoticeSha256 = "e9385525cf613450e8673703f7959cd869e2d9c1290d474a99ca4863d734f4ae";
    public const string CandidateTopologyId = "aetheris.humanoid.antonia.adoption-candidate.v1";
    public const string Version = "antonia-original-adoption.v1";
    public const string SourceRepository = "https://github.com/odf/Antonia.Polygon";

    private static readonly HashSet<string> AdmittedMaterials = new(StringComparer.Ordinal)
    {
        "skin_ARMS", "skin_BODY", "skin_HEAD", "skin_LEGS", "lips", "nailsFingers", "nailsToes",
        "scleraLeft", "scleraRight", "irisLeft", "irisRight", "pupilLeft", "pupilRight", "corneaLeft", "corneaRight"
    };

    public static AntoniaAdoptionCandidate Prepare(string objPath, string noticePath)
    {
        Verify(objPath, SourceSha256);
        Verify(noticePath, NoticeSha256);
        var source = new List<Point3D>();
        var polygons = new List<AntoniaSourcePolygon>();
        string group = "", material = "";
        var faceIndex = 0;
        foreach (var line in File.ReadLines(objPath))
        {
            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length == 0) continue;
            switch (fields[0])
            {
                case "v":
                    source.Add(new(Parse(fields[1]), Parse(fields[2]), Parse(fields[3])));
                    break;
                case "g": group = fields[1]; break;
                case "usemtl": material = fields[1]; break;
                case "f":
                    if (fields.Length != 5) throw new InvalidDataException("Pinned Antonia source must contain only quads.");
                    if (AdmittedMaterials.Contains(material))
                        polygons.Add(new(faceIndex, fields.Skip(1).Select(x => int.Parse(x.Split('/')[0], CultureInfo.InvariantCulture) - 1).ToArray(), group, material));
                    faceIndex++;
                    break;
            }
        }
        if (source.Count != 38822 || faceIndex != 38614) throw new InvalidDataException("Original Antonia topology count mismatch.");
        var sourceIds = polygons.SelectMany(p => p.SourceVertexIndices).Distinct().Order().ToArray();
        var remap = sourceIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        var groups = polygons.GroupBy(p => p.Group).ToDictionary(g => g.Key, g => g.SelectMany(p => p.SourceVertexIndices).Distinct().Order().ToArray());
        var sole = sourceIds.Min(i => source[i].Y);
        var scale = 1750 / (sourceIds.Max(i => source[i].Y) - sole);
        var pelvisZ = groups["hip"].Intersect(groups["waist"]).Select(i => source[i].Z).Average();
        // (-X, Z, Y) has determinant +1. Original l* groups have positive X.
        Point3D Map(Point3D p) => new(-p.X * scale, (p.Z - pelvisZ) * scale, (p.Y - sole) * scale);
        var memberships = sourceIds.ToDictionary(i => i, _ => new List<string>());
        foreach (var (name, ids) in groups) foreach (var id in ids) memberships[id].Add(name);
        var positions = sourceIds.Select(i => Map(source[i])).ToArray();
        var symmetry = PairSymmetry(positions);
        var vertices = sourceIds.Select((id, i) => new HumanoidVertex($"antonia:v:{id:D6}", positions[i],
            Region(memberships[id].Order(StringComparer.Ordinal).First()), symmetry[i])).ToArray();
        var faces = new List<HumanoidFace>();
        var componentFaces = new Dictionary<HumanoidComponentKind, List<int>>();
        foreach (var polygon in polygons)
        {
            var ids = polygon.SourceVertexIndices.Select(i => remap[i]).ToArray();
            var region = Region(polygon.Group);
            var kind = polygon.Group switch { "lEye" => HumanoidComponentKind.LeftEye, "rEye" => HumanoidComponentKind.RightEye, _ => HumanoidComponentKind.OuterSkin };
            if (!componentFaces.TryGetValue(kind, out var list)) componentFaces[kind] = list = [];
            // Preserve authored polygon identity in SourcePolygons; freeze 0-2 binding diagonal.
            for (var t = 0; t < 2; t++)
            {
                list.Add(faces.Count);
                faces.Add(new($"antonia:f:{polygon.SourceFaceIndex:D6}:t{t}", ids[0], ids[t + 1], ids[t + 2], region, null));
            }
        }
        var components = componentFaces.OrderBy(x => x.Key).Select(x => new HumanoidComponent(
            "antonia:" + x.Key, x.Key, x.Value, ["Material-subset boundaries retained; watertightness not asserted."])).ToArray();
        var skeleton = BuildSkeleton(groups, source, Map);
        var weights = GenerateWeights(sourceIds, memberships,
            polygons.Select(p => p.SourceVertexIndices.Select(i => remap[i]).ToArray()).ToArray(), skeleton);
        var hash = Hash(string.Join(";", faces.Select(f => $"{f.A},{f.B},{f.C}")));
        var regions = Enum.GetValues<HumanoidRegionKind>().ToDictionary(r => r, r => Swap(r));
        var surface = new HumanoidSurface(CandidateTopologyId, CandidateTopologyId + ".quad-diagonal-02.v1", hash,
            vertices, faces, faces.Select((f, i) => new BindingTriangle(f.Id, i, f.A, f.B, f.C)).ToArray(),
            null, weights, components, regions);
        var preparation = Preparation(skeleton);
        var prepared = HumanoidPosing.EvaluateVertices(surface, skeleton, preparation);
        var bind = HumanoidPosing.EvaluateVertices(surface, skeleton, new("source-bind", [], HumanoidTransform.Identity));
        var bindError = bind.Select((p, i) => (p - positions[i]).Length).Max();
        var evidence = InspectPreparation(surface, skeleton, preparation, prepared, bindError);
        var preparedSkeleton = Rebind(skeleton, HumanoidPosing.GlobalPose(skeleton, preparation));
        var preparedSurface = surface with { Vertices = vertices.Select((v, i) => v with { Position = prepared[i] }).ToArray() };
        var preparedBind = HumanoidPosing.EvaluateVertices(preparedSurface, preparedSkeleton, new("prepared-bind", [], HumanoidTransform.Identity));
        var preparedBindError = preparedBind.Select((p, i) => (p - prepared[i]).Length).Max();
        var sweep = new Dictionary<string, AntoniaPreparationEvidence>();
        foreach (var pose in QualificationPoses())
            sweep.Add(pose.PoseId, InspectPreparation(preparedSurface, preparedSkeleton, pose,
                HumanoidPosing.EvaluateVertices(preparedSurface, preparedSkeleton, pose), preparedBindError));
        var symmetryError = positions.Select((p, i) => (new Point3D(-p.X, p.Y, p.Z) - positions[symmetry[i]]).Length).Max();
        var preparedSymmetryError = prepared.Select((p, i) => (new Point3D(-p.X, p.Y, p.Z) - prepared[symmetry[i]]).Length).Max();
        var failed = preparedSymmetryError > .001 || sweep.Values.Append(evidence).Any(e => !e.Finite || e.CollapsedTriangles != 0 || e.OrientationReversalProxies != 0);
        return new(failed ? "FailedDeformationGate-NONCANONICAL" : "NeedsReview-NONCANONICAL", surface, skeleton, preparation, prepared, polygons, sourceIds,
            scale, sole, pelvisZ, symmetryError, preparedSymmetryError, evidence, preparedSkeleton, sweep,
            new("aetheris.humanoid.provenance.v1",
                [new("antonia-original-1.2", "adopted-base-figure", SourceRepository, Revision, SourceSha256,
                    "CC-BY-3.0", "selected original body/eye polygons; source-pose preparation experiment", true,
                    "Antonia Polygon by Olaf Delgado-Friedrichs, with phantom3D, MikeJ and others credited in the preserved original notice. No endorsement implied."),
                 new("antonia-original-notice", "license-evidence", SourceRepository + "/blob/" + Revision + "/Runtime/Docs/Antonia/README",
                    Revision, NoticeSha256, "CC-BY-3.0", "original notice retained", true, "Not a grant for separately distributed extras.")],
                ["admitted material subset; original vertex and polygon indices retained", "right-handed frame (-sourceX, sourceZ-pelvisZ, sourceY-soleY), uniformly normalized to 1750 mm",
                 "fixed quad diagonal 0-2; original polygons retained separately", "joint proxies from shared source group boundary centroids; no source rig",
                 "group membership skin seeds; 16 authored-quad-edge adjacency averaging passes; binding diagonals excluded", "shared Aetheris LBS; shoulders and elbows target bilateral 35-degree arms from vertical"],
                Version, Hash(Version + SourceSha256 + NoticeSha256), hash, "SourceAdmitted-RuntimePromotionBlocked",
                ["All source-pose preparation, anatomical proxies and difficult regions require review. No canonical landmark, measurement, attachment or morph admission."]));
    }

    public static IReadOnlyList<HumanoidPoseState> QualificationPoses()
    {
        var result = new List<HumanoidPoseState>();
        foreach (var (name, joint, axis) in new[]
        {
            ("shoulder-abduction", HumanoidJointKind.LeftShoulder, Vector3.UnitY),
            ("shoulder-flexion", HumanoidJointKind.LeftShoulder, Vector3.UnitX),
            ("elbow-flexion", HumanoidJointKind.LeftElbow, Vector3.UnitY),
            ("hip-flexion", HumanoidJointKind.LeftHip, Vector3.UnitX),
            ("hip-abduction", HumanoidJointKind.LeftHip, Vector3.UnitY),
            ("knee-flexion", HumanoidJointKind.LeftKnee, -Vector3.UnitX)
        })
            foreach (var degrees in new[] { 35, 70 })
                result.Add(new(name + "-" + degrees,
                    [new(joint, Quaternion.CreateFromAxisAngle(axis, degrees * MathF.PI / 180))], HumanoidTransform.Identity));
        return result;
    }

    private static HumanoidSkeleton Rebind(HumanoidSkeleton source, IReadOnlyList<Matrix4x4> globals)
    {
        var joints = source.Joints.Select((j, i) =>
        {
            var position = new Vector3(globals[i].M41, globals[i].M42, globals[i].M43);
            var parent = j.ParentIndex is { } pi ? new Vector3(globals[pi].M41, globals[pi].M42, globals[pi].M43) : Vector3.Zero;
            var delta = position - parent;
            var bind = Matrix4x4.CreateTranslation(position);
            Matrix4x4.Invert(bind, out var inverse);
            return j with { LocalRest = new(new(delta.X, delta.Y, delta.Z), Quaternion.Identity), GlobalBind = bind, InverseBind = inverse };
        }).ToArray();
        return source with { RestPoseId = "antonia.candidate.apose.v1", Joints = joints };
    }

    private static HumanoidSkeleton BuildSkeleton(Dictionary<string, int[]> groups, List<Point3D> source, Func<Point3D, Point3D> map)
    {
        Point3D Mean(IEnumerable<int> ids)
        {
            var p = ids.Select(i => source[i]).ToArray();
            if (p.Length == 0) throw new InvalidDataException("Missing group-boundary joint proxy.");
            return map(new(p.Average(v => v.X), p.Average(v => v.Y), p.Average(v => v.Z)));
        }
        Point3D Seam(string a, string b) => Mean(groups[a].Intersect(groups[b]));
        var specs = new List<(HumanoidJointKind Kind, HumanoidJointKind? Parent, Point3D P)>();
        void Add(HumanoidJointKind k, HumanoidJointKind? parent, Point3D p) => specs.Add((k, parent, p));
        Add(HumanoidJointKind.Root, null, Point3D.Origin);
        Add(HumanoidJointKind.Pelvis, HumanoidJointKind.Root, Seam("hip", "waist"));
        Add(HumanoidJointKind.SpineLower, HumanoidJointKind.Pelvis, Seam("waist", "abdomen"));
        Add(HumanoidJointKind.SpineMid, HumanoidJointKind.SpineLower, Seam("abdomen", "chest"));
        Add(HumanoidJointKind.Chest, HumanoidJointKind.SpineMid, Mean(groups["chest"]));
        Add(HumanoidJointKind.Neck, HumanoidJointKind.Chest, Seam("chest", "neck"));
        Add(HumanoidJointKind.Head, HumanoidJointKind.Neck, Seam("neck", "head"));
        foreach (var side in new[] { "l", "r" })
        {
            HumanoidJointKind K(string s) => Enum.Parse<HumanoidJointKind>((side == "l" ? "Left" : "Right") + s);
            Add(K("Clavicle"), HumanoidJointKind.Chest, Seam("chest", side + "Collar"));
            Add(K("Shoulder"), K("Clavicle"), Seam(side + "Collar", side + "Shldr"));
            Add(K("Elbow"), K("Shoulder"), Seam(side + "Shldr", side + "ForeArm"));
            Add(K("Wrist"), K("Elbow"), Seam(side + "ForeArm", side + "Hand"));
            Add(K("Hip"), HumanoidJointKind.Pelvis, Seam("hip", side + "Thigh"));
            Add(K("Knee"), K("Hip"), Seam(side + "Thigh", side + "Shin"));
            Add(K("Ankle"), K("Knee"), Seam(side + "Shin", side + "Foot"));
            Add(K("ToeBase"), K("Ankle"), Seam(side + "Foot", side + "Instep"));
            foreach (var (sourceName, name) in new[] { ("Thumb", "Thumb"), ("Index", "Index"), ("Mid", "Middle"), ("Ring", "Ring"), ("Pinky", "Little") })
            {
                var first = name == "Thumb" ? "Metacarpal" : "Proximal";
                var second = name == "Thumb" ? "Proximal" : "Intermediate";
                Add(K(name + first), K("Wrist"), Seam(side + "Hand", side + sourceName + "1"));
                Add(K(name + second), K(name + first), Seam(side + sourceName + "1", side + sourceName + "2"));
                Add(K(name + "Distal"), K(name + second), Seam(side + sourceName + "2", side + sourceName + "3"));
            }
            Add(K("Eye"), HumanoidJointKind.Head, Mean(groups[side + "Eye"]));
        }
        var index = specs.Select((s, i) => (s.Kind, i)).ToDictionary(x => x.Kind, x => x.i);
        var joints = specs.Select(s =>
        {
            int? parent = s.Parent is { } k ? index[k] : null;
            var delta = s.P - (parent is { } pi ? specs[pi].P : Point3D.Origin);
            var global = Matrix4x4.CreateTranslation((float)s.P.X, (float)s.P.Y, (float)s.P.Z);
            if (!Matrix4x4.Invert(global, out var inverse)) throw new InvalidDataException("Singular source bind.");
            var side = s.Kind.ToString().StartsWith("Left") ? AnatomicalSide.Left : s.Kind.ToString().StartsWith("Right") ? AnatomicalSide.Right : AnatomicalSide.Center;
            return new HumanoidJoint("joint:" + s.Kind, s.Kind, side, parent, new(new(delta.X, delta.Y, delta.Z), Quaternion.Identity), global, inverse,
                s.Kind is HumanoidJointKind.LeftEye or HumanoidJointKind.RightEye);
        }).ToArray();
        return new(CanonicalAdultStandardV1.SkeletonId, "antonia.original-source-pose.v1",
            "linear-blend-skinning; System.Numerics row-vector matrices; pWorld=pModel*InverseBind*GlobalPose",
            joints, Enum.GetValues<HumanoidJointKind>().ToDictionary(k => k, k => Swap(k)));
    }

    private static IReadOnlyList<VertexSkinWeights> GenerateWeights(int[] sourceIds, Dictionary<int, List<string>> memberships,
        IReadOnlyList<int[]> quads, HumanoidSkeleton skeleton)
    {
        var indices = skeleton.Joints.Select((j, i) => (j.Kind, i)).ToDictionary(x => x.Kind, x => x.i);
        var n = sourceIds.Length;
        var count = skeleton.Joints.Count;
        var weights = new double[n][];
        var adjacency = Enumerable.Range(0, n).Select(_ => new SortedSet<int>()).ToArray();
        // Binding diagonals are not authored edges. Using them would introduce a
        // left/right bias into otherwise symmetric weights on mirrored quads.
        foreach (var quad in quads)
            for (var edge = 0; edge < 4; edge++)
            {
                var a = quad[edge]; var b = quad[(edge + 1) % 4];
                adjacency[a].Add(b); adjacency[b].Add(a);
            }
        for (var i = 0; i < n; i++)
        {
            weights[i] = new double[count];
            var joints = memberships[sourceIds[i]].Select(GroupJoint).Distinct().ToArray();
            foreach (var joint in joints) weights[i][indices[joint]] = 1d / joints.Length;
        }
        for (var pass = 0; pass < 16; pass++)
        {
            var next = new double[n][];
            for (var i = 0; i < n; i++)
            {
                next[i] = new double[count];
                for (var j = 0; j < count; j++)
                    next[i][j] = adjacency[i].Count == 0 ? weights[i][j] : .5 * weights[i][j] + .5 * adjacency[i].Sum(k => weights[k][j]) / adjacency[i].Count;
            }
            weights = next;
        }
        return weights.Select((row, i) =>
        {
            var kept = row.Select((w, j) => new JointWeight(j, w)).Where(w => w.Weight > 1e-8).ToArray();
            var total = kept.Sum(w => w.Weight);
            return new VertexSkinWeights(i, kept.Select(w => w with { Weight = w.Weight / total }).ToArray());
        }).ToArray();
    }

    private static HumanoidPoseState Preparation(HumanoidSkeleton skeleton)
    {
        var joints = skeleton.Joints.ToDictionary(j => j.Kind);
        Vector3 P(HumanoidJointKind k) { var m = joints[k].GlobalBind; return new(m.M41, m.M42, m.M43); }
        var rotations = new List<JointPose>();
        foreach (var left in new[] { true, false })
        {
            var shoulder = left ? HumanoidJointKind.LeftShoulder : HumanoidJointKind.RightShoulder;
            var elbow = left ? HumanoidJointKind.LeftElbow : HumanoidJointKind.RightElbow;
            var wrist = left ? HumanoidJointKind.LeftWrist : HumanoidJointKind.RightWrist;
            var target = new Vector3((left ? -1 : 1) * MathF.Sin(35 * MathF.PI / 180), 0, -MathF.Cos(35 * MathF.PI / 180));
            var upper = FromTo(P(elbow) - P(shoulder), target);
            var lower = FromTo(P(wrist) - P(elbow), Vector3.Transform(target, Quaternion.Inverse(upper)));
            rotations.Add(new(shoulder, upper)); rotations.Add(new(elbow, lower));
        }
        return new("antonia.apose-preparation.v1", rotations, HumanoidTransform.Identity);
    }

    private static AntoniaPreparationEvidence InspectPreparation(HumanoidSurface surface, HumanoidSkeleton skeleton,
        HumanoidPoseState pose, IReadOnlyList<Point3D> prepared, double bindError)
    {
        var globals = HumanoidPosing.GlobalPose(skeleton, pose);
        var reversals = new Dictionary<HumanoidRegionKind, int>();
        var reversalIds = new List<string>();
        var worstEdge = Array.Empty<string>();
        var collapsed = 0;
        var maxStretch = 1d;
        foreach (var face in surface.Faces)
        {
            Point3D P(int i) => surface.Vertices[i].Position;
            var oldNormal = (P(face.B) - P(face.A)).Cross(P(face.C) - P(face.A));
            var normal = (prepared[face.B] - prepared[face.A]).Cross(prepared[face.C] - prepared[face.A]);
            var joint = new[] { face.A, face.B, face.C }.SelectMany(i => surface.SkinWeights[i].Weights)
                .GroupBy(w => w.JointIndex).OrderByDescending(g => g.Sum(w => w.Weight)).ThenBy(g => g.Key).First().Key;
            var expected = Vector3.TransformNormal(new((float)oldNormal.X, (float)oldNormal.Y, (float)oldNormal.Z), skeleton.Joints[joint].InverseBind * globals[joint]);
            if (normal.LengthSquared < 4e-8) collapsed++;
            else if (normal.Dot(new(expected.X, expected.Y, expected.Z)) <= 0)
            {
                reversals[face.Region] = reversals.GetValueOrDefault(face.Region) + 1;
                reversalIds.Add(face.Id);
            }
            foreach (var (a, b) in new[] { (face.A, face.B), (face.B, face.C), (face.C, face.A) })
            {
                var before = (P(a) - P(b)).Length;
                var after = (prepared[a] - prepared[b]).Length;
                if (before == 0 || after == 0) throw new InvalidDataException("Collapsed source or preparation edge.");
                var ratio = Math.Max(after / before, before / after);
                if (ratio > maxStretch) { maxStretch = ratio; worstEdge = [surface.Vertices[a].Id, surface.Vertices[b].Id]; }
            }
        }
        return new(prepared.All(p => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z)),
            reversals.Values.Sum(), collapsed, maxStretch, bindError, reversals, reversalIds, worstEdge);
    }

    private static int[] PairSymmetry(IReadOnlyList<Point3D> points)
    {
        var buckets = points.Select((p, i) => (Key: Key(p), i)).GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.Select(x => x.i).Order().ToArray());
        var pair = Enumerable.Repeat(-1, points.Count).ToArray();
        for (var i = 0; i < points.Count; i++)
        {
            if (pair[i] >= 0) continue;
            var p = points[i];
            if (Math.Abs(p.X) < .0001) { pair[i] = i; continue; }
            var target = new Point3D(-p.X, p.Y, p.Z);
            var (x, y, z) = Key(target);
            var candidates = new List<int>();
            for (var dx = -1; dx <= 1; dx++)
                for (var dy = -1; dy <= 1; dy++)
                    for (var dz = -1; dz <= 1; dz++)
                        if (buckets.TryGetValue((x + dx, y + dy, z + dz), out var matches))
                            candidates.AddRange(matches.Where(j => pair[j] < 0 && (target - points[j]).Length < .001));
            if (candidates.Count == 0) throw new InvalidDataException($"No unpaired symmetry partner within 0.001 mm for source vertex {i}.");
            var j = candidates.OrderBy(j => (target - points[j]).LengthSquared).ThenBy(j => j).First();
            pair[i] = j; pair[j] = i;
        }
        return pair;
        static (long, long, long) Key(Point3D p) => ((long)Math.Floor(p.X), (long)Math.Floor(p.Y), (long)Math.Floor(p.Z));
    }

    private static HumanoidRegionKind Region(string group)
    {
        if (group is "head" or "neck" or "chest" or "abdomen" or "waist" or "hip")
            return group switch { "head" => HumanoidRegionKind.Head, "neck" => HumanoidRegionKind.Neck, "chest" => HumanoidRegionKind.Chest, "abdomen" or "waist" => HumanoidRegionKind.Abdomen, _ => HumanoidRegionKind.Pelvis };
        var prefix = group[0] == 'l' ? "Left" : "Right";
        var stem = group[1..];
        var region = stem switch
        {
            "Eye" => "Eye", "Collar" => "Shoulder", "Shldr" => "UpperArm", "ForeArm" => "Forearm", "Hand" => "Hand",
            "Thigh" => "Thigh", "Shin" => "Shin", "Foot" or "Instep" => "Foot", "Toe" or "BigToe1" or "BigToe2" => "Toes",
            _ when stem.StartsWith("Thumb") => "Thumb", _ when stem.StartsWith("Index") => "Index", _ when stem.StartsWith("Mid") => "Middle",
            _ when stem.StartsWith("Ring") => "Ring", _ when stem.StartsWith("Pinky") => "Little",
            _ => throw new InvalidDataException("Unmapped source group: " + group)
        };
        return Enum.Parse<HumanoidRegionKind>(prefix + region);
    }

    private static HumanoidJointKind GroupJoint(string group)
    {
        if (group is "head" or "neck" or "chest" or "abdomen" or "waist" or "hip")
            return group switch { "head" => HumanoidJointKind.Head, "neck" => HumanoidJointKind.Neck, "chest" => HumanoidJointKind.Chest, "abdomen" => HumanoidJointKind.SpineMid, "waist" => HumanoidJointKind.SpineLower, _ => HumanoidJointKind.Pelvis };
        var prefix = group[0] == 'l' ? "Left" : "Right";
        var stem = group[1..];
        var joint = stem switch
        {
            "Eye" => "Eye", "Collar" => "Clavicle", "Shldr" => "Shoulder", "ForeArm" => "Elbow", "Hand" => "Wrist",
            "Thigh" => "Hip", "Shin" => "Knee", "Foot" => "Ankle", "Instep" or "Toe" or "BigToe1" or "BigToe2" => "ToeBase",
            _ => Finger(stem)
        };
        return Enum.Parse<HumanoidJointKind>(prefix + joint);
        static string Finger(string s)
        {
            var name = s[..^1] switch { "Mid" => "Middle", "Pinky" => "Little", var other => other };
            var segment = s[^1] switch { '1' => name == "Thumb" ? "Metacarpal" : "Proximal", '2' => name == "Thumb" ? "Proximal" : "Intermediate", '3' => "Distal", _ => throw new InvalidDataException("Unknown finger group") };
            return name + segment;
        }
    }

    private static T Swap<T>(T value) where T : struct, Enum
    {
        var s = value.ToString();
        return s.StartsWith("Left") ? Enum.Parse<T>("Right" + s[4..]) : s.StartsWith("Right") ? Enum.Parse<T>("Left" + s[5..]) : value;
    }
    private static Quaternion FromTo(Vector3 from, Vector3 to)
    {
        from = Vector3.Normalize(from); to = Vector3.Normalize(to);
        var dot = Vector3.Dot(from, to);
        if (dot < -.99999f) throw new InvalidDataException("Ambiguous antiparallel preparation rotation.");
        return Quaternion.Normalize(new Quaternion(Vector3.Cross(from, to), 1 + dot));
    }
    private static double Parse(string s) => double.Parse(s, CultureInfo.InvariantCulture);
    private static string Hash(string s) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
    private static void Verify(string path, string expected)
    {
        using var stream = File.OpenRead(path);
        if (Convert.ToHexStringLower(SHA256.HashData(stream)) != expected)
            throw new HumanoidDomainException(new(HumanoidDiagnosticCode.HUM002_ReferenceHashMismatch, HumanoidDiagnosticSeverity.Error, "Pinned original Antonia input hash mismatch.", Path.GetFileName(path)));
    }
}
