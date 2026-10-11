#:project ../Aetheris.Kernel.Firmament/Aetheris.Kernel.Firmament.csproj
#:property PublishAot=false
#:property JsonSerializerIsReflectionEnabledByDefault=true
using System.Diagnostics;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Aetheris.Cloth3D;
using Aetheris.Humanoid;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Visualization;
using Aetheris.Kernel.Firmament.Garment;
using Aetheris.Surfacing;

if (args.Length is < 2 or > 3)
{
    throw new ArgumentException("dotnet run -c Release scripts/experiment-garment-spline.cs -- body.json output-directory [guides.json]");
}
var body = HumanoidGameplayBody.Load(Path.GetFullPath(args[0]));
string output = Path.GetFullPath(args[1]);
Directory.CreateDirectory(output);
var json = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
var watch = Stopwatch.StartNew();
var guides = args.Length == 3
    ? JsonSerializer.Deserialize<GarmentTubeTopSurface>(File.ReadAllText(args[2]), json)
        ?? throw new InvalidDataException("Empty tube-top guides.")
    : new GarmentTubeTopSurface("spline-tube-top");
var fit = GarmentSplineAuthoring.FitTubeTop(guides, body);
Console.Error.WriteLine($"Spline fitting: seed RMS {fit.SeedRmsMm:F2} -> {fit.FittedRmsMm:F2} mm, max {fit.MaximumTargetErrorMm:F2} mm");
var material = new ClothMaterial3D
{
    WarpCompliance = .001f, WeftCompliance = .004f, DiagonalCompliance = .002f, BendCompliance = .01f,
};
var garment = GarmentSplineAuthoring.Lower(fit.Source, material);
string sourceJson = GarmentSplineAuthoring.SerializeSource(fit.Source);
var restoredSource = GarmentSplineAuthoring.ParseSource(sourceJson);
var restoredGarment = GarmentSplineAuthoring.Lower(restoredSource, material);
bool sourceReplay = restoredGarment.SourceHash == garment.SourceHash && restoredGarment.Cloth.ContentKey == garment.Cloth.ContentKey;
var contacts = new ClothContacts3D { BodySurface = GarmentDraping.BodySurface(body), BodyClearance = .004f };
var clearance = GarmentSplineAuthoring.Clearance(fit.Source, contacts.BodySurface);
Console.Error.WriteLine($"Independent spline clearance: {clearance.MinimumMm:F2}..{clearance.MaximumMm:F2} mm");
File.WriteAllText(Path.Combine(output, "source.json"), sourceJson);
File.WriteAllText(Path.Combine(output, "body.obj"), GarmentExport.BodyObj(body));
File.WriteAllText(Path.Combine(output, "control-cage.obj"), Cage(fit.Source));
File.WriteAllText(Path.Combine(output, "clearance.svg"), ClearanceMap(fit.Source, contacts.BodySurface));
var patchEvidence = new List<object>();
foreach (var patch in fit.Source.Patches)
{
    var sheet = SurfacePatchBrepMaterializer.MaterializeSheet(patch.Surface);
    var binding = BrepBindingValidator.Validate(sheet, true);
    var pcurve = BrepPcurveValidator.Validate(sheet, requireEveryCoedge: true);
    var wire = BrepWireframeSvgRenderer.Render(sheet, new(View: WireframeView.IsometricZUp));
    File.WriteAllText(Path.Combine(output, patch.Id + "-support.svg"), wire.Svg);
    patchEvidence.Add(new { patch.Id, binding.IsSuccess, pcurve, wire.Evidence });
    if (!binding.IsSuccess || !pcurve.IsValid || wire.Evidence.FacesWithTrimmedIsolines != 1)
    {
        throw new InvalidOperationException("GARMENT_SPLINE_BREP_NOT_QUALIFIED: Invalid open sheet binding or pcurve.");
    }
}
using var solver = new ClothSolver3D(garment.Cloth);
var initial = solver.Capture();
File.WriteAllText(Path.Combine(output, "initial-garment.obj"), garment.Obj(initial));
float residual = garment.Cloth.Constraints.Max(constraint => Math.Abs(ClothSurfaceMechanics3D.Evaluate(constraint, initial.Positions).Residual));
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
var frames = new List<ClothMetrics3D>();
for (int frame = 0; frame < 32; frame++)
{
    solver.Step(options, contacts);
    if (frame % 8 == 7)
    {
        var metrics = ClothState3D.Measure(garment.Cloth, solver.Capture(), contacts);
        frames.Add(metrics);
        Console.Error.WriteLine($"Spline frame {frame + 1}: stretch {metrics.MaximumStretch:P2}, penetration {metrics.MaximumSurfacePenetration * 1000:F2} mm");
    }
}
var settled = solver.Capture();
solver.Step(options, contacts);
var expected = solver.Capture();
solver.Restore(settled);
solver.Step(options, contacts);
var replay = solver.Capture();
bool replayExact = expected.Positions.SequenceEqual(replay.Positions) && expected.Velocities.SequenceEqual(replay.Velocities);
var retained = ClothGarmentArtifact3D.Parse(garment.Artifact(settled));
using var resumed = retained.CreateSolver();
resumed.Step(options, contacts);
bool artifactReplay = resumed.Capture().Positions.SequenceEqual(expected.Positions)
    && resumed.Capture().Velocities.SequenceEqual(expected.Velocities);
float displacement = initial.Positions.Zip(settled.Positions, Vector3.Distance).Max();
bool accepted = fit.FittedRmsMm < fit.SeedRmsMm && clearance.MinimumMm >= 5
    && residual < .0001f && restDrift < .0001f && sourceReplay && replayExact && artifactReplay
    && frames.All(metrics => metrics.Finite && metrics.MaximumStretch <= .15f
        && metrics.MaximumSurfacePenetration <= .005f && metrics.MaximumPinError < .0001f);
File.WriteAllText(Path.Combine(output, "garment.obj"), garment.Obj(settled));
File.WriteAllText(Path.Combine(output, "garment.json"), garment.Artifact(settled));
File.WriteAllText(Path.Combine(output, "evidence.json"), JsonSerializer.Serialize(new
{
    accepted, fit.SeedRmsMm, fit.FittedRmsMm, fit.MaximumTargetErrorMm, fit.TargetCount, clearance,
    garment.SourceHash, garment.Cloth.ContentKey, sourceReplay, replayExact, artifactReplay,
    vertices = initial.Positions.Length, quads = garment.Cloth.Definition.Surface!.Quads.Length,
    bottomBandPins = garment.Cloth.Definition.Pins.Length, residual, restDrift, displacement,
    frames, patchEvidence, elapsedSeconds = watch.Elapsed.TotalSeconds,
    qualified = "Body-fitted editable bicubic midsurfaces, welded quads, stationary body, lower-band attachments, 32 gravity frames",
    frictionQualified = false, animatedBodyQualified = false, selfContactQualified = false,
    flatPatternQualified = false, closedSolidQualified = false, globalClearanceQualified = false,
}, json));
Console.WriteLine(accepted ? "GARMENT_SPLINE_TUBE_TOP_PASSED" : "GARMENT_SPLINE_TUBE_TOP_NOT_QUALIFIED");
return accepted ? 0 : 1;

static string Cage(GarmentSplineSource source)
{
    var result = new StringBuilder("# Retained spline controls; diagnostic lines; metres; Z up\n");
    int offset = 1;
    foreach (var patch in source.Patches)
    {
        foreach (var row in patch.Surface.ControlPoints)
        {
            foreach (var point in row)
            {
                result.AppendLine(FormattableString.Invariant($"v {point.X / 1000:R} {point.Y / 1000:R} {point.Z / 1000:R}"));
            }
        }
        for (int column = 0; column < 7; column++)
        {
            result.AppendLine("l " + string.Join(" ", Enumerable.Range(0, 4).Select(row => offset + column * 4 + row)));
        }
        for (int row = 0; row < 4; row++)
        {
            result.AppendLine("l " + string.Join(" ", Enumerable.Range(0, 7).Select(column => offset + column * 4 + row)));
        }
        offset += 28;
    }
    return result.ToString();
}

static string ClearanceMap(GarmentSplineSource source, IClothBodySurface3D body)
{
    var svg = new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1040\" height=\"370\" viewBox=\"0 0 1040 370\">\n");
    svg.AppendLine("<rect width=\"1040\" height=\"370\" fill=\"#10151d\"/>");
    svg.AppendLine("<g fill=\"#ffffff\" font-family=\"sans-serif\" font-size=\"17\"><text x=\"25\" y=\"27\">Spline/body sampled clearance: red &lt; 5 mm; amber 5..10 mm; green &gt;= 10 mm</text>");
    svg.AppendLine("<text x=\"25\" y=\"54\">Front support (u horizontal, v downward)</text><text x=\"545\" y=\"54\">Back support</text></g>");
    for (int patch = 0; patch < 2; patch++)
    {
        for (int row = 0; row < 25; row++)
        {
            for (int column = 0; column < 49; column++)
            {
                double u = column / 48d;
                double v = row / 24d;
                var point = source.Patches[patch].Surface.Evaluate(u, v);
                double distance = body.Query(new((float)(point.X / 1000), (float)(point.Y / 1000),
                    (float)(point.Z / 1000))).SignedDistance * 1000;
                string color = "#43a979";
                if (distance < 5)
                {
                    color = "#e05252";
                }
                else if (distance < 10)
                {
                    color = "#efb94f";
                }
                svg.AppendLine(FormattableString.Invariant($"<rect x=\"{25 + patch * 520 + column * 10}\" y=\"{70 + row * 10}\" width=\"9\" height=\"9\" fill=\"{color}\"><title>u={u:F4}, v={v:F4}: {distance:F3} mm</title></rect>"));
            }
        }
    }
    svg.AppendLine("<text x=\"25\" y=\"350\" fill=\"#dbeafe\" font-family=\"sans-serif\" font-size=\"15\">2450 discrete nearest-triangle queries; no global collision-free certificate.</text></svg>");
    return svg.ToString();
}
