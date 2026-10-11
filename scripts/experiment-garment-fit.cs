#:project ../Aetheris.Kernel.Firmament/Aetheris.Kernel.Firmament.csproj
#:property PublishAot=false
#:property JsonSerializerIsReflectionEnabledByDefault=true
// Experiments use the real authoring compiler, cloth compiler, solver and body queries.
// No commercial inputs. Rest lengths, masses, seams and qualification limits are retained.
using System.Collections.Immutable;
using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetheris.Cloth3D;
using Aetheris.Humanoid;
using Aetheris.Kernel.Firmament.Garment;
using Aetheris.Kernel.Firmament.Humanoid;

if (args.Length != 4 || args[0] is not ("prefix" or "presentation" or "regions"))
{
    throw new ArgumentException("dotnet run -c Release scripts/experiment-garment-fit.cs -- prefix|presentation|regions source.firmament body.json output-directory");
}
string mode = args[0];
string sourcePath = Path.GetFullPath(args[1]);
string bodyPath = Path.GetFullPath(args[2]);
string output = Path.GetFullPath(args[3]);
Directory.CreateDirectory(output);
var json = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
string source = File.ReadAllText(sourcePath);
string bodyHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(bodyPath)));
var body = HumanoidGameplayBody.Load(bodyPath);
var binding = new GarmentFigureBinding("Anatolia", body, bodyHash);
var garment = Compile(source, sourcePath);

if (mode == "prefix")
{
    if (!source.Contains("meshSize: 60mm;", StringComparison.Ordinal))
    {
        throw new ArgumentException("The controlled prefix comparison requires an explicit 60mm baseline meshSize.");
    }
    var results = new List<object>();
    foreach (int size in new[] { 60, 30, 15 })
    {
        var variant = Compile(source.Replace("meshSize: 60mm;", $"meshSize: {size}mm;"), sourcePath + "/mesh-" + size);
        var surface = PosedSurface(variant.Source.Pose!);
        var contacts = new ClothContacts3D
        {
            BodySurface = surface,
            BodyClearance = variant.Source.BodyClearance,
            Guides = variant.FittingGuides.Select(guide => guide.Constraint).ToImmutableArray(),
        };
        using var solver = new ClothSolver3D(variant.Cloth);
        var watch = Stopwatch.StartNew();
        Console.Error.WriteLine($"meshSize={size}mm: {variant.Cloth.Definition.Positions.Length} vertices, five sewing frames");
        var frames = new List<object>();
        for (int stage = 0; stage < 5; stage++)
        {
            solver.Step(new()
            {
                Gravity = Vector3.Zero, Substeps = 8, Iterations = 24, ContactIterations = 1,
                VelocityDamping = 8, StitchComplianceScale = MathF.Pow(10, 6 * (1 - stage / 29f)),
            }, contacts);
            var snapshot = solver.Capture();
            frames.Add(new { frame = stage + 1, metrics = ClothState3D.Measure(variant.Cloth, snapshot, contacts),
                             seamGap = variant.MaximumSeamGap(snapshot), elapsedSeconds = watch.Elapsed.TotalSeconds });
        }
        var final = solver.Capture();
        var options = new ClothStepOptions3D { Gravity = Vector3.Zero, Substeps = 8, Iterations = 24, ContactIterations = 1 };
        solver.Step(options, contacts);
        var expected = solver.Capture();
        solver.Restore(final);
        solver.Step(options, contacts);
        var actual = solver.Capture();
        bool replay = expected.Positions.SequenceEqual(actual.Positions) && expected.Velocities.SequenceEqual(actual.Velocities);
        File.WriteAllText(Path.Combine(output, $"prefix-{size}.obj"), GarmentExport.Obj(variant, final));
        results.Add(new { meshSizeMm = size, vertices = variant.Cloth.Definition.Positions.Length,
            triangles = variant.Cloth.Definition.Indices.Length / 3, variant.Cloth.ContentKey,
            meshQuality = GarmentMeshInspection.Measure(variant), restAreaM2 = RestArea(variant.Cloth.Definition),
            frames, replayExact = replay, qualifiedDrape = false,
            reason = "Five-frame startup/cost probe, not a completed sewing or wearing sequence" });
        WriteJson("prefix-results.json", new { bodyHash, sourceHash = garment.Source.SourceHash, results });
    }
}
else if (mode == "regions")
{
    int[] vertices = BoundaryBand(garment, "collar", .048f);
    var definition = garment.Cloth.Definition with
    {
        MaterialRegions = [new("collar-band-48mm", vertices.ToImmutableArray(), .25f, 4)],
    };
    var tuned = garment with { Cloth = definition.Compile() };
    int changed = garment.Cloth.Constraints.Zip(tuned.Cloth.Constraints)
        .Count(pair => pair.First.Compliance != pair.Second.Compliance);
    if (changed == 0)
    {
        throw new InvalidOperationException("Experiment region selected no structural stencils.");
    }
    var drape = GarmentDraping.Settle(tuned, body, 60,
        progress => Console.Error.WriteLine($"regional {progress.Phase}: {progress.Completed}/{progress.Total} ({progress.ElapsedSeconds:F1}s)"));
    Export(tuned, drape);
    WriteJson("region-results.json", new
    {
        bodyHash, sourceHash = garment.Source.SourceHash, baselineKey = garment.Cloth.ContentKey,
        tunedKey = tuned.Cloth.ContentKey, selectedVertices = vertices.Length, changedConstraints = changed,
        overrides = definition.MaterialRegions, drape.Metrics, drape.MaximumSeamGap,
        drape.ReplayExact, drape.TransitionReplayExact, drape.GuidesReleased,
        frames = drape.Frames.Select(frame => new { frame.Name, frame.Metrics, frame.MaximumSeamGap, frame.StretchOutliers }),
        accepted = drape.ReplayExact && drape.TransitionReplayExact && drape.GuidesReleased
            && GarmentDrapeLimits.Accept(drape.Metrics, drape.MaximumSeamGap)
            && drape.Frames.All(frame => frame.BodyEvidence.IsAdmissible
                && GarmentDrapeLimits.Accept(frame.Metrics, frame.MaximumSeamGap)),
    });
}
else
{
    string retainedPath = Path.Combine(output, "input-garment.json");
    var retained = ClothGarmentArtifact3D.Load(retainedPath);
    if (retained.SourceHash != garment.Source.SourceHash || retained.Cloth.ContentKey != garment.Cloth.ContentKey)
    {
        throw new InvalidDataException("Presentation input must match the supplied source and cloth definition.");
    }
    var snapshot = retained.Settled ?? throw new InvalidDataException("Presentation needs a retained solver snapshot.");
    string finalPose = garment.Source.Transition is { } transition
        ? garment.Source.Posing!.Transitions[transition].To : garment.Source.Pose!;
    var finalRecipe = garment.Source.Posing!.Resolve(finalPose, "Anatolia");
    var finalPositions = body.EvaluatePositions(HumanoidPoseAuthoring.Solve(finalRecipe, body));
    var surface = GarmentDraping.BodySurface(body, finalPositions);
    File.WriteAllText(Path.Combine(output, "body.obj"), GarmentExport.BodyObj(body, finalPositions));
    var contacts = new ClothContacts3D { BodySurface = surface, BodyClearance = garment.Source.BodyClearance };
    var results = new List<object>();
    foreach (int levels in new[] { 0, 1, 2 })
    {
        var watch = Stopwatch.StartNew();
        var presentation = ClothPresentationBinding3D.Compile(garment.Cloth, levels);
        double compileMs = watch.Elapsed.TotalMilliseconds;
        watch.Restart();
        var mesh = presentation.Evaluate(snapshot);
        double evaluateMs = watch.Elapsed.TotalMilliseconds;
        float maximumClearanceViolation = 0;
        float maximumBodyPenetration = 0;
        float halfThickness = garment.Cloth.Definition.Material.Thickness * .5f;
        float clearance = halfThickness + contacts.BodyClearance;
        foreach (Vector3 point in mesh.Positions)
        {
            MeasurePoint(point);
        }
        for (int face = 0; face < mesh.Indices.Length; face += 3)
        {
            foreach (Vector3 barycentric in ClothBodyContact3D.Samples)
            {
                Vector3 point = mesh.Positions[mesh.Indices[face]] * barycentric.X
                    + mesh.Positions[mesh.Indices[face + 1]] * barycentric.Y
                    + mesh.Positions[mesh.Indices[face + 2]] * barycentric.Z;
                MeasurePoint(point);
            }
        }
        File.WriteAllText(Path.Combine(output, $"presentation-{levels}.obj"), MeshObj(mesh));
        results.Add(new { levels, vertices = mesh.Positions.Length, triangles = mesh.Indices.Length / 3,
            compileMs, evaluateMs, maximumSampledPresentationClearanceViolation = maximumClearanceViolation,
            maximumSampledPresentationBodyPenetration = maximumBodyPenetration,
            simulationMetrics = ClothState3D.Measure(garment.Cloth, snapshot, contacts),
            simulationSeamGap = garment.MaximumSeamGap(snapshot), simulationKey = presentation.SourceContentKey });

        void MeasurePoint(Vector3 point)
        {
            float distance = surface.Query(point).SignedDistance;
            maximumClearanceViolation = MathF.Max(maximumClearanceViolation, clearance - distance);
            maximumBodyPenetration = MathF.Max(maximumBodyPenetration, halfThickness - distance);
        }
    }
    WriteJson("presentation-results.json", new { bodyHash, sourceHash = garment.Source.SourceHash,
        inputHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(retainedPath))), results,
        limits = new[] { "Presentation stencils only", "Original material boundaries preserved",
            "Sampled visible contact is diagnostic, not exhaustive certification", "No changes to solver state" } });
}

CompiledGarment Compile(string text, string identity)
{
    var result = GarmentCompiler.Compile(text, identity, binding);
    if (!result.IsSuccess)
    {
        throw new InvalidDataException(string.Join("; ", result.Diagnostics));
    }
    return result.Garment!;
}

ClothTriangleBody3D PosedSurface(string name)
{
    var recipe = garment.Source.Posing!.Resolve(name, "Anatolia");
    var pose = HumanoidPoseAuthoring.Solve(recipe, body);
    return GarmentDraping.BodySurface(body, body.EvaluatePositions(pose));
}

void WriteJson(string name, object value)
{
    File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value, json));
}

void Export(CompiledGarment compiled, GarmentDrapeResult drape)
{
    File.WriteAllText(Path.Combine(output, "garment.json"), GarmentExport.Artifact(compiled, drape.Snapshot));
    File.WriteAllText(Path.Combine(output, "garment.obj"), GarmentExport.Obj(compiled, drape.Snapshot));
    File.WriteAllText(Path.Combine(output, "body.obj"), GarmentExport.BodyObj(body, drape.BodyPositions));
}

static double RestArea(ClothDefinition3D definition)
{
    double area = 0;
    for (int face = 0; face < definition.Indices.Length; face += 3)
    {
        Vector3 a = definition.Positions[definition.Indices[face]];
        Vector3 b = definition.Positions[definition.Indices[face + 1]];
        Vector3 c = definition.Positions[definition.Indices[face + 2]];
        area += Vector3.Cross(b - a, c - a).Length() * .5;
    }
    return area;
}

static int[] BoundaryBand(CompiledGarment compiled, string name, float width)
{
    var definition = compiled.Cloth.Definition;
    var selected = new List<int>();
    foreach (var panel in compiled.Panels)
    {
        var edges = panel.Edges.Where(entry => entry.Key.Contains(name, StringComparison.Ordinal)).ToArray();
        for (int vertex = panel.VertexStart; vertex < panel.VertexStart + panel.VertexCount; vertex++)
        {
            Vector3 point = definition.Positions[vertex];
            bool inside = false;
            foreach (var edge in edges)
            {
                for (int index = 0; index + 1 < edge.Value.Length; index++)
                {
                    Vector3 a = definition.Positions[edge.Value[index]];
                    Vector3 b = definition.Positions[edge.Value[index + 1]];
                    Vector3 ab = b - a;
                    float fraction = Math.Clamp(Vector3.Dot(point - a, ab) / ab.LengthSquared(), 0, 1);
                    if (Vector3.Distance(point, a + ab * fraction) <= width)
                    {
                        inside = true;
                        break;
                    }
                }
                if (inside)
                {
                    break;
                }
            }
            if (inside)
            {
                selected.Add(vertex);
            }
        }
    }
    return selected.ToArray();
}

static string MeshObj(ClothPresentationMesh3D mesh)
{
    var text = new StringBuilder("# Aetheris compiled presentation stencils; not a simulated collision mesh\n");
    foreach (Vector3 point in mesh.Positions)
    {
        text.AppendLine(FormattableString.Invariant($"v {point.X:R} {point.Y:R} {point.Z:R}"));
    }
    for (int face = 0; face < mesh.Indices.Length; face += 3)
    {
        text.AppendLine($"f {mesh.Indices[face] + 1} {mesh.Indices[face + 1] + 1} {mesh.Indices[face + 2] + 1}");
    }
    return text.ToString();
}
