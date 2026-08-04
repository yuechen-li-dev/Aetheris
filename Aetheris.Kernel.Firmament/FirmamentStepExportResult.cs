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
    FirmamentAirChamferReport? Air = null,
    Aetheris.Kernel.Firmament.FirmamentV2.ConceptIrDocument? ConceptIr = null,
    IReadOnlyList<FirmamentHoleFeatureReport>? Features = null);

public sealed record FirmamentHoleFeatureReport(
    string Name,
    string Kind,
    string FeatureId,
    double Diameter,
    double LocalU,
    double LocalV,
    IReadOnlyList<double>? ResolvedPoint3,
    string? CenterSource,
    string? CenterStableId,
    int? PointOrdinal,
    string PlacementFace,
    string? SourceSpan,
    string MaterializationRoute,
    string? ConstructionKind = null,
    string? StackKind = null,
    string? WitnessSummary = null,
    int CylindricalFaces = 0,
    int ConicalFaces = 0,
    int PlanarFaces = 0,
    string? StepSha256 = null,
    bool StepReimportSucceeded = false);

public sealed record FirmamentAirChamferReport(
    FirmamentAirChamferFeatureReport Feature,
    FirmamentAirChamferConstructionReport Construction,
    FirmamentAirChamferBRepPlanReport BRepPlan,
    FirmamentAirChamferMaterializationReport Materialization,
    FirmamentAirChamferStepReport Step,
    FirmamentLocalizedChamferTrace? LocalizedChamfer = null,
    FirmamentLocalizedFilletTrace? LocalizedFillet = null,
    FirmamentLocalizedEdgeFinishTrace? LocalizedEdgeFinish = null);

/// <summary>Authoritative shared semantic evidence for localized edge replacement routes.</summary>
public sealed record FirmamentLocalizedEdgeFinishTrace(
    string Kind,
    string Selection,
    string RuleKind,
    double Value,
    string Construction,
    string ReplacementGeometry,
    string SelectionMode,
    int RetainedFaces,
    int ReplacementFaces,
    string EndpointPolicy,
    FirmamentLocalizedChamferPlanTrace BRepPlan,
    string Preflight,
    bool LegacyFallback);

/// <summary>Bounded report of the localized AIR path; it is evidence, never input to emission.</summary>
public sealed record FirmamentLocalizedChamferTrace(
    string Selection,
    string Construction,
    string SelectionMode,
    int RetainedFaces,
    int ReplacementFaces,
    string EndpointPolicy,
    FirmamentLocalizedChamferPlanTrace BRepPlan,
    string Preflight,
    bool LegacyFallback);

public sealed record FirmamentLocalizedChamferPlanTrace(bool Authoritative, string Signature);

/// <summary>Evidence emitted by the narrow AIR-FILLET-LOCALIZED-M1 production route.</summary>
public sealed record FirmamentLocalizedFilletTrace(
    string Selection,
    string Rule,
    double Radius,
    string Construction,
    string Profile,
    string Sweep,
    string SelectionMode,
    int RetainedFaces,
    int ReplacementFaces,
    string EndpointPolicy,
    FirmamentLocalizedChamferPlanTrace BRepPlan,
    string Preflight,
    bool LegacyFallback);

public sealed record FirmamentAirChamferFeatureReport(string Kind, string Body, string FeatureId, string FeatureName, string Selection, double Distance, string Unit, string SourceSpan, string Admission, string AdmissionReason, IReadOnlyDictionary<string, string>? Provenance = null);
public sealed record FirmamentAirChamferConstructionReport(
    string Kind,
    int SectionCount,
    IReadOnlyList<double> SectionZ,
    string Correspondence,
    string SplitPolicy,
    string? WitnessSummary = null,
    bool CompilerGeneratedWitness = false,
    IReadOnlyList<IReadOnlyList<double>>? SharpProfile = null,
    IReadOnlyList<IReadOnlyList<double>>? ReplacementProfile = null);
public sealed record FirmamentAirChamferBRepPlanReport(bool Authoritative, int ExpectedVertices, int ExpectedEdges, int ExpectedFaces, int ExpectedLoops, int ExpectedCoedges, int ChamferFaces, string SplitPolicy, string DeterministicSignature, string? PlanKind = null);
public sealed record FirmamentAirChamferMaterializationReport(string Route, bool LegacyFallback, bool EnclosedManifold, int Vertices, int Edges, int Faces, string Bounds, double MeasuredTopInsetX, double MeasuredTopInsetY, int CylindricalFaces = 0, int ConicalFaces = 0, int PlanarFaces = 0);
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
