using System.Text;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament;

public static class FirmamentBuildAndExport
{
    public static KernelResult<FirmamentBuildAndExportResult> Run(string sourcePath, string? outputPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var fullSourcePath = Path.GetFullPath(sourcePath);
        var sourceText = NormalizeLf(File.ReadAllText(fullSourcePath, Encoding.UTF8));
        var exportResult = ExportSource(sourceText);
        if (!exportResult.IsSuccess)
        {
            return KernelResult<FirmamentBuildAndExportResult>.Failure(exportResult.Diagnostics);
        }

        var resolvedOutputPath = string.IsNullOrWhiteSpace(outputPath)
            ? ResolveDefaultOutputPath(fullSourcePath)
            : Path.GetFullPath(outputPath);

        Directory.CreateDirectory(Path.GetDirectoryName(resolvedOutputPath)!);
        File.WriteAllText(resolvedOutputPath, exportResult.Value.StepText, new UTF8Encoding(false));

        return KernelResult<FirmamentBuildAndExportResult>.Success(
            new FirmamentBuildAndExportResult(
                fullSourcePath,
                resolvedOutputPath,
                exportResult.Value));
    }


    private static KernelResult<FirmamentStepExportResult> ExportSource(string sourceText)
    {
        var v2Parse = FirmamentV2Parser.Parse(sourceText);
        if (v2Parse.IsSuccess && v2Parse.Document is not null)
        {
            if (TryExportV2SemanticHoleBody(v2Parse.Document) is { } semanticHoleExport)
            {
                return semanticHoleExport;
            }

            var lowering = FirmamentV2BuildLowering.LowerPrimitiveBridge(v2Parse.Document);
            if (!lowering.IsSuccess)
            {
                return KernelResult<FirmamentStepExportResult>.Failure(lowering.Diagnostics);
            }

            var execution = FirmamentPrimitiveExecutor.Execute(lowering.Value);
            if (!execution.IsSuccess)
            {
                return KernelResult<FirmamentStepExportResult>.Failure(execution.Diagnostics);
            }

            var executedPrimitive = execution.Value.ExecutedPrimitives.LastOrDefault();
            if (executedPrimitive is null)
            {
                return KernelResult<FirmamentStepExportResult>.Failure(execution.Diagnostics);
            }

            var step = Step242Exporter.ExportBody(executedPrimitive.Body);
            if (!step.IsSuccess)
            {
                return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
            }

            return KernelResult<FirmamentStepExportResult>.Success(
                new FirmamentStepExportResult(
                    step.Value,
                    executedPrimitive.FeatureId,
                    executedPrimitive.OpIndex,
                    "primitive",
                    v2Parse.Document.Solid.RecordType.ToLowerInvariant(),
                    DatumInspection: [],
                    DimensionInspection: []));
        }

        return FirmamentStepExporter.Export(new FirmamentCompileRequest(new FirmamentSourceDocument(sourceText)));
    }

    private static KernelResult<FirmamentStepExportResult>? TryExportV2SemanticHoleBody(FirmamentV2Document document)
    {
        if (document.ModifyBlocks is not { Count: > 0 })
        {
            return null;
        }

        if (document.Solids.Count != 1 || document.Solid.Primitive is not FirmamentV2BoxRecord box || box.Size.Count != 3)
        {
            return null;
        }

        var semanticHoles = FirmamentV2SemanticHoleLowering.LowerSemanticHoles(document);
        if (semanticHoles.Count == 0)
        {
            return null;
        }

        var feature = semanticHoles[0];
        var host = new AirHoleSimpleShaftHost(box.Size[0], box.Size[1], -box.Size[2] / 2d, box.Size[2] / 2d);
        Aetheris.Kernel.Core.Brep.BrepBody? body;
        IReadOnlyList<string> diagnostics;
        if (semanticHoles.Count == 1)
        {
            var materialized = AirHoleSimpleShaftMaterializer.Execute(feature, host);
            body = materialized.Body;
            diagnostics = materialized.Diagnostics;
            if (!materialized.Succeeded || body is null)
            {
                return SemanticHoleFailure(diagnostics);
            }
        }
        else
        {
            var materialized = AirHoleCompositeMaterializer.Execute(semanticHoles, host);
            body = materialized.Body;
            diagnostics = materialized.Diagnostics;
            if (!materialized.Succeeded || body is null)
            {
                return SemanticHoleFailure(diagnostics);
            }
        }

        var step = Step242Exporter.ExportBody(body);
        if (!step.IsSuccess)
        {
            return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
        }

        return KernelResult<FirmamentStepExportResult>.Success(
            new FirmamentStepExportResult(
                step.Value,
                feature.FeatureId,
                0,
                semanticHoles.Count == 1 ? nameof(AirHoleSimpleShaftMaterializer) : nameof(AirHoleCompositeMaterializer),
                semanticHoles.Count == 1 ? feature.Stack.Kind.ToString() : "CompositeSimpleShaft",
                DatumInspection: [],
                DimensionInspection: []));
    }

    private static KernelResult<FirmamentStepExportResult> SemanticHoleFailure(IEnumerable<string> diagnostics) =>
        KernelResult<FirmamentStepExportResult>.Failure(diagnostics.Select(d => new Kernel.Core.Diagnostics.KernelDiagnostic(
            Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed,
            Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error,
            d,
            "FirmamentV2.SemanticHoleBuild")).ToArray());

    private static string ResolveDefaultOutputPath(string fullSourcePath)
    {
        var root = FindRepositoryRoot(Path.GetDirectoryName(fullSourcePath)!);
        var sourceFileName = Path.GetFileNameWithoutExtension(fullSourcePath);
        return Path.Combine(root, "testdata", "firmament", "exports", sourceFileName + ".step");
    }

    private static string FindRepositoryRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root for Firmament export output.");
    }

    private static string NormalizeLf(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
}
