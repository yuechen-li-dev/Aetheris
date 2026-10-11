#:project ../Aetheris.Kernel.Firmament/Aetheris.Kernel.Firmament.csproj
#:property PublishAot=false
#:property JsonSerializerIsReflectionEnabledByDefault=true
// Read-only inspection and bounded replay probes through the shared CPU solver.
using System.Collections.Immutable;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Cloth3D;
using Aetheris.Humanoid;
using Aetheris.Kernel.Firmament.Garment;
using Aetheris.Kernel.Firmament.Humanoid;

if (args.Length != 4)
{
    throw new ArgumentException("dotnet run -c Release scripts/inspect-garment-strain.cs -- source.firmament body.json garment.json output.json");
}
var json = new JsonSerializerOptions
{
    IncludeFields = true,
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter() },
};
string sourcePath = Path.GetFullPath(args[0]);
string bodyPath = Path.GetFullPath(args[1]);
string snapshotPath = Path.GetFullPath(args[2]);
string output = Path.GetFullPath(args[3]);
if (new[] { sourcePath, bodyPath, snapshotPath }.Contains(output, StringComparer.OrdinalIgnoreCase))
{
    throw new ArgumentException("Inspection output must not replace its input.");
}
string bodyHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(bodyPath)));
var body = HumanoidGameplayBody.Load(bodyPath);
var binding = new GarmentFigureBinding("Anatolia", body, bodyHash);
var result = GarmentCompiler.Compile(File.ReadAllText(sourcePath), sourcePath, binding);
if (!result.IsSuccess)
{
    throw new InvalidDataException(string.Join("; ", result.Diagnostics));
}
var garment = result.Garment!;
using var document = JsonDocument.Parse(File.ReadAllText(snapshotPath));
string sourceHash = garment.Source.SourceHash;
if (document.RootElement.GetProperty("sourceHash").GetString() != sourceHash)
{
    throw new InvalidDataException("Snapshot source hash does not match the supplied authoring source.");
}
var retainedFits = document.RootElement.GetProperty("figureFits").EnumerateArray().ToArray();
if (retainedFits.Length == 0)
{
    throw new InvalidDataException("This probe requires a FigureFit binding to verify the retained body's artifact hash.");
}
foreach (var fit in retainedFits)
{
    if (fit.GetProperty("ArtifactHash").GetString() != bodyHash)
    {
        throw new InvalidDataException("Snapshot figure binding does not match the supplied body artifact.");
    }
}
var snapshot = document.RootElement.GetProperty("settled").Deserialize<ClothSnapshot3D>(json)
    ?? throw new InvalidDataException("A retained settled snapshot is required.");
ClothState3D.ValidateSnapshot(garment.Cloth, snapshot);
string finalPose = garment.Source.Pose
    ?? throw new InvalidDataException("The retained drape requires an authored pose binding.");
if (garment.Source.Transition is { } transitionName)
{
    finalPose = garment.Source.Posing!.Transitions[transitionName].To;
}
var surface = GarmentDraping.BodySurface(body);
if (finalPose != "Rest")
{
    var recipe = garment.Source.Posing!.Resolve(finalPose, "Anatolia");
    var pose = HumanoidPoseAuthoring.Solve(recipe, body);
    var posedPositions = body.EvaluatePositions(pose);
    surface = GarmentDraping.BodySurface(body, posedPositions);
}
var contacts = new ClothContacts3D { BodySurface = surface, BodyClearance = garment.Source.BodyClearance };
var structuralLinks = garment.Cloth.Constraints.Where(constraint =>
    constraint.Vertices.Length == 2 && constraint.Weights.IsEmpty);
var outliers = structuralLinks
    .Select(constraint => new
    {
        constraint.Kind,
        constraint.RestLength,
        stretch = Vector3.Distance(snapshot.Positions[constraint.Vertices[0]],
            snapshot.Positions[constraint.Vertices[1]]) / constraint.RestLength - 1,
        vertices = constraint.Vertices.Select(DescribeVertex),
    })
    .OrderByDescending(link => link.stretch)
    .Take(8)
    .ToArray();
var probes = new List<object>();
foreach (bool contact in new[] { true, false })
{
    foreach (int substeps in new[] { 8, 32 })
    {
        Console.Error.WriteLine($"Replay: body contact={contact}, substeps={substeps}, frames=5");
        using var solver = new ClothSolver3D(garment.Cloth);
        solver.Restore(snapshot with
        {
            Velocities = snapshot.Velocities.Select(_ => Vector3.Zero).ToImmutableArray(),
        });
        var options = new ClothStepOptions3D
        {
            Gravity = Vector3.Zero,
            Substeps = substeps,
            Iterations = 24,
            ContactIterations = 1,
        };
        var probeContacts = contact ? contacts : new ClothContacts3D();
        for (int tick = 0; tick < 5; tick++)
        {
            solver.Step(options, probeContacts);
        }
        probes.Add(new
        {
            contact,
            substeps,
            ticks = 5,
            measuredAgainstOriginalBody = ClothState3D.Measure(garment.Cloth, solver.Capture(), contacts),
            seamGap = garment.MaximumSeamGap(solver.Capture()),
        });
    }
}
string report = JsonSerializer.Serialize(new
{
    sourcePath,
    sourceHash,
    bodyPath,
    bodyHash,
    finalPose,
    snapshotPath,
    garment.Cloth.ContentKey,
    initial = ClothState3D.Measure(garment.Cloth, snapshot, contacts),
    outliers,
    probes,
    limits = new[]
    {
        "Retained final snapshot and its authored final body pose; not an intermediate checkpoint",
        "Five replay probe frames",
        "Zero gravity and initial velocity",
        "Contact-disabled probes are diagnostic only, not admissible fits",
    },
}, json);
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output, report);
Console.WriteLine(report);

object DescribeVertex(int vertex)
{
    var semanticEdges = garment.Panels.SelectMany(panel =>
        panel.Edges.Where(edge => edge.Value.Contains(vertex))
            .Select(edge => panel.Identity + ".edges." + edge.Key));
    float inverseMass = garment.Cloth.InverseMasses[vertex];
    return new
    {
        vertex,
        position = snapshot.Positions[vertex],
        material = garment.Cloth.Definition.Positions[vertex],
        inverseMass,
        body = surface.Query(snapshot.Positions[vertex]),
        edges = semanticEdges.ToArray(),
    };
}
