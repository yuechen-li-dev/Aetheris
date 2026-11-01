using System.Text;
using System.Text.Json;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament;
using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.CLI;

/// <summary>One admitted standalone part, projected through the existing display tessellator.
/// STEP and display derive from the same build; a DCC client never reconstructs engineering geometry.</summary>
internal static class PartDisplayMeshCommand
{
    public static int Run(string input, string? output, string? stepOutput, TextWriter stdout)
    {
        int Fail(IEnumerable<string> diagnostics)
        {
            stdout.WriteLine(JsonSerializer.Serialize(new { success = false, command = "mesh", diagnostics }));
            return 1;
        }
        if (!Path.GetExtension(input).Equals(".firmament", StringComparison.OrdinalIgnoreCase))
            return Fail(["display-json requires a standalone Firmament part (.firmament). Use assembly-json for assemblies."]);
        var destination = Path.GetFullPath(output ?? Path.Combine("artifacts", "local", "mesh", Path.GetFileNameWithoutExtension(input) + ".display.json"));
        if (stepOutput is not null && Path.GetFullPath(stepOutput).Equals(destination, StringComparison.OrdinalIgnoreCase))
            return Fail(["Display JSON and STEP need different output paths."]);
        var temporary = Path.Combine(Path.GetTempPath(), "aetheris-display-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            var build = FirmamentBuildAndExport.Run(input, Path.Combine(temporary, "part.step"));
            if (!build.IsSuccess) return Fail(build.Diagnostics.Select(d => d.Message));
            var export = build.Value.Export;
            var body = export.RuntimeBody;
            if (body is null)
            {
                var imported = Step242Importer.ImportBody(export.StepText);
                if (!imported.IsSuccess || imported.Value is null) return Fail(imported.Diagnostics.Select(d => d.Message));
                body = imported.Value;
            }
            var entity = export.RuntimeCorrespondence?.BodyStableId ?? export.ExportedFeatureId;
            if (string.IsNullOrWhiteSpace(entity)) entity = Path.GetFileNameWithoutExtension(input);
            var definition = AssemblyDisplayMeshExporter.PrepareDefinition("definition:" + entity, entity, body,
                externalStep: export.ExportedBodyCategory == "inline-step");
            var display = new AssemblyDisplayMeshDocument("aetheris/display-mesh/1", Path.GetFileNameWithoutExtension(input), "mm",
                [definition], [new(entity + ":occurrence", entity, null, definition.Id,
                    [1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1], entity)]);
            // Finish compilation and tessellation before replacing any caller artifact.
            var serialized = AssemblyDisplayMeshExporter.Serialize(display);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            if (stepOutput is not null)
            {
                stepOutput = Path.GetFullPath(stepOutput);
                Directory.CreateDirectory(Path.GetDirectoryName(stepOutput)!);
                File.WriteAllText(stepOutput, export.StepText, new UTF8Encoding(false));
            }
            File.WriteAllText(destination, serialized, new UTF8Encoding(false));
            stdout.WriteLine(JsonSerializer.Serialize(new { success = true, output = destination, stepOutput, schema = display.Schema,
                units = display.Units, definitions = 1, occurrences = 1, triangles = definition.Indices.Length / 3 }));
            return 0;
        }
        catch (Exception error) when (error is InvalidOperationException or IOException or ArgumentException)
        {
            return Fail([error.Message]);
        }
        finally { Directory.Delete(temporary, true); }
    }
}
