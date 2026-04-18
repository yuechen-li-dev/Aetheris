using System.Text;
using System.Text.Json;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Firmament.Assembly;

public sealed record FirmasmAssemblyRoundtripExportedInstance(
    string InstanceId,
    string PartName,
    string StepPath,
    int BodyCount,
    int FaceCount,
    int EdgeCount,
    int VertexCount);

public sealed record FirmasmAssemblyRoundtripExportResult(
    string SourceManifestPath,
    string OutputDirectory,
    string PackageManifestPath,
    IReadOnlyList<FirmasmAssemblyRoundtripExportedInstance> ExportedInstances,
    int InstanceCount,
    int ComposedBodyCount);

public sealed class FirmasmAssemblyRoundtripExporter
{
    private const string PackageSchema = "asm-a4-step-instance-package/v1";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly FirmasmAssemblyExecutor _executor = new();

    public KernelResult<FirmasmAssemblyRoundtripExportResult> ExportFromFile(string manifestPath, string outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return Failure("Roundtrip export requires an output directory path.");
        }

        var executionResult = _executor.ExecuteFromFile(manifestPath);
        if (!executionResult.IsSuccess)
        {
            return KernelResult<FirmasmAssemblyRoundtripExportResult>.Failure(executionResult.Diagnostics);
        }

        var executed = executionResult.Value;
        var outputDirectoryPath = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputDirectoryPath);

        var exportedInstances = new List<FirmasmAssemblyRoundtripExportedInstance>(executed.Instances.Count);
        var orderedInstances = executed.Instances
            .OrderBy(instance => instance.InstanceId, StringComparer.Ordinal)
            .ToArray();

        for (var index = 0; index < orderedInstances.Length; index++)
        {
            var instance = orderedInstances[index];
            var fileName = $"{index + 1:D4}-{SanitizeFileName(instance.InstanceId)}.step";
            var exportPath = Path.Combine(outputDirectoryPath, fileName);

            var export = Step242Exporter.ExportBody(instance.Body, options: new Step242ExportOptions
            {
                ProductName = $"{executed.LoadedAssembly.Manifest.Assembly.Name}:{instance.InstanceId}"
            });

            if (!export.IsSuccess)
            {
                var diagnostics = export.Diagnostics.Append(new KernelDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    KernelDiagnosticSeverity.Error,
                    $"Failed to export instance '{instance.InstanceId}' during .firmasm roundtrip export.",
                    Source: "firmasm")).ToArray();
                return KernelResult<FirmasmAssemblyRoundtripExportResult>.Failure(diagnostics);
            }

            File.WriteAllText(exportPath, export.Value, new UTF8Encoding(false));
            exportedInstances.Add(new FirmasmAssemblyRoundtripExportedInstance(
                instance.InstanceId,
                instance.PartName,
                exportPath,
                instance.Body.Topology.Bodies.Count(),
                instance.Body.Topology.Faces.Count(),
                instance.Body.Topology.Edges.Count(),
                instance.Body.Topology.Vertices.Count()));
        }

        var packageManifestPath = Path.Combine(outputDirectoryPath, "roundtrip.package.json");
        var packageManifest = new
        {
            schema = PackageSchema,
            nativeAuthority = ".firmasm",
            exportIntent = "outbound-step-interop",
            sourceManifestPath = executed.LoadedAssembly.SourcePath,
            assemblyName = executed.LoadedAssembly.Manifest.Assembly.Name,
            units = executed.LoadedAssembly.Manifest.Assembly.Units,
            summary = new
            {
                partCount = executed.LoadedAssembly.LoadedParts.Count,
                instanceCount = executed.Instances.Count,
                composedBodyCount = executed.ComposedBody.Topology.Bodies.Count()
            },
            instances = exportedInstances.Select(instance => new
            {
                instanceId = instance.InstanceId,
                partName = instance.PartName,
                stepPath = instance.StepPath,
                bodyCount = instance.BodyCount,
                faceCount = instance.FaceCount,
                edgeCount = instance.EdgeCount,
                vertexCount = instance.VertexCount
            }).ToArray()
        };

        File.WriteAllText(packageManifestPath, JsonSerializer.Serialize(packageManifest, JsonOptions), new UTF8Encoding(false));

        return KernelResult<FirmasmAssemblyRoundtripExportResult>.Success(new FirmasmAssemblyRoundtripExportResult(
            executed.LoadedAssembly.SourcePath,
            outputDirectoryPath,
            packageManifestPath,
            exportedInstances,
            executed.Instances.Count,
            executed.ComposedBody.Topology.Bodies.Count()));
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
        var sanitized = new string(chars);
        return string.IsNullOrWhiteSpace(sanitized) ? "instance" : sanitized;
    }

    private static KernelResult<FirmasmAssemblyRoundtripExportResult> Failure(string message)
    {
        return KernelResult<FirmasmAssemblyRoundtripExportResult>.Failure([
            new KernelDiagnostic(
                KernelDiagnosticCode.ValidationFailed,
                KernelDiagnosticSeverity.Error,
                message,
                Source: "firmasm")
        ]);
    }
}
