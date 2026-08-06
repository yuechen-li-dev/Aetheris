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
    IReadOnlyList<FirmamentHoleFeatureReport>? Features = null,
    FirmamentRoundedBoxReport? RoundedBox = null,
    FirmamentHollowBodyReport? Hollow = null,
    FirmamentStandaloneLatticeReport? Lattice = null,
    FirmamentCombinedFeaturePlanReport? Combined = null);

/// <summary>Compact inspection evidence for the bounded X1 composed feature route.</summary>
public sealed record FirmamentCombinedFeaturePlanReport(
    string Route,
    string Disposition,
    string HostPlanId,
    IReadOnlyList<string> OrderedStages,
    string FinalPlanId,
    string Interaction,
    int HoleDescendantCount,
    int EdgeFinishDescendantCount,
    double HostVolume,
    double HoleRemovedVolume,
    double FinalAnalyticVolume,
    string StepSha256,
    int Vertices,
    int Edges,
    int Faces,
    int Planes,
    int Cylinders,
    bool ReimportSucceeded,
    bool ReimportedManifold);

public sealed record FirmamentStandaloneLatticeReport(
    string Template,
    string Pattern,
    IReadOnlyList<int> Cells,
    double CellSize,
    double StrutRadius,
    double NodeRadius,
    IReadOnlyList<double> DomainSize,
    string Placement,
    int Nodes,
    int Members,
    int Seams,
    int Valence3,
    int Valence4,
    int Valence5,
    int Valence6,
    bool AuthoritativePlan,
    string Signature,
    int Vertices,
    int Edges,
    int Faces,
    int SphericalFaces,
    int CylindricalFaces,
    double AnalyticVolume,
    double BrepVolume,
    double VolumeDelta,
    double SurfaceArea,
    IReadOnlyList<double> Centroid,
    string StepSha256,
    bool ReimportSucceeded,
    bool ReimportedManifold,
    bool LegacyFallback = false);

/// <summary>Export evidence for the generic Primitive&lt;Hollow&gt; path.</summary>
public sealed record FirmamentHollowBodyReport(
    string Primitive,
    string ConstructionPolicy,
    double WallThickness,
    IReadOnlyList<string> Openings,
    string WitnessKind,
    bool WitnessExact,
    string ThicknessPolicy,
    bool ThicknessVerified,
    string BRepPlanKind,
    bool AuthoritativePlan,
    string Signature,
    int Vertices,
    int Edges,
    int Faces,
    int Planes,
    int Cylinders,
    int Cones,
    int RimFaces,
    string AnalyticVolume,
    string StepSha256,
    bool ReimportSucceeded,
    bool ReimportedManifold,
    bool LegacyFallback = false);

public sealed record FirmamentRoundedBoxReport(
    FirmamentRoundedBoxPrimitiveReport Primitive,
    FirmamentRoundedBoxEdgeFinishReport? EdgeFinish,
    FirmamentRoundedBoxPlanReport BRepPlan,
    FirmamentRoundedBoxGeometryReport Geometry,
    FirmamentRoundedBoxStepReport Step);
public sealed record FirmamentRoundedBoxPrimitiveReport(string FeatureAir, string ConstructionAir, double Width, double Depth, double Height, double CornerRadius, int PlanarSideFaces, int CylindricalCornerWallFaces, bool LegacyFallback);
public sealed record FirmamentRoundedBoxEdgeFinishReport(string Target, string Kind, double Radius, int CylindricalStraightFaces, int ToroidalCornerFaces, double ToroidalMajorRadius, double ToroidalMinorRadius, string Derivation);
public sealed record FirmamentRoundedBoxPlanReport(bool Authoritative, string Signature, int Vertices, int Edges, int Faces, int Loops, int Coedges, IReadOnlyList<string> Roles);
public sealed record FirmamentRoundedBoxGeometryReport(string Bounds, bool EnclosedManifold, int Planes, int Cylinders, int Tori, double AnalyticVolume, string Preflight);
public sealed record FirmamentRoundedBoxStepReport(string Sha256, bool ReimportSucceeded, bool ReimportedManifold, int ReimportedFaces, string ReimportedBounds);

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
    FirmamentLocalizedEdgeFinishTrace? LocalizedEdgeFinish = null,
    FirmamentLocalizedEdgeJunctionTrace? LocalizedEdgeJunction = null);

/// <summary>Evidence for the combined two-edge realization; both replacements share one plan.</summary>
public sealed record FirmamentLocalizedEdgeJunctionTrace(
    IReadOnlyList<string> Edges,
    string FinishKind,
    string Rule,
    double Value,
    string SelectionMode,
    string Construction,
    string CornerPatch,
    int ReplacementFaces,
    int JunctionFaces,
    FirmamentLocalizedChamferPlanTrace BRepPlan,
    string Preflight,
    bool LegacyFallback,
    int CandidatePlans,
    int HardValidPlans,
    FirmamentLocalizedJunctionClosureTrace? Closure = null);

public sealed record FirmamentLocalizedJunctionClosureTrace(
    string Kind,
    string SurfaceA,
    string SurfaceB,
    string CurveKind,
    bool Exact,
    int SharedEdges,
    string Branch);

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
public sealed record FirmamentAirChamferMaterializationReport(string Route, bool LegacyFallback, bool EnclosedManifold, int Vertices, int Edges, int Faces, string Bounds, double MeasuredTopInsetX, double MeasuredTopInsetY, int CylindricalFaces = 0, int ConicalFaces = 0, int PlanarFaces = 0, int SphericalFaces = 0);
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
