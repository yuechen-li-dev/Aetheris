namespace Aetheris.Kernel.Firmament;

public sealed record FirmamentStepExportResult(
    string StepText,
    string ExportedFeatureId,
    int ExportedOpIndex,
    string ExportedBodyCategory,
    string? ExportedFeatureKind = null,
    string ExportBodyPolicy = FirmamentStepExporter.LastExecutedGeometricBodyPolicy,
    string ExportBodySelectionReason = FirmamentStepExporter.LastExecutedGeometricBodySelectionReason,
    IReadOnlyList<FirmamentPmiInspectionDatum>? DatumInspection = null,
    IReadOnlyList<FirmamentPmiInspectionDimension>? DimensionInspection = null,
    Aetheris.Kernel.Firmament.FirmamentV2.InlineStepMigrationReport? InlineStepMigration = null,
    Aetheris.Kernel.Firmament.FirmamentV2.InlineStepReplacementAssistReport? InlineStepReplacementAssist = null,
    FirmamentAirChamferReport? Air = null);

public sealed record FirmamentAirChamferReport(
    FirmamentAirChamferFeatureReport Feature,
    FirmamentAirChamferConstructionReport Construction,
    FirmamentAirChamferBRepPlanReport BRepPlan,
    FirmamentAirChamferMaterializationReport Materialization,
    FirmamentAirChamferStepReport Step);

public sealed record FirmamentAirChamferFeatureReport(string Kind, string Body, string FeatureId, string FeatureName, string Selection, double Distance, string Unit, string SourceSpan, string Admission, string AdmissionReason);
public sealed record FirmamentAirChamferConstructionReport(string Kind, int SectionCount, IReadOnlyList<double> SectionZ, string Correspondence, string SplitPolicy);
public sealed record FirmamentAirChamferBRepPlanReport(bool Authoritative, int ExpectedVertices, int ExpectedEdges, int ExpectedFaces, int ExpectedLoops, int ExpectedCoedges, int ChamferFaces, string SplitPolicy, string DeterministicSignature);
public sealed record FirmamentAirChamferMaterializationReport(string Route, bool LegacyFallback, bool EnclosedManifold, int Vertices, int Edges, int Faces, string Bounds, double MeasuredTopInsetX, double MeasuredTopInsetY);
public sealed record FirmamentAirChamferStepReport(string Schema, string Sha256, bool ReimportSucceeded, int ReimportedVertices, int ReimportedEdges, int ReimportedFaces, string ReimportedBounds, bool ReimportedManifold);

public sealed record FirmamentPmiInspectionDatum(
    string Label,
    string DatumType,
    string Target);

public sealed record FirmamentPmiInspectionDimension(
    string Kind,
    string Target,
    string? Datum,
    double Value,
    string? SourceTag,
    string? CandidateName = null);
