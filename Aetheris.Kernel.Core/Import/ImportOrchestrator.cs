using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Import;

public interface ISourceConnector
{
    string Name { get; }

    bool CanOpen(ImportRequest request);

    KernelResult<IParsedSourceDocument> Parse(ImportRequest request);
}

public interface IParsedSourceDocument
{
    string SourceFamily { get; }
}

public interface IImportLane
{
    string Name { get; }

    ImportLaneKind Kind { get; }

    bool CanImport(IParsedSourceDocument document, ImportPolicy policy);

    KernelResult<BrepBody> Import(IParsedSourceDocument document, ImportPolicy policy);
}

public sealed class ImportOrchestrator
{
    private readonly IReadOnlyList<ISourceConnector> _connectors;
    private readonly IReadOnlyList<IImportLane> _lanes;

    public ImportOrchestrator(IReadOnlyList<ISourceConnector> connectors, IReadOnlyList<IImportLane> lanes)
    {
        _connectors = connectors;
        _lanes = lanes;
    }

    public static ImportOrchestrator CreateDefault(Action<ImportCompositionBuilder>? configure = null)
    {
        var builder = new ImportCompositionBuilder();
        Step242.Step242ImportComposition.Register(builder);
        configure?.Invoke(builder);

        return new ImportOrchestrator(builder.Connectors, builder.Lanes);
    }

    public ImportResult Import(ImportRequest request)
    {
        var policy = request.Policy ?? new ImportPolicy();
        var connector = _connectors.FirstOrDefault(c => c.CanOpen(request));
        if (connector is null)
        {
            return ImportResult.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.NotImplemented,
                    KernelDiagnosticSeverity.Error,
                    "No source connector recognized this import request.",
                    "Import.Orchestrator.ConnectorSelection")
            ]);
        }

        var parseResult = connector.Parse(request);
        if (!parseResult.IsSuccess)
        {
            return ImportResult.Failure(parseResult.Diagnostics);
        }

        var lane = SelectLane(parseResult.Value, policy);
        if (lane is null)
        {
            return ImportResult.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.NotImplemented,
                    KernelDiagnosticSeverity.Error,
                    $"No import lane can process source family '{parseResult.Value.SourceFamily}' with preference '{policy.PreferredLane}'.",
                    "Import.Orchestrator.LaneSelection")
            ]);
        }

        var bodyResult = lane.Import(parseResult.Value, policy);
        return new ImportResult(bodyResult, connector.Name, lane.Kind, ResolveRepresentationTruth(lane.Kind), parseResult.Value.SourceFamily);
    }

    private static ImportRepresentationTruthKind ResolveRepresentationTruth(ImportLaneKind laneKind)
    {
        return laneKind switch
        {
            ImportLaneKind.ExactBRep => ImportRepresentationTruthKind.ExactBRep,
            ImportLaneKind.Tessellated => ImportRepresentationTruthKind.TessellatedOrFaceted,
            ImportLaneKind.Approximation => ImportRepresentationTruthKind.Approximation,
            ImportLaneKind.Compatibility => ImportRepresentationTruthKind.CompatibilityAdjusted,
            _ => ImportRepresentationTruthKind.Unknown
        };
    }

    private IImportLane? SelectLane(IParsedSourceDocument document, ImportPolicy policy)
    {
        if (policy.PreferredLane != ImportLaneKind.Auto)
        {
            return _lanes.FirstOrDefault(l => l.Kind == policy.PreferredLane && l.CanImport(document, policy));
        }

        return _lanes.FirstOrDefault(l => l.CanImport(document, policy));
    }
}

public sealed class ImportCompositionBuilder
{
    private readonly List<ISourceConnector> _connectors = [];
    private readonly List<IImportLane> _lanes = [];

    internal IReadOnlyList<ISourceConnector> Connectors => _connectors;

    internal IReadOnlyList<IImportLane> Lanes => _lanes;

    public ImportCompositionBuilder AddConnector(ISourceConnector connector)
    {
        _connectors.Add(connector);
        return this;
    }

    public ImportCompositionBuilder AddLane(IImportLane lane)
    {
        _lanes.Add(lane);
        return this;
    }
}
