using System.Security.Cryptography;
using System.Text.Json;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.Export;

// Compiles a Firmament part with PMI and writes its AP242 STEP and annotated GLB presentation.
// Every callout in the GLB is read back from the exported STEP, never from the source.
if (args.Length is < 1 or > 2 || args.Any(a => a.StartsWith('-')))
{
    Console.Error.WriteLine("Usage: Aetheris.PmiShowcase <part.firmament> [output-directory]");
    return 1;
}
try
{
    var sourcePath = Path.GetFullPath(args[0]);
    var output = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/local/demos/pmi-showcase");
    Directory.CreateDirectory(output);
    var compilation = FirmamentBuildAndExport.CompileSource(File.ReadAllText(sourcePath));
    if (!compilation.IsSuccess) throw new InvalidOperationException(string.Join("\n", compilation.Diagnostics.Select(d => d.Message)));
    var step = compilation.Value.StepText;
    var imported = Step242Importer.ImportBody(step);
    if (!imported.IsSuccess) throw new InvalidOperationException("STEP reimport failed.");
    // Presentation density: smooth silhouettes, still light enough for a phone or tablet.
    var options = DisplayTessellationOptions.Default with { AngularToleranceRadians = Math.PI / 24 };
    var tessellation = BrepDisplayTessellator.Tessellate(imported.Value, options);
    if (!tessellation.IsSuccess) throw new InvalidOperationException("Display tessellation failed.");
    var positions = new List<double>(); var normals = new List<double>(); var indices = new List<int>();
    foreach (var face in tessellation.Value.FacePatches)
    {
        var offset = positions.Count / 3;
        positions.AddRange(face.Positions.SelectMany(p => new[] { p.X, p.Y, p.Z }));
        normals.AddRange(face.Normals.SelectMany(p => new[] { p.X, p.Y, p.Z }));
        indices.AddRange(face.TriangleIndices.Select(i => i + offset));
    }
    var name = Path.GetFileNameWithoutExtension(sourcePath);
    var display = new AssemblyDisplayMeshDocument("aetheris/display-mesh/1", name, "mm",
        [new("part-definition", name, positions.ToArray(), normals.ToArray(), indices.ToArray())],
        [new("part", name, null, "part-definition", [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1])]) { TessellationOptions = options };
    foreach (var artifact in ProductArtifactExporter.Export(new(name, step, display), ["step", "glb"], output))
        File.WriteAllBytes(Path.Combine(output, artifact.FileName), artifact.Bytes);
    var pmi = Step242SemanticPmiInspector.Inspect(step);
    File.WriteAllText(Path.Combine(output, name + ".pmi.json"), JsonSerializer.Serialize(pmi.Items, new JsonSerializerOptions { WriteIndented = true }));
    foreach (var file in Directory.GetFiles(output, name + ".*"))
        Console.WriteLine($"{Path.GetFileName(file),-40} {new FileInfo(file).Length,10:N0} bytes  {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)))[..12]}");
    Console.WriteLine($"PMI records: {pmi.Items.Count} ({pmi.DatumCount} datums, {pmi.DimensionCount} sizes, {pmi.GeometricToleranceCount} controls, {pmi.AnnotationCount} notes)");
    return 0;
}
catch (Exception exception) when (exception is InvalidOperationException or IOException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
