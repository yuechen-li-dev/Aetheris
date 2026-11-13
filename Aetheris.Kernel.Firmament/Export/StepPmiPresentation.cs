using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Export;

/// <summary>
/// Presentation of the semantic PMI an AP242 file already carries, without a Firmament source: a GLB of the part with
/// its callouts regenerated from the semantic records (values in millimetres whatever the file's units; the file's own
/// presentation glyphs are not reused). Geometry and every association come from the file.
/// </summary>
public static class StepPmiPresentation
{
    /// <summary>The single part of an AP242 file as a display document, in millimetres, with its PMI callouts.</summary>
    public static AssemblyDisplayMeshDocument Display(string step, string name)
    {
        var imported = Step242Importer.ImportBody(step);
        if (!imported.IsSuccess || imported.Value is null)
            throw new InvalidOperationException("step-pmi-import-failed: " + string.Join("; ", imported.Diagnostics.Select(d => d.Message)));
        var options = DisplayTessellationOptions.ForViewport(imported.Value);
        var definition = AssemblyDisplayMeshExporter.PrepareDefinition("part", name, imported.Value, options, externalStep: true);
        var occurrence = new AssemblyDisplayMeshOccurrence("part", name, null, "part", [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1], name) { Kind = "Part" };
        var annotations = Visualization.PmiDisplayProjection.FromStep(step, options);
        return new AssemblyDisplayMeshDocument("aetheris/assembly-display-mesh/1", name, "mm", [definition], [occurrence])
        {
            TessellationOptions = options,
            Annotations = annotations,
            EdgeLines = annotations.Count > 0 ? Visualization.PmiDisplayProjection.EdgeLines(step, options) : [],
        };
    }

    /// <summary>Binary glTF of the part and its PMI callouts.</summary>
    public static byte[] Glb(string step, string name) => AssemblyGlbExporter.Serialize(Display(step, name));
}
