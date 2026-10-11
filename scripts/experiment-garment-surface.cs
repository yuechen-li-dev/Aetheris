#:project ../Aetheris.Kernel.Firmament/Aetheris.Kernel.Firmament.csproj
#:property PublishAot=false
#:property JsonSerializerIsReflectionEnabledByDefault=true
using System.Collections.Immutable;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using Aetheris.Cloth3D;
using Aetheris.Humanoid;
using Aetheris.Kernel.Firmament.Garment;

if (args.Length != 2)
{
    throw new ArgumentException("dotnet run -c Release scripts/experiment-garment-surface.cs -- body.json output-directory");
}
var body = HumanoidGameplayBody.Load(Path.GetFullPath(args[0]));
string output = Path.GetFullPath(args[1]);
Directory.CreateDirectory(output);
var json = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
// Same declared body section heights as the canonical Anatolia fitting specimen.
var garment = GarmentSurfaceAuthoring.Skirt(new("surface-skirt", HipHeight: .96f, ChestHeight: 1.28f), body, new()
{
    WarpCompliance = .001f, WeftCompliance = .004f, DiagonalCompliance = .002f, BendCompliance = .01f,
});
var contacts = new ClothContacts3D
{
    BodySurface = GarmentDraping.BodySurface(body), BodyClearance = .004f,
};
using var solver = new ClothSolver3D(garment.Cloth);
var initial = solver.Capture();
var initialMetrics = ClothState3D.Measure(garment.Cloth, initial, contacts);
File.WriteAllText(Path.Combine(output, "initial-garment.obj"), garment.Obj(initial));
File.WriteAllText(Path.Combine(output, "body.obj"), GarmentExport.BodyObj(body));
float initialResidual = garment.Cloth.Constraints
    .Where(constraint => constraint.Kind is ClothConstraintKind3D.SurfaceWarp or ClothConstraintKind3D.SurfaceWeft
        or ClothConstraintKind3D.SurfaceShear or ClothConstraintKind3D.SurfaceBend)
    .Max(constraint => Math.Abs(ClothSurfaceMechanics3D.Evaluate(constraint, initial.Positions).Residual));
var zeroGravity = new ClothStepOptions3D { Gravity = Vector3.Zero, Substeps = 2, Iterations = 8 };
for (int frame = 0; frame < 8; frame++)
{
    solver.Step(zeroGravity, new());
}
float restDrift = initial.Positions.Zip(solver.Capture().Positions, Vector3.Distance).Max();
solver.Restore(initial);
var options = new ClothStepOptions3D
{
    Gravity = new(0, 0, -9.81f), Substeps = 2, Iterations = 8, ContactIterations = 1, VelocityDamping = 4,
};
var watch = Stopwatch.StartNew();
var frames = new List<object>();
for (int frame = 0; frame < 32; frame++)
{
    solver.Step(options, contacts);
    if (frame % 8 == 7)
    {
        var metrics = ClothState3D.Measure(garment.Cloth, solver.Capture(), contacts);
        frames.Add(new { frame = frame + 1, metrics });
        Console.Error.WriteLine($"surface frame {frame + 1}: {watch.Elapsed.TotalSeconds:F1}s, stretch {metrics.MaximumStretch:P2}");
    }
}
var settled = solver.Capture();
solver.Step(options, contacts);
var expected = solver.Capture();
solver.Restore(settled);
solver.Step(options, contacts);
var replay = solver.Capture();
bool replayExact = expected.Positions.SequenceEqual(replay.Positions) && expected.Velocities.SequenceEqual(replay.Velocities);
var finalMetrics = ClothState3D.Measure(garment.Cloth, settled, contacts);
string artifact = garment.Artifact(settled);
var retained = ClothGarmentArtifact3D.Parse(artifact);
using var resumed = retained.CreateSolver();
resumed.Step(options, contacts);
bool artifactReplay = resumed.Capture().Positions.SequenceEqual(expected.Positions);
bool accepted = initialResidual < .0001f && restDrift < .0001f && replayExact && artifactReplay
    && finalMetrics.Finite && finalMetrics.MaximumStretch <= .15f
    && finalMetrics.MaximumSurfacePenetration <= .005f && finalMetrics.MaximumPinError < .0001f;
File.WriteAllText(Path.Combine(output, "garment.obj"), garment.Obj(settled));
File.WriteAllText(Path.Combine(output, "garment.json"), artifact);
File.WriteAllText(Path.Combine(output, "evidence.json"), JsonSerializer.Serialize(new
{
    accepted, source = garment.Source, figure = garment.Figure, garment.SourceHash, garment.Cloth.ContentKey,
    vertices = garment.Cloth.Definition.Positions.Length, quads = garment.Cloth.Definition.Surface!.Quads.Length,
    rendererTriangles = garment.Cloth.Definition.Indices.Length / 3, waistbandPins = garment.Waist.Length,
    stitches = garment.Cloth.Definition.Stitches.Length, initialResidual, restDrift, initialMetrics, finalMetrics,
    replayExact, artifactReplay, elapsedSeconds = watch.Elapsed.TotalSeconds, frames,
    qualified = "Stationary body, fitted skirt rest, explicit waistband attachments, 32 gravity frames",
    selfCollisionQualified = false, flatPatternQualified = false, animatedBodyQualified = false,
}, json));
Console.WriteLine(accepted ? "GARMENT_SURFACE_SKIRT_PASSED" : "GARMENT_SURFACE_SKIRT_NOT_QUALIFIED");
return accepted ? 0 : 1;

