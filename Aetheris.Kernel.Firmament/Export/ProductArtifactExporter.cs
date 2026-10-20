using System.Text;
using System.IO.Compression;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.Drawing;
using Aetheris.Kernel.Firmament.Scene;

namespace Aetheris.Kernel.Firmament.Export;

public sealed record ProductExportOption(string Format, string Label, string Description, bool Available, string? Reason = null);
public sealed record ProductExportFile(string FileName, string MediaType, byte[] Bytes);
public sealed record ProductExportSource(string Name, string? StepText, AssemblyDisplayMeshDocument Display,
    AssemblyIr? Assembly = null, CompiledScene? Scene = null,
    IReadOnlyList<string>? ExpectedPmi = null, string? SourceDocument = null);

/// <summary>Download adapters over the existing engineering and presentation exporters.
/// Scratch files belong to this invocation and are removed even when an exporter rejects a product.</summary>
public static class ProductArtifactExporter
{
    public static ProductExportFile Package(IReadOnlyList<ProductExportFile> files)
    {
        if (files.Count == 1) return files[0];
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var file in files)
            {
                var entry = archive.CreateEntry(file.FileName);
                entry.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
                using var stream = entry.Open(); stream.Write(file.Bytes);
            }
        return new(Path.GetFileNameWithoutExtension(files[0].FileName) + "-exports.zip", "application/zip", memory.ToArray());
    }
    public static IReadOnlyList<ProductExportOption> Options(bool hasStep, bool isPart) =>
    [
        new("step", "STEP AP242", "Exact geometry and supported semantic PMI", hasStep, hasStep ? null : "Scenes have no STEP product artifact."),
        new("glb", "glTF / GLB", "Binary glTF 2.0 · static authored pose", true),
        new("usda", "OpenUSD / USDA", "Scene hierarchy and static authored pose", true),
        new("stl", "STL", "Validated closed triangle mesh · millimetres", isPart, isPart ? null : "STL export currently requires a single part."),
        new("obj", "OBJ", "SurfaceMeshIR polygons · millimetres", isPart, isPart ? null : "OBJ export currently requires a single part."),
        new("pdf", "PDF drawing", "Dimensioned drawing · supported part families", isPart, isPart ? null : "Automatic drawings currently require a single part."),
        new("svg", "SVG drawing", "Vector drawing sheets", isPart, isPart ? null : "Automatic drawings currently require a single part."),
        new("pptx", "PowerPoint drawing", "Drawing sheets in an editable slide deck", isPart, isPart ? null : "Automatic drawings currently require a single part.")
    ];

    public static IReadOnlyList<ProductExportFile> Export(ProductExportSource source, IReadOnlyList<string> formats, string scratchRoot)
    {
        if (formats.Count == 0) throw new InvalidOperationException("export-format-required: Select at least one format.");
        var options = Options(source.StepText is not null, source.Assembly is null && source.Scene is null);
        foreach (var format in formats)
        {
            var option = options.SingleOrDefault(o => o.Format == format)
                ?? throw new InvalidOperationException($"export-format-unknown: Unknown export format '{format}'.");
            if (!option.Available) throw new InvalidOperationException($"export-format-unavailable: {option.Label}: {option.Reason}");
        }
        // Never use a document name as a filesystem path, including browser project paths.
        var stem = new string(source.Name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_').ToArray());
        if (string.IsNullOrWhiteSpace(stem) || stem is "." or "..") stem = "model";
        var scratch = Path.Combine(Path.GetFullPath(scratchRoot), "export-" + Guid.NewGuid().ToString("N"));
        var files = new List<ProductExportFile>();
        DrawingCompileArtifacts? drawing = null;
        SurfaceMeshDocument? surfaceMesh = null;
        try
        {
            foreach (var format in formats.Distinct(StringComparer.Ordinal))
            {
                byte[] bytes;
                var mediaType = "application/octet-stream";
                switch (format)
                {
                    case "step": bytes = Encoding.UTF8.GetBytes(source.StepText!); mediaType = "model/step"; break;
                    case "glb":
                        var annotatedDisplay = source.StepText is not null && source.Assembly is null && source.Scene is null
                            ? source.Display with { Annotations = Visualization.PmiDisplayProjection.FromStep(source.StepText) } : source.Display;
                        bytes = source.Scene is { } scene ? SceneExport.Glb(scene) : AssemblyGlbExporter.Serialize(annotatedDisplay,
                            occurrenceLooks: source.Assembly?.Instances.Where(i => i.Appearance is not null)
                                .ToDictionary(i => i.StableId, i => i.Appearance!, StringComparer.Ordinal));
                        mediaType = "model/gltf-binary"; break;
                    case "usda":
                        var usd = source.Scene is { } spatial ? SceneExport.Usd(spatial)
                            : source.Assembly is { } assembly ? AssemblyUsdExporter.Serialize(assembly, source.Display)
                            : AssemblyUsdExporter.SerializeSpatial(source.Display, new(
                                source.Display.Occurrences.ToDictionary(o => o.Id, _ => source.Display.Definitions[0].Identity),
                                new Dictionary<string, AssemblyAppearanceBinding>(), new Dictionary<string, IReadOnlyDictionary<string, string>>(), []));
                        bytes = Encoding.UTF8.GetBytes(usd); mediaType = "text/plain"; break;
                    case "stl":
                    case "obj":
                        if (surfaceMesh is null)
                        {
                            var body = Step242Importer.ImportBody(source.StepText!);
                            if (!body.IsSuccess) throw new InvalidOperationException("mesh-import-failed: " + string.Join("; ", body.Diagnostics.Select(d => d.Message)));
                            if (!SurfaceMeshIrTessellator.TryBuild(body.Value, SurfaceMeshPolicy.FromDisplayOptions(DisplayTessellationOptions.Default), out surfaceMesh, out var failure)
                                || !SurfaceMeshIrValidator.TryValidate(surfaceMesh, out failure))
                                throw new InvalidOperationException("mesh-export-unsupported: " + failure);
                        }
                        if (format == "obj") { bytes = Encoding.UTF8.GetBytes(SurfaceMeshObjExporter.Export(surfaceMesh, stem).Text); mediaType = "text/plain"; break; }
                        if (!SurfaceMeshIrTessellator.TryLowerToTriangleMesh(surfaceMesh, out var mesh, out var topology))
                            throw new InvalidOperationException($"stl-mesh-not-closed: cracks={topology.CrackCount}, nonmanifold={topology.NonManifoldEdgeCount}, outward={topology.IsOutwardOriented}.");
                        Directory.CreateDirectory(scratch);
                        var stl = Path.Combine(scratch, "model.stl"); BinaryStlExporter.Export(stl, mesh);
                        bytes = File.ReadAllBytes(stl); mediaType = "model/stl"; break;
                    default: // All drawing formats share the same admitted DrawingIR and generation pass.
                        if (drawing is null)
                        {
                            Directory.CreateDirectory(scratch);
                            var generated = FirmamentDrawingCompiler.GenerateFromStep(source.StepText!, stem,
                                source.SourceDocument ?? stem + ".step", source.ExpectedPmi ?? [], Path.Combine(scratch, "drawing"));
                            if (!generated.IsSuccess) throw new InvalidOperationException(string.Join("; ", generated.Diagnostics));
                            drawing = generated.Artifacts!;
                        }
                        bytes = File.ReadAllBytes(format == "pdf" ? drawing.PdfPath : format == "svg" ? drawing.SvgPath : drawing.PptxPath);
                        mediaType = format == "pdf" ? "application/pdf" : format == "svg" ? "image/svg+xml" : "application/vnd.openxmlformats-officedocument.presentationml.presentation";
                        break;
                }
                files.Add(new(stem + "." + format, mediaType, bytes));
            }
            return files;
        }
        finally { if (Directory.Exists(scratch)) Directory.Delete(scratch, recursive: true); }
    }
}
