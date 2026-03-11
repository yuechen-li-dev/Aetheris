using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Import;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Step242;

internal sealed class StepParsedSourceDocument(Step242ParsedDocument document) : IParsedSourceDocument
{
    public Step242ParsedDocument Document { get; } = document;

    public string SourceFamily => "STEP/AP242";
}

internal sealed class StepSourceConnector : ISourceConnector
{
    public string Name => "step-ap242-connector";

    public bool CanOpen(ImportRequest request)
    {
        return request.SourceText.Contains("ISO-10303-21", StringComparison.OrdinalIgnoreCase)
            || request.SourceText.Contains("DATA;", StringComparison.OrdinalIgnoreCase);
    }

    public KernelResult<IParsedSourceDocument> Parse(ImportRequest request)
    {
        var parseResult = Step242SubsetParser.Parse(request.SourceText);
        if (!parseResult.IsSuccess)
        {
            return KernelResult<IParsedSourceDocument>.Failure(parseResult.Diagnostics);
        }

        return KernelResult<IParsedSourceDocument>.Success(new StepParsedSourceDocument(parseResult.Value));
    }
}

internal sealed class Step242ExactBRepImportLane : IImportLane
{
    public string Name => "step-ap242-exact-brep";

    public ImportLaneKind Kind => ImportLaneKind.ExactBRep;

    public bool CanImport(IParsedSourceDocument document, ImportPolicy policy)
    {
        if (document is not StepParsedSourceDocument stepDocument)
        {
            return false;
        }

        return !Step242Importer.HasTessellatedSolid(stepDocument.Document);
    }

    public KernelResult<BrepBody> Import(IParsedSourceDocument document, ImportPolicy policy)
    {
        var stepDocument = (StepParsedSourceDocument)document;
        return ImportExactBrep(stepDocument.Document);
    }

    internal static KernelResult<BrepBody> ImportExactBrep(Step242ParsedDocument document)
    {
        try
        {
            return Step242Importer.ImportExactBrepCore(document);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.InvalidArgument,
                    KernelDiagnosticSeverity.Error,
                    $"Importer rejected parseable STEP input: {ex.Message}",
                    "Importer.Guardrail")
            ]);
        }
    }
}

internal sealed class Step242TessellatedImportLane : IImportLane
{
    public string Name => "step-ap242-tessellated";

    public ImportLaneKind Kind => ImportLaneKind.Tessellated;

    public bool CanImport(IParsedSourceDocument document, ImportPolicy policy)
    {
        if (document is not StepParsedSourceDocument stepDocument)
        {
            return false;
        }

        return Step242Importer.HasTessellatedSolid(stepDocument.Document);
    }

    public KernelResult<BrepBody> Import(IParsedSourceDocument document, ImportPolicy policy)
    {
        var stepDocument = (StepParsedSourceDocument)document;
        return Step242Importer.ImportParsedDocumentForLane(stepDocument.Document, Kind);
    }
}
