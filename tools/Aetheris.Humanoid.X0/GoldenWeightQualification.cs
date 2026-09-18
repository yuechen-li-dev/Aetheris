using System.Security.Cryptography;
using System.Text.Json;
using Aetheris.Humanoid;
using Aetheris.Kernel.Core.Math;

/// <summary>Bounded research import of portable canonical-joint weights; no rig synthesis.</summary>
internal static class GoldenWeightQualification
{
    public static int Run(string input, string rig, string weightsPath, string corpusPath, string output)
    {
        var candidate = JsonSerializer.Deserialize<AntoniaAdoptionCandidate>(File.ReadAllText(input), HumanoidArtifactIO.JsonOptions)
            ?? throw new InvalidDataException("Missing Antonia candidate.");
        var adoption = AntoniaReferenceRigAdapter.Adopt(candidate, rig);
        var skeleton = adoption.Skeleton;
        using var document = JsonDocument.Parse(File.ReadAllText(weightsPath));
        var root = document.RootElement;
        Require(root.GetProperty("schema").GetString() == "aetheris.humanoid.experimental-weights.v1", "Unsupported weight schema.");
        Require(root.GetProperty("topologyId").GetString() == adoption.Surface.TopologyId, "Weight topology mismatch.");
        Require(root.GetProperty("connectivityHash").GetString() == adoption.Surface.ConnectivityHash, "Weight connectivity hash mismatch.");
        Require(root.GetProperty("referenceRigSha256").GetString() == Sha(rig), "Weight reference rig hash mismatch.");
        Require(root.GetProperty("skinning").GetString() == "linear-blend-skinning", "Unsupported skinning convention.");
        var extensions = root.GetProperty("deformationRigExtensions");
        Require(extensions.ValueKind == JsonValueKind.Array && extensions.GetArrayLength() == 0, "Deformation rig extensions are unsupported.");
        var maximum = root.GetProperty("maxInfluences").GetInt32();
        Require(maximum is >= 1 and <= 8, "This experiment supports one to eight influences.");
        var vertices = root.GetProperty("vertices");
        Require(vertices.GetArrayLength() == adoption.Surface.Vertices.Count, "Weight vertex count mismatch.");
        var jointIndices = skeleton.Joints.Select((joint, index) => (joint.Kind, index)).ToDictionary(x => x.Kind.ToString(), x => x.index, StringComparer.Ordinal);
        var imported = new Dictionary<string, JointWeight[]>(StringComparer.Ordinal);
        foreach (var vertex in vertices.EnumerateArray())
        {
            var id = vertex.GetProperty("vertexId").GetString() ?? throw new InvalidDataException("Missing vertex ID.");
            var entries = vertex.GetProperty("weights");
            Require(entries.GetArrayLength() is > 0 && entries.GetArrayLength() <= maximum, "Influence count outside declared bound: " + id);
            var weights = new List<JointWeight>();
            foreach (var entry in entries.EnumerateArray())
            {
                var bone = entry.GetProperty("boneId").GetString();
                Require(bone is not null && jointIndices.ContainsKey(bone), "Unknown canonical bone: " + bone);
                var value = entry.GetProperty("weight").GetDouble();
                Require(double.IsFinite(value) && value >= 0, "Invalid weight: " + id);
                weights.Add(new(jointIndices[bone!], value));
            }
            Require(weights.Select(w => w.JointIndex).Distinct().Count() == weights.Count, "Duplicate bone influence: " + id);
            Require(Math.Abs(weights.Sum(w => w.Weight) - 1) <= CanonicalAdultStandardV1.WeightTolerance, "Unnormalized weight sum: " + id);
            Require(imported.TryAdd(id, weights.ToArray()), "Duplicate vertex ID: " + id);
        }
        Require(adoption.Surface.Vertices.All(v => imported.ContainsKey(v.Id)), "Stable vertex ID set mismatch.");
        var surface = adoption.Surface with { SkinWeights = adoption.Surface.Vertices.Select((v, i) => new VertexSkinWeights(i, imported[v.Id])).ToArray() };
        using var corpus = JsonDocument.Parse(File.ReadAllText(corpusPath));
        var requests = corpus.RootElement.EnumerateArray().Select(item =>
        {
            var name = item.GetProperty("name").GetString() ?? throw new InvalidDataException("Missing pose name.");
            Require(name.Length > 0 && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'), "Unsafe pose filename: " + name);
            var joints = item.GetProperty("requested").EnumerateArray().Select(joint =>
            {
                var kind = joint.GetProperty("joint").GetString();
                Require(kind is not null && jointIndices.ContainsKey(kind), "Unknown pose joint: " + kind);
                return new AnatomicalJointRequest(Enum.Parse<HumanoidJointKind>(kind!), Angle("flexionDegrees"), Angle("abductionDegrees"), Angle("twistDegrees"));
                double Angle(string key)
                {
                    var value = joint.TryGetProperty(key, out var angle) ? angle.GetDouble() : 0;
                    Require(double.IsFinite(value), "Nonfinite pose angle: " + name);
                    return value;
                }
            }).ToArray();
            return (name, joints);
        }).ToArray();
        Require(requests.Length > 0 && requests.Select(r => r.name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == requests.Length, "Empty corpus or duplicate pose names.");
        Directory.CreateDirectory(output);
        Write("vertex-order.json", surface.Vertices.Select(v => v.Id));
        var neutral = HumanoidKinematicSolver.Solve(skeleton, new("import-bind-check", skeleton.SkeletonId, skeleton.RestPoseId, 0, []));
        Require(neutral.Pose is not null, "Reference neutral solve failed.");
        var bind = HumanoidConstrainedSurface.Evaluate(surface, skeleton, 0, neutral.Pose!);
        var bindMaximumMm = bind.Positions.Select((p, i) => (p - surface.Vertices[i].Position).Length).Max();
        var heightMm = surface.Vertices.Max(v => v.Position.Z) - surface.Vertices.Min(v => v.Position.Z);
        var cases = new List<object>();
        foreach (var (name, joints) in requests)
        {
            var solve = HumanoidKinematicSolver.Solve(skeleton, new(name, skeleton.SkeletonId, skeleton.RestPoseId, 0, joints));
            if (solve.Pose is not { } posed)
            {
                cases.Add(new { name, requested = joints, solve.IsSolved, solve.Diagnostics, solve.Projections });
                continue;
            }
            var evaluated = HumanoidConstrainedSurface.Evaluate(surface, skeleton, 0, posed);
            var obj = name + (evaluated.Evidence.IsAdmissible ? ".screened.obj" : ".failed-diagnostic.obj");
            Export(Path.Combine(output, obj), surface, evaluated.Positions);
            var positions = name + ".positions.json";
            Write(positions, evaluated.Positions.Select(p => new[] { p.X, p.Y, p.Z }));
            cases.Add(new { name, requested = joints, solve.IsSolved, solve.Diagnostics, solve.Projections,
                maximumCenterResidualMm = posed.Residuals.Select(r => r.CenterMm).DefaultIfEmpty().Max(),
                maximumLinkResidualMm = posed.Residuals.Select(r => r.LinkLengthMm).DefaultIfEmpty().Max(),
                surfaceEvidence = evaluated.Evidence, obj, positions });
        }
        Write("evidence.json", new
        {
            schema = "aetheris.humanoid.rerig-x1.golden-weight-import.v1", solverVersion = HumanoidKinematicSolver.Version,
            sourceCandidateSha256 = Sha(input), referenceRigSha256 = Sha(rig), weightsSha256 = Sha(weightsPath), corpusSha256 = Sha(corpusPath),
            surface.TopologyId, surface.ConnectivityHash, vertexCount = surface.Vertices.Count,
            declaredMaximumInfluences = maximum, observedMaximumInfluences = surface.SkinWeights.Max(w => w.Weights.Count),
            maximumWeightSumError = surface.SkinWeights.Max(w => Math.Abs(w.Weights.Sum(x => x.Weight) - 1)),
            bindMaximumMm, bindMaximumNormalizedByHeight = bindMaximumMm / heightMm, heightMm,
            bindWithinExistingTolerance = bindMaximumMm <= HumanoidKinematicSolver.LinearToleranceMm,
            stableVertexOrder = "vertex-order.json", adoption.Evidence, cases,
            limitations = new[] { "Research diagnostic export only; no canonical promotion or visual acceptance.", "Orientation screen is a proxy; self-intersection count unavailable.", "Only surface skin weights replaced; source topology, reference skeleton and solver unchanged." }
        });
        Console.WriteLine($"Golden-weight import evidence: {Path.Combine(output, "evidence.json")}; {cases.Count} pose results.");
        return 0;

        void Write(string file, object value) => File.WriteAllText(Path.Combine(output, file), JsonSerializer.Serialize(value, HumanoidArtifactIO.JsonOptions));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException("GOLDEN_IMPORT: " + message);
    }
    private static string Sha(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Export(string path, HumanoidSurface surface, IReadOnlyList<Point3D> positions)
    {
        using var writer = new StreamWriter(path, false, new System.Text.UTF8Encoding(false));
        writer.WriteLine("# NONCANONICAL diagnostic; Antonia Polygon CC-BY-3.0 Olaf Delgado-Friedrichs; no endorsement");
        foreach (var p in positions) writer.WriteLine(FormattableString.Invariant($"v {p.X:R} {p.Y:R} {p.Z:R}"));
        foreach (var f in surface.Faces) writer.WriteLine($"f {f.A + 1} {f.B + 1} {f.C + 1}");
    }
}
