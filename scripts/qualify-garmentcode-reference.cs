#:project ../Aetheris.Kernel.Firmament/Aetheris.Kernel.Firmament.csproj
#:property PublishAot=false
#:property JsonSerializerIsReflectionEnabledByDefault=true
// Research orchestration only. Sewing, settling, contacts and replay retain Aetheris owners.
// The supplied upstream OBJ is a local reference input, not a redistributed fixture or rig.
using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetheris.Cloth3D;
using Aetheris.Kernel.Firmament.Garment;

if (args.Length != 3)
{
    throw new ArgumentException("dotnet run scripts/qualify-garmentcode-reference.cs -- pattern.firmament upstream-Y-up-metres.obj output-directory");
}
string patternPath = Path.GetFullPath(args[0]);
string bodyPath = Path.GetFullPath(args[1]);
string output = Path.GetFullPath(args[2]);
var vertices = new List<Vector3>();
var indices = new List<int>();
foreach (string line in File.ReadLines(bodyPath))
{
    string[] values = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    if (values.Length == 0)
    {
        continue;
    }
    if (values[0] == "v")
    {
        if (values.Length != 4)
        {
            throw new InvalidDataException("Reference OBJ must contain XYZ vertices.");
        }
        float x = float.Parse(values[1], CultureInfo.InvariantCulture);
        float y = float.Parse(values[2], CultureInfo.InvariantCulture);
        float z = float.Parse(values[3], CultureInfo.InvariantCulture);
        vertices.Add(new(x, z, y));
    }
    if (values[0] == "f")
    {
        if (values.Length != 4)
        {
            throw new InvalidDataException("Reference OBJ must contain triangles; no implicit retopology.");
        }
        // Y-up -> Z-up swaps axes, so reverse each face to preserve outward normals.
        foreach (int column in new[] { 1, 3, 2 })
        {
            indices.Add(int.Parse(values[column].Split('/')[0], CultureInfo.InvariantCulture) - 1);
        }
    }
}
var surface = new ClothTriangleBody3D(vertices.ToImmutableArray(), indices.ToImmutableArray());
var compiled = GarmentCompiler.Compile(File.ReadAllText(patternPath), patternPath);
if (!compiled.IsSuccess)
{
    throw new InvalidDataException(string.Join("; ", compiled.Diagnostics));
}
string bodyHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(bodyPath)));
var garment = compiled.Garment!;
var drape = GarmentDraping.SettleOnSurface(garment, surface, "garmentcode.mean_all", bodyHash, 30,
    progress => Console.Error.WriteLine($"{progress.Phase}: {progress.Completed}/{progress.Total} ({progress.ElapsedSeconds:F1}s)"));
Directory.CreateDirectory(output);
File.WriteAllText(Path.Combine(output, "garment.obj"), GarmentExport.Obj(garment, drape.Snapshot));
File.WriteAllText(Path.Combine(output, "garment.json"), GarmentExport.Artifact(garment, drape.Snapshot));
File.WriteAllText(Path.Combine(output, "patterns.svg"), GarmentPatternPreview.Svg(garment.Source));
var obj = new StringBuilder();
foreach (var point in vertices)
{
    obj.AppendLine(FormattableString.Invariant($"v {point.X:R} {point.Y:R} {point.Z:R}"));
}
for (int i = 0; i < indices.Count; i += 3)
{
    obj.AppendLine($"f {indices[i] + 1} {indices[i + 1] + 1} {indices[i + 2] + 1}");
}
File.WriteAllText(Path.Combine(output, "body.obj"), obj.ToString());
bool passed = drape.Metrics.Finite && drape.Metrics.MaximumStretch <= .15f
    && drape.Metrics.MaximumSurfacePenetration <= .005f && drape.MaximumSeamGap <= .01f && drape.ReplayExact;
string evidence = JsonSerializer.Serialize(new
{
    qualification = passed ? "passed" : "failed",
    bodyPath,
    bodyHash,
    garment.Source.SourceHash,
    garment.Cloth.ContentKey,
    drape.Metrics,
    drape.MaximumSeamGap,
    drape.ReplayExact,
    drape.StretchOutliers,
    drape.SurfaceContactOutliers,
    upstreamSimulationUsed = false,
    limits = new[] { "Static reference body", "30 sewing ticks + 30 gravity ticks", "Discrete oriented contact",
        "No friction, CCD or self-contact", "Source body remains an external research input" },
}, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(Path.Combine(output, "evidence.json"), evidence);
Console.WriteLine(evidence);
return passed ? 0 : 2;
