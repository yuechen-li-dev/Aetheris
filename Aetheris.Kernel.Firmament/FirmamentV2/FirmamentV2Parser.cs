using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public static partial class FirmamentV2Parser
{
    public const string MissingModel = "firmament-v2-missing-model";
    public const string MissingUnits = "firmament-v2-missing-units";
    public const string MissingSolid = "firmament-v2-missing-solid";
    public const string UnsupportedConstruct = "firmament-v2-unsupported-construct";
    public const string UnknownRecordType = "firmament-v2-unknown-record-type";
    public const string BoxMissingSize = "firmament-v2-box-missing-size";
    public const string BoxSizeArity = "firmament-v2-box-size-arity";
    public const string PrimitiveFieldMissing = "firmament-v2-primitive-field-missing";
    public const string PrimitiveFieldUnknown = "firmament-v2-primitive-field-unknown";
    public const string PrimitiveFieldInvalid = "firmament-v2-primitive-field-invalid";
    public const string DegenerateDimension = "firmament-degenerate-dimension";
    public const string NameUnresolved = "firmament-v2-name-unresolved";
    public const string DuplicateName = "firmament-v2-duplicate-name";
    public const string WithRequiresRecord = "firmament-v2-with-requires-record";
    public const string WithRequiresBoxRecord = "firmament-v2-with-requires-box-record";
    public const string WithFieldNotFound = "firmament-v2-with-field-not-found";
    public const string WithFieldTypeMismatch = "firmament-v2-with-field-type-mismatch";
    public const string WithForwardReference = "firmament-v2-with-forward-reference";
    public const string WithDerivedRecordInvalid = "firmament-v2-with-derived-record-invalid";
    public const string ExposeBlockUnsupported = "firmament-v2-expose-block-unsupported";
    public const string ExposeRequiresBoxRecord = "firmament-v2-expose-requires-box-record";
    public const string ExposeAliasDuplicate = "firmament-v2-expose-alias-duplicate";
    public const string ExposeAliasInvalid = "firmament-v2-expose-alias-invalid";
    public const string SelectorUnsupported = "firmament-v2-selector-unsupported";
    public const string SelectorAxisInvalid = "firmament-v2-selector-axis-invalid";
    public const string SelectorSubselectorUnsupported = "firmament-v2-selector-subselector-unsupported";
    public const string FatArrowOutsideExpose = "firmament-v2-fat-arrow-outside-expose";
    public const string RawBackendIdReferenceForbidden = "firmament-raw-backend-id-reference-forbidden";
    public const string DfmConceptUnitMismatch = "firmament-v2-dfm-concept-unit-mismatch";
    public const string DfmMinimumToolRadiusViolation = "firmament-v2-dfm-minimum-tool-radius-violation";
    public const string Phase3EdgeFinishSyntaxInvalid = "firmament-v2-phase3-edge-finish-syntax-invalid";
    public const string FillRegionInvalid = "firmament-v2-fill-region-invalid";
    public const string FillUnknownRegion = "firmament-v2-fill-unknown-region";
    public const string FillUnknownHost = "firmament-v2-fill-unknown-host";
    public const string FillUnsupportedPattern = "firmament-v2-fill-unsupported-pattern";
    public const string FillInvalidCellSize = "firmament-v2-fill-invalid-cell-size";
    public const string FillInvalidStrutRadius = "firmament-v2-fill-invalid-strut-radius";
    public const string FillUnsupportedBoundaryPolicy = "firmament-v2-fill-unsupported-boundary-policy";
    public const string FillMultipleUnsupported = "firmament-v2-fill-multiple-unsupported";
    public const string StandaloneFillRegionRequired = "standalone-fill-region-required";
    public const string UnsupportedStandaloneFillPattern = "unsupported-standalone-fill-pattern";
    public const string InvalidCellCount = "invalid-cell-count";
    public const string CellSizeMustBePositive = "cell-size-must-be-positive";
    public const string StrutRadiusMustBePositive = "strut-radius-must-be-positive";
    public const string NodeRadiusMustBePositive = "node-radius-must-be-positive";
    public const string MaterialBoundsMismatch = "material-bounds-mismatch";
    public const string StandaloneFillMultipleUnsupported = "standalone-fill-multiple-unsupported";
    public const string UnknownConstructionPolicy = "firmament-v2-unknown-construction-policy";
    public const string PrimitiveDoesNotSatisfyHollowConstructible = "firmament-v2-primitive-does-not-satisfy-hollow-constructible";
    public const string WallThicknessRequired = "firmament-v2-wall-thickness-required";
    public const string WallThicknessMustBePositive = "firmament-v2-wall-thickness-must-be-positive";
    public const string UnsupportedOpening = "firmament-v2-unsupported-opening";
    public const string MultipleOpeningsNotSupported = "firmament-v2-multiple-openings-not-supported";
    public const string CanonicalDocumentMalformed = "firmament-v2-canonical-document-malformed";
    public const string CanonicalUnitsInvalid = "firmament-v2-canonical-units-invalid";
    public const string CanonicalPrimitiveMalformed = "firmament-v2-canonical-primitive-malformed";
    public const string CanonicalPoint2Invalid = "firmament-v2-canonical-point2-invalid";
    public const string CanonicalModifyMalformed = "firmament-v2-canonical-modify-malformed";
    public const string CanonicalDeclarationUnrecognized = "firmament-v2-canonical-declaration-unrecognized";
    public const string CanonicalDeclarationUnknown = "firmament-v2-canonical-declaration-unknown";
    public const string AssertVolumeMalformed = "firmament-v2-assert-volume-malformed";
    public const string AssertVolumeKindUnsupported = "firmament-v2-assert-kind-unsupported";
    public const string AssertVolumeExpectedDimensionMismatch = "firmament-v2-assert-volume-expected-dimension-mismatch";
    public const string AssertVolumeToleranceDimensionMismatch = "firmament-v2-assert-volume-tolerance-dimension-mismatch";
    public const string AssertVolumeToleranceNegative = "firmament-v2-assert-volume-tolerance-negative";
    public const string CanonicalConstructNotYetSupported = "firmament-v2-canonical-construct-not-yet-supported";
    public const string EdgeFinishProfileComposeBoundaryUnsupported = "EdgeFinishProfileComposeBoundaryUnsupported";
    public const string CanonicalFieldUnknown = "firmament-v2-canonical-field-unknown";
    public const string SelectionMalformed = "firmament-v2-selection-malformed";
    public const string SelectionDuplicateName = "firmament-v2-selection-duplicate-name";
    public const string SelectionUnknownSource = "firmament-v2-selection-unknown-source";
    public const string SelectionResultKindInvalid = "firmament-v2-selection-result-kind-invalid";

    public const string ConceptUnknownFamily = "firmament-v2-concept-unknown-family";
    public const string ConceptUnknownConcept = "firmament-v2-concept-unknown-concept";
    public const string ConceptMissingRequiredField = "firmament-v2-concept-missing-required-field";
    public const string ConceptUnknownField = "firmament-v2-concept-unknown-field";
    public const string ConceptDuplicateField = "firmament-v2-concept-duplicate-field";
    public const string ConceptFieldTypeMismatch = "firmament-v2-concept-field-type-mismatch";
    public const string ConceptInvalidTarget = "firmament-v2-concept-invalid-target";
    public const string ConceptDescriptorUnavailable = "firmament-v2-concept-descriptor-unavailable";

    public const string LetDuplicateName = "firmament-v2-let-duplicate-name";
    public const string LetUnknownType = "firmament-v2-let-unknown-type";
    public const string LetTypeMismatch = "firmament-v2-let-type-mismatch";
    public const string LetInvalidLiteral = "firmament-v2-let-invalid-literal";
    public const string LetUnitMismatch = "firmament-v2-let-unit-mismatch";
    public const string LetLiteralOnly = "firmament-v2-let-literal-only";
    public const string LetRecordDuplicateName = "firmament-v2-let-record-duplicate-name";
    public const string LetRecordDuplicateField = "firmament-v2-let-record-duplicate-field";
    public const string LetReferenceUnknownRecord = "firmament-v2-let-reference-unknown-record";
    public const string LetReferenceUnknownField = "firmament-v2-let-reference-unknown-field";
    public const string LetReferenceNonRecord = "firmament-v2-let-reference-non-record";
    public const string LetReferenceRecordUsedAsValue = "firmament-v2-let-reference-record-used-as-value";
    public const string ExpressionUnknownSymbol = "firmament-v2-expression-unknown-symbol";
    public const string ExpressionUnknownRecord = "firmament-v2-expression-unknown-record";
    public const string ExpressionUnknownField = "firmament-v2-expression-unknown-field";
    public const string ExpressionRecordUsedAsValue = "firmament-v2-expression-record-used-as-value";
    public const string ExpressionScalarUsedAsRecord = "firmament-v2-expression-scalar-used-as-record";
    public const string ExpressionTypeMismatch = "firmament-v2-expression-type-mismatch";
    public const string ExpressionInvalidOperator = "firmament-v2-expression-invalid-operator";
    public const string ExpressionDivisionByZero = "firmament-v2-expression-division-by-zero";
    public const string ExpressionCycle = "firmament-v2-expression-cycle";
    public const string ExpressionUnsupported = "firmament-v2-expression-unsupported";
    public const string ToleranceInvalidType = "firmament-v2-tolerance-invalid-type";
    public const string ToleranceUnitMismatch = "firmament-v2-tolerance-unit-mismatch";
    public const string ToleranceInvalidLiteral = "firmament-v2-tolerance-invalid-literal";
    public const string ToleranceNegativeBilateral = "firmament-v2-tolerance-negative-bilateral";
    public const string ToleranceMissingMinus = "firmament-v2-tolerance-missing-minus";
    public const string ToleranceMissingPlus = "firmament-v2-tolerance-missing-plus";
    public const string ToleranceDroppedThroughArithmetic = "firmament-v2-tolerance-dropped-through-arithmetic";
    public const string ToleranceUnsupported = "firmament-v2-tolerance-unsupported";
    public const string SemanticConstraintMalformed = "firmament-v2-semantic-constraint-malformed";
    public const string SemanticConstraintUnsupported = "firmament-v2-semantic-constraint-unsupported";
    public const string SemanticConstraintDimensionMismatch = "firmament-v2-semantic-constraint-dimension-mismatch";
    public const string SemanticConstraintValidationFailed = "firmament-v2-semantic-constraint-validation-failed";
    public const string PmiProjectionUnknownRequire = "firmament-v2-pmi-projection-unknown-require";
    public const string PmiProjectionUnsupportedKind = "firmament-v2-pmi-projection-unsupported-kind";
    public const string PmiProjectedFieldMustNotOverrideSourceConstraint = "firmament-v2-pmi-projected-field-must-not-override-source-constraint";

    public const string ModifyTargetUnresolved = "firmament-v2-modify-target-unresolved";
    public const string ModifyTargetNotSolid = "firmament-v2-modify-target-not-solid";
    public const string ModifyTargetNotBoundToActiveCompose = "ModifyTargetNotBoundToActiveCompose";
    public const string RegionUnsupported = "firmament-v2-region-unsupported";
    public const string RegionAttachmentSelectorUnsupported = "firmament-v2-region-attachment-selector-unsupported";
    public const string CutUnsupported = "firmament-v2-cut-unsupported";
    public const string CutToolUnsupported = "firmament-v2-cut-tool-unsupported";
    public const string CylinderRadiusMissing = "firmament-v2-cylinder-radius-missing";
    public const string CylinderRadiusInvalid = "firmament-v2-cylinder-radius-invalid";
    public const string ThroughSelectorUnsupported = "firmament-v2-through-selector-unsupported";
    public const string AliasUnresolved = "firmament-v2-alias-unresolved";
    public const string AliasRefTypeUnsupported = "firmament-v2-alias-ref-type-unsupported";
    public const string SideHoleAliasMustResolveToFace = "firmament-v2-side-hole-alias-must-resolve-to-face";
    public const string SideHoleAliasResolvesToUnsupportedFace = "firmament-v2-side-hole-alias-resolves-to-unsupported-face";
    public const string SideHoleOnlyPlusXMinusXSupported = "firmament-v2-side-hole-only-plus-x-minus-x-supported";
    public const string SideHoleRouteUnsupported = "firmament-v2-side-hole-route-unsupported";
    public const string SideHoleSameFaceUnsupported = "firmament-v2-side-hole-same-face-unsupported";
    public const string SideHoleAxisNotYetSupported = "firmament-v2-side-hole-axis-not-yet-supported";
    public const string SideHoleRadiusExceedsClearance = "firmament-v2-side-hole-radius-exceeds-clearance";
    public const string CylinderRadiusNotFinite = "firmament-v2-side-hole-radius-not-finite";
    public const string CylinderCenterInvalid = "firmament-v2-cylinder-center-invalid";
    public const string CylinderCenterArityInvalid = "firmament-v2-cylinder-center-arity-invalid";
    public const string CylinderCenterNotFinite = "firmament-v2-cylinder-center-not-finite";
    public const string SideHoleCenterExceedsClearance = "firmament-v2-side-hole-center-exceeds-clearance";
    public const string HoleVariantUnknown = "firmament-v2-hole-variant-unknown";
    public const string HoleEntryFaceMissing = "firmament-v2-hole-entry-face-missing";
    public const string HoleCenterMissing = "firmament-v2-hole-center-missing";
    public const string HoleShaftMissing = "firmament-v2-hole-shaft-diameter-missing";
    public const string HoleEndMissing = "firmament-v2-hole-end-missing";
    public const string HoleDiameterInvalid = "firmament-v2-hole-diameter-invalid";
    public const string HoleDepthInvalid = "firmament-v2-hole-depth-invalid";
    public const string HoleCounterboreInvalid = "firmament-v2-hole-counterbore-invalid";
    public const string HoleCountersinkInvalid = "firmament-v2-hole-countersink-invalid";
    public const string HoleConstructionPlaneNotFound = "HoleConstructionPlaneNotFound";
    public const string HolePlacementMixed = "HolePlacementMixed";
    public const string HolePlacementDuplicate = "HolePlacementDuplicate";
    public const string HoleConstructionPlaneHostUnsupported = "HoleConstructionPlaneHostUnsupported";
    public const string HoleConstructionPlaneCenterMissing = "HoleLocalCenterInvalid";
    public const string HoleConstructionPlaneExtentUnsupported = "HoleConstructionPlaneExtentUnsupported";
    public const string HoleBlindDepthMissing = "HoleBlindDepthMissing";
    public const string HoleBlindDepthInvalid = "HoleBlindDepthInvalid";
    public const string HoleDrillPointAngleInvalid = "HoleDrillPointAngleInvalid";
    public const string HoleTotalDepthShorterThanTip = "HoleTotalDepthShorterThanTip";
    public const string HoleTerminationConflictsWithExtent = "HoleTerminationConflictsWithExtent";
    public const string HoleDrillPointExitsHost = "HoleDrillPointExitsHost";
    public const string PmiKindUnknown = "firmament-v2-pmi-kind-unknown";
    public const string PmiTargetMissing = "firmament-v2-pmi-target-missing";
    public const string PmiTargetUnresolved = "firmament-v2-pmi-target-unresolved";
    public const string HoleWallUnknownHole = "firmament-v2-hole-wall-unknown-hole";
    public const string HoleWallNotHole = "firmament-v2-hole-wall-target-not-hole";
    public const string HoleWallAmbiguous = "firmament-v2-hole-wall-ambiguous-hole";
    public const string HoleWallConsumerInvalid = "firmament-v2-hole-wall-consumer-invalid";
    public const string PmiDiameterInvalid = "firmament-v2-pmi-diameter-invalid";
    public const string PmiDuplicateName = "firmament-v2-pmi-duplicate-name";
    public const string PmiDuplicateBlock = "firmament-v2-pmi-duplicate-block";
    public const string PmiDuplicateRecord = "firmament-v2-pmi-duplicate-record";
    public const string PmiDuplicateDatum = "firmament-v2-pmi-duplicate-datum";
    public const string PmiUnknownRecordKind = "firmament-v2-pmi-unknown-record-kind";
    public const string PmiMissingRequiredField = "firmament-v2-pmi-missing-required-field";
    public const string PmiUnknownField = "firmament-v2-pmi-unknown-field";
    public const string PmiDuplicateField = "firmament-v2-pmi-duplicate-field";
    public const string PmiInvalidTarget = "firmament-v2-pmi-invalid-target";
    public const string PmiUnknownDatum = "firmament-v2-pmi-unknown-datum";
    public const string PmiDimensionTypeMismatch = "firmament-v2-pmi-dimension-type-mismatch";
    public const string PmiDimensionMissingTolerance = "firmament-v2-pmi-dimension-missing-tolerance";
    public const string PmiToleranceTypeMismatch = "firmament-v2-pmi-tolerance-type-mismatch";
    public const string PmiUnsupported = "firmament-v2-pmi-unsupported";
    public const string InlineStepPathMissing = "firmament-v2-inline-step-path-missing";
    public const string InlineStepPathInvalid = "firmament-v2-inline-step-path-invalid";
    public const string InlineStepFileMissing = "firmament-v2-inline-step-file-missing";
    public const string InlineStepRequiresCanonical = "firmament-inline-step-requires-aetheris-canonical-step";
    public const string InlineStepUnknownBody = "firmament-inline-step-unknown-body";
    public const string InlineStepUnknownFace = "firmament-inline-step-unknown-face";
    public const string PmiImportedTargetNotFace = "firmament-pmi-imported-target-not-face";
    public const string PmiImportedTargetRequiresCanonicalStep = "firmament-pmi-imported-target-requires-canonical-step";
    public const string PmiInvalidImportedTarget = "firmament-pmi-invalid-imported-target";
    public const string UnknownRecognitionBody = "firmament-inline-step-unknown-recognition-body";
    public const string UnknownRecognitionFace = "firmament-inline-step-unknown-recognition-face";
    public const string DuplicateRegion = "firmament-inline-step-duplicate-region";
    public const string UnknownRecognitionRegion = "firmament-inline-step-unknown-recognition-region";
    public const string InvalidRecognitionKind = "firmament-inline-step-invalid-recognition-kind";
    public const string InvalidRecognitionConfidence = "firmament-inline-step-invalid-recognition-confidence";
    public const string PmiRecognizedRegionKindMismatch = "firmament-pmi-recognized-region-kind-mismatch";
    public const string UnknownReplacementBody = "firmament-inline-step-unknown-replacement-body";
    public const string UnknownReplacementRegion = "firmament-inline-step-unknown-replacement-region";
    public const string ReplacementKindMismatch = "firmament-inline-step-replacement-kind-mismatch";
    public const string ReplacementFaceUnresolved = "firmament-inline-step-replacement-face-unresolved";
    public const string ReplacementUnsupportedKind = "firmament-inline-step-replacement-unsupported-kind";
    public const string ReplacementVerificationFailed = "firmament-inline-step-replacement-verification-failed";
    public const string ReplacementRadiusInvalid = "firmament-inline-step-replacement-radius-invalid";
    public const string ReplacementEndUnsupported = "firmament-inline-step-replacement-end-unsupported";
    public const string RecognitionEvidenceRadiusInvalid = "firmament-inline-step-recognition-evidence-radius-invalid";
    public const string RecognitionEvidenceSurfaceFamilyUnknown = "firmament-inline-step-recognition-evidence-surface-family-unknown";
    public const string RecognitionEvidenceAxisInvalid = "firmament-inline-step-recognition-evidence-axis-invalid";
    public const string SemanticProposalKindMismatch = "firmament-inline-step-semantic-proposal-kind-mismatch";
    public const string SemanticProposalRadiusInvalid = "firmament-inline-step-semantic-proposal-radius-invalid";
    public const string SemanticProposalTargetUnresolved = "firmament-inline-step-semantic-proposal-target-unresolved";
    public const string SemanticProposalEndUnsupported = "firmament-inline-step-semantic-proposal-end-unsupported";

    private static readonly HashSet<string> ReservedWords = new(StringComparer.Ordinal) { "let", "model", "units", "solid", "modify", "region", "cut", "template", "concept", "pmi", "recognize", "replace", "with", "manufacturing", "process", "feature", "true", "false", "int", "float", "length", "angle", "string", "bool", "tol" };
    private static readonly Regex LetRegex = new(@"\blet\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<type>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<literal>[^\r\n{}]+)", RegexOptions.CultureInvariant);
    private static readonly Regex LetRecordHeaderRegex = new(@"^\s*let\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant | RegexOptions.Multiline);
    private static readonly Regex LetRecordFieldRegex = new(@"^\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<type>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<literal>.+?)\s*$", RegexOptions.CultureInvariant);
    private static readonly Regex DottedReferenceRegex = new(@"^(?<record>[A-Za-z_][A-Za-z0-9_]*)\.(?<field>[A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.CultureInvariant);
    private static readonly Regex SizeRegex = new(@"\bsize\s*:\s*\[(?<values>[^\]]*)\]", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex FieldRegex = new(@"(?<field>@[A-Za-z_][A-Za-z0-9_]*|[A-Za-z_][A-Za-z0-9_\.]*)\s*:", RegexOptions.CultureInvariant);
    private static readonly Regex FaceSelectorRegex = new(@"^face\((?<axis>[+-][XYZ])\)(?<sub>\.[A-Za-z_][A-Za-z0-9_]*)?$", RegexOptions.CultureInvariant);
    private static readonly Regex CenterRegex = new(@"\bcenter\s*:\s*\[(?<values>[^\]]*)\]", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex RecognizeHeaderRegex = new(@"\brecognize\s+(?<body>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex RecognitionRegionHeaderRegex = new(@"\bregion\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex KindRegex = new("\\bkind\\s*:\\s*(?<kind>\"[^\"]+\"|[A-Za-z_][A-Za-z0-9_]*(?:\\s*<\\s*[A-Za-z_][A-Za-z0-9_]*\\s*>)?)", RegexOptions.CultureInvariant);
    private static readonly Regex FacesRegex = new(@"\bfaces\s*:\s*\[(?<faces>[^\]]*)\]", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex ConfidenceRegex = new(@"\bconfidence\s*:\s*(?<confidence>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant);
    private static readonly Regex EvidenceHeaderRegex = new(@"\bevidence\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex ProposalHeaderRegex = new(@"\bproposes\s+(?<kind>[A-Za-z_][A-Za-z0-9_]*(?:\s*<\s*[A-Za-z_][A-Za-z0-9_]*\s*>)?)\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)?\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex SurfaceFamilyRegex = new(@"\bsurfaceFamily\s*:\s*(?<value>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant);
    private static readonly Regex AxisRegex = new(@"\baxis\s*:\s*(?<value>[+-][XYZ])", RegexOptions.CultureInvariant);
    private static readonly Regex BooleanThroughRegex = new(@"\bthrough\s*:\s*(?<value>true|false)", RegexOptions.CultureInvariant);
    private static readonly Regex EndConditionRegex = new(@"\bend\s*:\s*(?<value>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant);
    private static readonly Regex ReplaceHeaderRegex = new("\\breplace\\s+(?<target>[A-Za-z_][A-Za-z0-9_]*\\.region\\(\"[A-Za-z_][A-Za-z0-9_]*\"\\))\\s+with\\s+(?<kind>[A-Za-z_][A-Za-z0-9_]*(?:\\s*<\\s*[A-Za-z_][A-Za-z0-9_]*\\s*>)?)\\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\\s*\\{", RegexOptions.CultureInvariant);
    private static readonly Regex OnRegex = new("\\bon\\s*:\\s*(?<target>[A-Za-z_][A-Za-z0-9_]*\\.face\\(\"#[0-9]+\"\\)|face\\([^)]+\\)|[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant);
    private static readonly Regex HostSizeRegex = new(@"\bhostSize\s*:\s*\[(?<values>[^\]]*)\]", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    // Pmi is canonical; lowercase pmi remains a bounded compatibility spelling.
    private static readonly Regex PmiHeaderRegex = new(@"\b(?:Pmi|pmi)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex PmiEntryHeaderRegex = new(@"\b(?<kind>[A-Za-z_][A-Za-z0-9_]*)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex TargetRegex = new(@"\btarget\s*:\s*(?<target>[A-Za-z_][A-Za-z0-9_]*\.(?:face|region)\(""[#A-Za-z0-9_]+""\)|[A-Za-z_][A-Za-z0-9_]*\.[A-Za-z_][A-Za-z0-9_]*\([^)]*\)|face\([^)]+\)|[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant);
    private static readonly Regex ImportedFaceTargetRegex = new("^(?<body>[A-Za-z_][A-Za-z0-9_]*)\\.face\\(\\\"(?<entity>#[0-9]+)\\\"\\)$", RegexOptions.CultureInvariant);
    private static readonly Regex RecognizedRegionTargetRegex = new("^(?<body>[A-Za-z_][A-Za-z0-9_]*)\\.region\\(\\\"(?<region>[A-Za-z_][A-Za-z0-9_]*)\\\"\\)$", RegexOptions.CultureInvariant);
    private static readonly Regex ValueRegex = new(@"\b(?:value|diameter)\s*:\s*(?<value>[^\s}]+)", RegexOptions.CultureInvariant);
    private static readonly Regex ManufacturingConceptHeaderRegex = new(@"\bmanufacturing\s+(?<family>[A-Za-z_][A-Za-z0-9_]*)\s*<\s*(?<concept>[A-Za-z_][A-Za-z0-9_]*)\s*>\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex FeatureConceptHeaderRegex = new(@"\bfeature\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<family>[A-Za-z_][A-Za-z0-9_]*)\s*<\s*(?<concept>[A-Za-z_][A-Za-z0-9_]*)\s*>\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex ConceptFieldLineRegex = new(@"^\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<value>.+?)\s*$", RegexOptions.CultureInvariant);
    private static readonly Regex TargetExpressionRegex = new(@"^[A-Za-z_][A-Za-z0-9_]*\.(?:region|face)\(""[^""]+""\)$", RegexOptions.CultureInvariant);
    private static readonly Regex TemplateHeaderRegex = new(@"\btemplate\s*<\s*(?<process>[A-Za-z_][A-Za-z0-9_]*)\s*>\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex ConceptRegex = new(@"\bconcept\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<value>[-+0-9.eE]+)\s*(?<unit>[A-Za-z_][A-Za-z0-9_]*)?", RegexOptions.CultureInvariant);
    private static readonly Regex PathRegex = new("\\bpath\\s*:\\s*\"(?<path>[^\"]+)\"", RegexOptions.CultureInvariant);
    private static readonly Regex FillRegionHeaderRegex = new(@"\bregion\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex FillHeaderRegex = new(@"\bfill\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex CanonicalModelRegex = new(@"^\s*Model\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex CanonicalInlineStepHeaderRegex = new(@"\bInlineStep\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    // Match the declaration-shaped header first; variant admission belongs in the
    // binder so a misspelled family can never disappear from a Modify block.
    private static readonly Regex CanonicalPerforationHeaderRegex = new(@"\bPerforation\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex CanonicalThreadHeaderRegex = new(@"\bThread\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex CanonicalRecognizeHeaderRegex = new(@"\bRecognize\s+(?<body>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex CanonicalRegionHeaderRegex = new(@"\bRegion\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex CanonicalReplaceHeaderRegex = new(@"\bReplace\s+(?<body>[A-Za-z_][A-Za-z0-9_]*)\.(?<region>[A-Za-z_][A-Za-z0-9_]*)\s+With\s+(?<kind>[A-Za-z_][A-Za-z0-9_]*(?:\s*<\s*[A-Za-z_][A-Za-z0-9_]*\s*>)?)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);

    public static FirmamentV2ParseResult Parse(string sourceText) => Parse(sourceText, null, null);

    public static FirmamentV2ParseResult Parse(string sourceText, string? sourceDirectory) => Parse(sourceText, sourceDirectory, null);

    public static FirmamentV2ParseResult Parse(
        string sourceText,
        string? sourceDirectory,
        FirmamentV2ForgeConceptCatalog? conceptCatalog)
        => Parse(sourceText, sourceDirectory, conceptCatalog, null);

    /// <summary>Carries a selected schema through library/specialization processing;
    /// it cannot override a conflicting authored schema.</summary>
    public static FirmamentV2ParseResult Parse(string sourceText, string? sourceDirectory,
        FirmamentV2ForgeConceptCatalog? conceptCatalog, FirmamentFrontendSchema? expectedSchema,
        IReadOnlyDictionary<string, string>? resources = null)
    {
        using var buildPerfPhase = Aetheris.Kernel.Core.Diagnostics.BuildPerfTrace.Phase("firmament.v2-parse");
        ArgumentNullException.ThrowIfNull(sourceText);
        // BUILD-PERF-M2: one compilation parses the same text several times (an assembly definition
        // four times). The parser is a pure function of its text and directory, and its result is
        // immutable records, except InlineStep, which reads a STEP file; those, and parses with a
        // concept catalog, always run.
        if (resources is not null || conceptCatalog is not null || sourceText.Contains("InlineStep", StringComparison.OrdinalIgnoreCase))
            return ParseWithDirectives(sourceText, sourceDirectory, conceptCatalog, expectedSchema, resources);
        var key = expectedSchema + "\0" + (sourceDirectory ?? string.Empty) + "\0" + sourceText;
        lock (RecentParses)
        {
            var index = RecentParses.FindIndex(entry => string.Equals(entry.Key, key, StringComparison.Ordinal));
            if (index >= 0)
            {
                var hit = RecentParses[index];
                RecentParses.RemoveAt(index);
                RecentParses.Add(hit);
                return hit.Result;
            }
        }
        var result = ParseWithDirectives(sourceText, sourceDirectory, conceptCatalog, expectedSchema);
        lock (RecentParses)
        {
            RecentParses.Add((key, result));
            if (RecentParses.Count > RecentParseCapacity) RecentParses.RemoveAt(0);
        }
        return result;
    }

    private const int RecentParseCapacity = 8;
    private static readonly List<(string Key, FirmamentV2ParseResult Result)> RecentParses = [];

    private static FirmamentV2ParseResult ParseWithDirectives(string source, string? directory, FirmamentV2ForgeConceptCatalog? catalog,
        FirmamentFrontendSchema? expectedSchema, IReadOnlyDictionary<string, string>? resources = null)
    {
        var frontend = FirmamentFrontendSchemas.Select(source);
        if (!frontend.IsSuccess) return frontend.Failure(source);
        if (frontend.Schema is { } authored && expectedSchema is { } expected && authored != expected)
            return FirmamentV2ParseResult.Failure(["firmament-schema-context-conflict"]) with { OriginalSource = source,
                SyntaxDiagnostics = [FirmamentFrontendSchemas.Located(source, "firmament-schema-context-conflict", $"The authored schema {authored} conflicts with the caller's {expected} compilation context.", frontend.DirectiveSpan!)],
                ParserRoute = "UnifiedDirectives", OwnsUnifiedAdmission = true };
        var syntax = FirmamentSyntaxParser.Parse(frontend.Source);
        var pending = FirmamentSyntaxTraversal.Statements(syntax.Document).OfType<DirectiveSyntax>()
            .Where(d => !syntax.Document.Statements.Contains(d) || !ProfileModificationTemplateLibrary.IsImport(d)).ToArray();
        var malformed = syntax.Diagnostics.Where(d => d.Code == "firmament-v2-directive-malformed").ToArray();
        if (pending.Length > 0 || malformed.Length > 0)
        {
            var reported = pending.Select(d => FirmamentFrontendSchemas.Located(source,
                "firmament-v2-directive-unconsumed", $"The {d.Kind.ToString().ToLowerInvariant()} directive '{FirmamentSyntaxValues.Reference(d.Value) ?? source.Substring(d.Value.Span.Start, d.Value.Span.Length)}' is not supported by standalone part compilation. Resolve file includes through the documented Assembly/project loader, or use the registered owner of a shipped library import.", d.Span)).Concat(malformed).ToArray();
            return FirmamentV2ParseResult.Failure(reported.Select(d => d.Code).ToArray()) with
            { OriginalSource = source, SyntaxDiagnostics = reported, ParserRoute = "UnifiedDirectives", OwnsUnifiedAdmission = true };
        }
        var parsed = ParseUncached(frontend.Source, directory, catalog, resources);
        if (parsed.IsSuccess && (frontend.Schema ?? expectedSchema) is { } schema
            && FirmamentFrontendSchemas.DomainMismatch(schema, parsed.BoundSource ?? frontend.Source, admittedSchema: parsed.BoundSchema) is { } mismatch)
            return (frontend with { Diagnostics = [mismatch.Code], LocatedDiagnostics =
                [frontend.DirectiveSpan is { } span ? FirmamentFrontendSchemas.Located(source, mismatch.Code, mismatch.Message, span)
                    : mismatch with { LocationNote = "The schema was supplied by the caller; its original header span is unavailable." }] }).Failure(source);
        return CompleteAdmission(parsed, source);
    }

    private static FirmamentV2ParseResult CompleteAdmission(FirmamentV2ParseResult result, string originalSource)
    {
        if (result.IsSuccess && result.Document is { } document
            && (document.VolumeAssertions?.Count ?? 0) + document.GeometryAssertions.Count == 0)
        {
            var unbound = FirmamentAssertionSyntax.Declarations(originalSource)
                .Concat(FirmamentAssertionSyntax.Declarations(result.DiagnosticSource ?? result.BoundSource ?? originalSource));
            if (unbound.Any())
                result = result with { IsSuccess = false, Disposition = FirmamentV2ParseDisposition.RecognizedInvalid,
                    Diagnostics = [.. result.Diagnostics,
                        $"firmament-v2-assert-owner-unsupported: The {result.ParserRoute} owner does not bind these part assertions. They cannot be ignored; use a qualified mechanical body owner or inspect the geometry separately."] };
        }
        if (result.IsSuccess && result.OwnsUnifiedAdmission && result.BoundSchema is null)
            result = result with { IsSuccess = false, Disposition = FirmamentV2ParseDisposition.RecognizedInvalid,
                Diagnostics = [.. result.Diagnostics, "firmament-v2-admission-domain-missing: The engineering binder did not identify its admitted frontend schema."] };
        if (result.SyntaxDiagnostics.Count > 0)
        {
            var original = FirmamentSyntaxParser.Parse(originalSource);
            var unchanged = result.DiagnosticSource is not null && string.Equals(result.DiagnosticSource,
                MaskCommentTrivia(FirmamentSourceSpelling.Normalize(FirmamentFrontendSchemas.Select(originalSource).Source)), StringComparison.Ordinal);
            var located = result.SyntaxDiagnostics.Where(d => d.IsOriginalSource).ToArray();
            var expanded = result.SyntaxDiagnostics.Where(d => !d.IsOriginalSource).ToArray();
            result = result with { SyntaxDiagnostics = unchanged ? result.SyntaxDiagnostics
                : located.Concat(expanded.Length > 0 && original.Diagnostics.Count > 0 ? original.Diagnostics
                    : expanded.Select(d => d with { SourceSpan = null, Line = null, Column = null,
                        LocationNote = "The error occurs after expansion; original-source provenance is unavailable." })).ToArray() };
        }
        var hasErrors = result.Diagnostics.Any(IsFatalDiagnosticCode);
        return result with
        {
            OriginalSource = originalSource,
            IsSuccess = result.IsSuccess && !hasErrors,
            Disposition = hasErrors && result.Disposition != FirmamentV2ParseDisposition.NotRecognized
                ? FirmamentV2ParseDisposition.RecognizedInvalid : result.Disposition
        };
    }

    private static FirmamentV2ParseResult ParseUncached(
        string sourceText,
        string? sourceDirectory,
        FirmamentV2ForgeConceptCatalog? conceptCatalog, IReadOnlyDictionary<string, string>? resources = null)
    {
        // Serialization roots belong to the historical reader, which still must
        // prove explicit version 1 before execution. Their data values may be V2
        // keywords; those values must not turn a legacy document into V2 source.
        var leading = FirmamentLanguageAnalysisService.Lex(sourceText).Where(token => token.Shape != "comment").Take(2).ToArray();
        if (leading.Length == 2 && (leading[0].Text is "firmament" or "model" && leading[1].Text == ":"
            || leading[0].Text == "{" && leading[1].Shape == "string"))
            return FirmamentV2ParseResult.Failure([], FirmamentV2ParseDisposition.NotRecognized) with { ParserRoute = "LegacySerialization" };
        var authoredSourceText = sourceText;
        sourceText = FirmamentSourceSpelling.Normalize(sourceText);
        var valueSource = MaskCommentTrivia(sourceText);
        sourceText = ProfileModificationTemplateLibrary.ResolveImports(sourceText);
        var diagnostics = new List<string> { "firmament-v2-parser-invoked" };
        var source = MaskCommentTrivia(sourceText);
        var authoredSyntax = FirmamentSyntaxParser.Parse(source);
        if (MechanicalDeclarations(authoredSyntax.Document.Statements).Any(d => d.Type.Name.Text is "SectionChain" or "Loft"
            && !d.Modifiers.Any(m => m.Name.Text == "Template")))
        {
            var section = SectionChainAuthoringParser.Compile(source, materialize: false);
            diagnostics.AddRange(section.Diagnostics);
            var owner = authoredSyntax.Document.Statements.OfType<DeclarationSyntax>().FirstOrDefault(d => d.Type.Name.Text is "Model" or "Struct"
                && !d.Modifiers.Any(m => m.Name.Text is "Concept" or "Template"))?.Name?.Text
                ?? section.Chain?.StableId ?? "SectionChain";
            return section.IsSuccess
                ? FirmamentV2ParseResult.Success(new(owner, "mm", []), diagnostics.Append("firmament-v2-parse-succeeded").ToArray())
                    with { ParserRoute = "UnifiedSectionChain", OwnsUnifiedAdmission = true, BoundSchema = FirmamentFrontendSchema.SectionChain, BoundSectionChain = section, BoundSource = source }
                : FirmamentV2ParseResult.Failure(diagnostics.Append("firmament-v2-parse-failed").ToArray())
                    with { ParserRoute = "UnifiedSectionChain", OwnsUnifiedAdmission = true, SyntaxDiagnostics = authoredSyntax.Diagnostics };
        }
        var polygonExpansion = ClosedBoundary2Authoring.Expand(source, diagnostics);
        if (polygonExpansion is null)
        {
            diagnostics.Add("firmament-v2-parse-failed");
            return FirmamentV2ParseResult.Failure(
                diagnostics.Distinct(StringComparer.Ordinal).Order().ToArray(),
                FirmamentV2ParseDisposition.RecognizedInvalid);
        }
        source = polygonExpansion.Source;
        var pipelineContextDiagnostics = ProfileAuthoringParser.ValidatePipelineContexts(source);
        if (pipelineContextDiagnostics.Count > 0)
        {
            diagnostics.AddRange(pipelineContextDiagnostics);
            diagnostics.Add("firmament-v2-parse-failed");
            return FirmamentV2ParseResult.Failure(
                diagnostics.Distinct(StringComparer.Ordinal).Order().ToArray(),
                FirmamentV2ParseDisposition.RecognizedInvalid);
        }
        var v2AdmissionCandidate = IsV2AdmissionCandidate(source);
        conceptCatalog ??= FirmamentV2ForgeConceptRegistry.Catalog;

        // Modern Template specialization owns typed Record binding.  Run it while
        // Record and scalar Static declarations still exist, then let canonical
        // static authoring erase those compile-time declarations before AIR.
        var compileTime = new FirmamentCompileTimeValues(valueSource);
        var templateExpansion = FirmamentV2TemplateExpansion.Expand(source, diagnostics, compileTime, authoredSourceText);
        if (templateExpansion is null || compileTime.Diagnostics.Any(d => d.Severity is "error" or "fatal"))
        {
            diagnostics.Add("firmament-v2-parse-failed");
            return FirmamentV2ParseResult.Failure(
                diagnostics.Distinct(StringComparer.Ordinal).Order().ToArray(),
                v2AdmissionCandidate ? FirmamentV2ParseDisposition.RecognizedInvalid : FirmamentV2ParseDisposition.NotRecognized)
                with { SyntaxDiagnostics = compileTime.Diagnostics,
                    OwnsUnifiedAdmission = compileTime.Diagnostics.Any(d => d.Severity is "error" or "fatal") };
        }
        source = templateExpansion.Source;
        compileTime.BindExpandedScalars(source, diagnostics);
        // Feature is Firmament's bounded function-like abstraction. It is expanded once,
        // after typed Template specialization and before static pattern lowering, so every command consumes the
        // same concrete semantic declarations and no geometry callback can bypass AIR.
        var featureExpansion = FirmamentV2FeatureExpansion.Expand(source, diagnostics, compileTime, authoredSourceText);
        if (featureExpansion is null || compileTime.Diagnostics.Any(d => d.Severity is "error" or "fatal"))
        {
            diagnostics.Add("firmament-v2-parse-failed");
            return FirmamentV2ParseResult.Failure(
                diagnostics.Distinct(StringComparer.Ordinal).Order().ToArray(),
                FirmamentV2ParseDisposition.RecognizedInvalid) with { SyntaxDiagnostics = compileTime.Diagnostics,
                    OwnsUnifiedAdmission = compileTime.Diagnostics.Any(d => d.Severity is "error" or "fatal") };
        }
        source = featureExpansion.Source;



        var specializedBoundaries = ClosedBoundary2Authoring.Expand(source, diagnostics, compileTime);
        if (specializedBoundaries is null)
        {
            diagnostics.Add("firmament-v2-parse-failed");
            return FirmamentV2ParseResult.Failure(diagnostics.Distinct(StringComparer.Ordinal).Order().ToArray(), FirmamentV2ParseDisposition.RecognizedInvalid);
        }
        source = specializedBoundaries.Source;
        polygonExpansion = new(source, polygonExpansion.Boundaries.Concat(specializedBoundaries.Boundaries
                .Where(boundary => !polygonExpansion.GeneratedDeclarationNames.Contains(boundary.Name, StringComparer.Ordinal)))
            .DistinctBy(boundary => boundary.Name, StringComparer.Ordinal).ToArray());

        var canonicalStaticExpansion = CanonicalStaticAuthoring.Expand(source, diagnostics, compileTime);
        if (canonicalStaticExpansion is null)
        {
            diagnostics.Add("firmament-v2-parse-failed");
            return FirmamentV2ParseResult.Failure(diagnostics.Distinct(StringComparer.Ordinal).Order().ToArray(),
                v2AdmissionCandidate ? FirmamentV2ParseDisposition.RecognizedInvalid : FirmamentV2ParseDisposition.NotRecognized)
                with { SyntaxDiagnostics = compileTime.Diagnostics, OwnsUnifiedAdmission = compileTime.Diagnostics.Any(d => d.Severity is "error" or "fatal") };
        }
        source = canonicalStaticExpansion.Source;
        var concepts = new FirmamentConcepts(source, compileTime);
        concepts.ValidateClaims();
        if (concepts.Diagnostics.Count > 0)
        {
            diagnostics.AddRange(concepts.BindingDiagnostics);
            diagnostics.AddRange(concepts.Diagnostics.Select(d => d.Code + ": " + d.Message));
            return FirmamentV2ParseResult.Failure(diagnostics.Append("firmament-v2-parse-failed").ToArray()) with
            {
                OwnsUnifiedAdmission = true, ParserRoute = "UnifiedConceptBinding",
                SyntaxDiagnostics = concepts.Diagnostics.Select(d =>
                {
                    var original = d.Target is { } name ? compileTime.OriginalDeclaration("Struct", name, "Concept") : null;
                    var unchanged = source == valueSource;
                    return d with { SourceSpan = original ?? (unchanged ? d.SourceSpan : null), IsOriginalSource = original is not null || unchanged,
                        LocationNote = original is not null ? "Location identifies the original Concept Struct declaration; conformance uses specialized values."
                            : unchanged ? d.LocationNote : "Conformance failed after expansion; original-source provenance is unavailable." };
                }).ToArray()
            };
        }
        v2AdmissionCandidate |= IsV2AdmissionCandidate(source);

        var syntax = FirmamentSyntaxParser.Parse(source);
        var domainDeclarations = MechanicalDeclarations(syntax.Document.Statements).ToArray();
        if (domainDeclarations.Any(d => Enum.TryParse<GearFamily>(d.Type.Name.Text, out _)))
        {
            var gear = GearAuthoring.Bind(syntax);
            diagnostics.AddRange(gear.Diagnostics);
            return gear.IsSuccess
                ? FirmamentV2ParseResult.Success(new(gear.ModelName, "mm", []), diagnostics.Append("firmament-v2-parse-succeeded").ToArray())
                    with { ParserRoute = "UnifiedGear", OwnsUnifiedAdmission = true, BoundSchema = FirmamentFrontendSchema.Mechanical, BoundGear = gear, BoundSource = source, SyntaxDiagnostics = syntax.Diagnostics }
                : FirmamentV2ParseResult.Failure(diagnostics.Append("firmament-v2-parse-failed").ToArray())
                    with { ParserRoute = "UnifiedGear", OwnsUnifiedAdmission = true, SyntaxDiagnostics = syntax.Diagnostics };
        }
        if (domainDeclarations.Any(d => d.Type.Name.Text == "Panel" && !d.Modifiers.Any(m => m.Name.Text == "Template")))
        {
            var panelCompilation = FirmamentPanelCompiler.Bind(syntax, templateExpansion.Instantiations);
            diagnostics.AddRange(panelCompilation.Diagnostics);
            diagnostics.Add(panelCompilation.IsSuccess ? "firmament-v2-parse-succeeded" : "firmament-v2-parse-failed");
            if (!panelCompilation.IsSuccess)
                return FirmamentV2ParseResult.Failure(diagnostics.Distinct(StringComparer.Ordinal).Order().ToArray())
                    with { ParserRoute = "UnifiedPanel", OwnsUnifiedAdmission = true, SyntaxDiagnostics = syntax.Diagnostics };
            var panelDocument = new FirmamentV2Document(panelCompilation.ModelName, "mm", [],
                TemplateInstantiations: templateExpansion.Instantiations, Panels: panelCompilation.Panels,
                FeatureDefinitions: featureExpansion.Definitions, FeatureInvocations: featureExpansion.Invocations,
                FeatureExpansion: featureExpansion.Metrics, Polygons: polygonExpansion.Polygons, Boundaries: polygonExpansion.Boundaries);
            return FirmamentV2ParseResult.Success(panelDocument, diagnostics.Distinct(StringComparer.Ordinal).Order().ToArray())
                with { ParserRoute = "UnifiedPanel", OwnsUnifiedAdmission = true, BoundSchema = FirmamentFrontendSchema.Mechanical, BoundSource = source };
        }

        // Canonical V2 is parsed as one document, before compatibility adapters.  The
        // adapters below remain only for old fixtures and no longer define the language
        // an author must select for ordinary mechanical work.
        var modelSyntax = syntax.Document.Statements.OfType<DeclarationSyntax>().FirstOrDefault(d =>
            d.Type.Name.Text is "Model" or "Struct" && !d.Modifiers.Any(m => m.Name.Text is "Template" or "Concept") && d.Body is not null
            && (d.Type.Name.Text == "Model" || MechanicalDeclarations(d.Body.Statements).Any(n => IsPrimitiveDeclaration(n) || n.Type.Name.Text is "Compose" or "Extrude" or "Revolve" or "WireForm" or "WireRoute" or "Sweep" or "SectionChain")));
        var standaloneConstruction = modelSyntax is null && domainDeclarations.Any(d => d.Type.Name.Text is "Extrude" or "Compose");
        if (standaloneConstruction)
        {
            // A wrapper-free construction is the same lexical scope with implicit
            // millimetres. Project the existing statements; do not rewrite source.
            var owner = domainDeclarations.FirstOrDefault(d => d.Type.Name.Text is "Profile" or "Compose")?.Name
                ?? new IdentifierSyntax("StandaloneConstruction", syntax.Document.Span);
            modelSyntax = new(new(new("Struct", owner.Span), [], owner.Span), owner, [], [], null, null, [], null,
                new(syntax.Document.Statements, syntax.Document.Span) { Delimited = false }, false, syntax.Document.Span);
            syntax = syntax with { Document = syntax.Document with { Statements = [modelSyntax] } };
        }
        // Dedicated material domains retain their established semantic owners.
        // This is a decision over material declaration nodes, never a keyword
        // search that lets an unused guide choose the mechanical grammar.
        var rootMaterials = modelSyntax?.Body is null ? [] : MechanicalDeclarations(modelSyntax.Body.Statements).ToArray();
        var hasMechanicalMaterial = rootMaterials.Any(d => IsPrimitiveDeclaration(d) || d.Type.Name.Text is "Compose" or "Extrude");
        var dedicatedMaterialDomain = rootMaterials.Any(d => d.Type.Name.Text is "Revolve" or "WireForm" or "WireRoute" or "Sweep" or "SectionChain");
        if (domainDeclarations.Any(d => d.Type.Name.Text == "Revolve"))
        {
            if (!syntax.IsSuccess)
                return FirmamentV2ParseResult.Failure(diagnostics.Concat(syntax.Diagnostics.Select(d => d.Code)).ToArray())
                    with { ParserRoute = "UnifiedRevolve", OwnsUnifiedAdmission = true, SyntaxDiagnostics = syntax.Diagnostics };
            var revolve = RevolveAuthoringParser.BindExpanded(source, syntax, out var revolveDiagnostics);
            diagnostics.AddRange(revolveDiagnostics);
            if (revolve is null || diagnostics.Any(IsFatalDiagnosticCode))
                return FirmamentV2ParseResult.Failure(diagnostics.Append("firmament-v2-parse-failed").ToArray()) with { ParserRoute = "UnifiedRevolve", OwnsUnifiedAdmission = true };
            return FirmamentV2ParseResult.Success(new(modelSyntax?.Name?.Text ?? revolve.Name, "mm", []), diagnostics.Append("firmament-v2-parse-succeeded").ToArray())
                with { ParserRoute = "UnifiedRevolve", OwnsUnifiedAdmission = true, BoundSchema = FirmamentFrontendSchema.Mechanical, BoundRevolve = revolve, BoundSource = source };
        }
        if (modelSyntax is not null && (hasMechanicalMaterial || !dedicatedMaterialDomain))
        {
            if (!syntax.IsSuccess)
                return FirmamentV2ParseResult.Failure(diagnostics.Concat(syntax.Diagnostics.Select(d => d.Code)).ToArray(), FirmamentV2ParseDisposition.RecognizedInvalid)
                    with { ParserRoute = "UnifiedMechanical", OwnsUnifiedAdmission = true, SyntaxDiagnostics = syntax.Diagnostics };
            var parsed = Recognized(BindMechanicalModel(syntax.Document, modelSyntax, source, diagnostics, sourceDirectory, templateExpansion.Instantiations, compileTime, resources))
                with { DiagnosticSource = source };
            if (parsed.Document is null) return parsed with { ParserRoute = "UnifiedMechanical", OwnsUnifiedAdmission = true };
            if (domainDeclarations.Any(d => d.Type.Name.Text == "Plateau"))
            {
                var plateauDiagnostics = new List<string>();
                var plateau = PlateauAuthoring.Bind(source, plateauDiagnostics);
                if (plateau is null) return FirmamentV2ParseResult.Failure(diagnostics.Concat(plateauDiagnostics).Append("firmament-v2-parse-failed").ToArray())
                    with { ParserRoute = "UnifiedMechanical", OwnsUnifiedAdmission = true };
                parsed = parsed with { BoundPlateau = plateau };
            }
            if (standaloneConstruction) parsed = parsed with { Document = parsed.Document with { ModelName = parsed.BoundProfile?.Name ?? parsed.BoundComposition?.Feature?.Name ?? parsed.Document.ModelName } };
            return WithFeatureMetadata(
                AttachVolumeAssertionsAndSymbols(WithPolygonMetadata(parsed, polygonExpansion), source, canonicalStaticExpansion.Document, diagnostics),
                featureExpansion, compileTime) with { ParserRoute = "UnifiedMechanical", OwnsUnifiedAdmission = true, BoundSchema = FirmamentFrontendSchema.Mechanical, BoundSource = parsed.BoundSource ?? source };
        }

        if (syntax.Document.Statements.OfType<DeclarationSyntax>().Any(d => d.Type.Name.Text is "Model" or "Struct" or "Concept"
            || d.Modifiers.Any(m => m.Name.Text == "Concept")))
            return Recognized(WithFeatureMetadata(
                AttachVolumeAssertionsAndSymbols(WithPolygonMetadata(ParseConceptModelingDocument(RemoveCanonicalVolumeAssertions(source), diagnostics, templateExpansion.Instantiations, compileTime), polygonExpansion), source, canonicalStaticExpansion.Document, diagnostics),
                featureExpansion, compileTime)) with { ParserRoute = "ConceptModeling" };

        if (ContainsRawBackendId(source)) diagnostics.Add(RawBackendIdReferenceForbidden);
        if (ContainsUnsupportedConstruct(source)) diagnostics.Add(UnsupportedConstruct);

        var compatibilityModels = syntax.Document.Statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text == "model").ToArray();
        var compatibilityModel = compatibilityModels.FirstOrDefault();
        if (compatibilityModel?.Body is null) diagnostics.Add(MissingModel);
        if (compatibilityModels.Length > 1) diagnostics.Add(DuplicateName);
        var units = compatibilityModel?.Body?.Statements.OfType<FieldSyntax>().Where(f => FirmamentSyntaxFields.SameField(f.Name.Text, "Units")).ToArray() ?? [];
        if (units.Length == 0) diagnostics.Add(MissingUnits);
        else if (units.Length != 1 || FirmamentSyntaxValues.Reference(units[0].Value) != "mm") diagnostics.Add(CanonicalUnitsInvalid);
        var compatibilityCore = BindCompatibilityScope(syntax.Document, compatibilityModel, diagnostics);
        if (compatibilityCore) diagnostics.AddRange(syntax.Diagnostics.Select(d => d.Code));

        var byName = BindCompatibilitySolids(syntax, compatibilityModel, source, sourceDirectory, diagnostics, resources);
        // A referenced record prototype is compile-time data, not another output
        // body. Preserve legitimate `with` derivation while rejecting unrelated
        // material that the last-body exporter would otherwise discard.
        var prototypes = byName.Values.Select(s => s.DerivedFrom).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var outputs = byName.Keys.Where(name => !prototypes.Contains(name)).ToHashSet(StringComparer.Ordinal);
        if (v2AdmissionCandidate && compatibilityModel?.Body is { } compatibilityBody &&
            RejectMultipleMaterialBodies(compatibilityBody.Statements.OfType<DeclarationSyntax>()
                .Where(d => d.Type.Name.Text == "solid" && d.Name is not null && outputs.Contains(d.Name.Text)).ToArray(),
                compatibilityModel.Name?.Text ?? "<unnamed>", diagnostics) is { } rejectedBodies)
            return rejectedBodies with { ParserRoute = compatibilityCore ? "UnifiedCompatibility" : "CompatibilityExtensions", OwnsUnifiedAdmission = compatibilityCore, BoundSource = source, DiagnosticSource = source };
        var solids = byName.Values.ToList();
        var appearsStandaloneLattice = compatibilityModel?.Body?.Statements.OfType<DeclarationSyntax>().Any(d => d.Type.Name.Text == "fill") == true;
        if (solids.Count == 0 && !appearsStandaloneLattice) diagnostics.Add(MissingSolid);

        var letRecords = ParseLetRecords(source, byName.Keys, diagnostics);
        var lets = ParseLets(source, byName.Keys.Concat(letRecords.Select(r => r.Name)), diagnostics);
        var boundLetRecords = BindLetRecords(letRecords);
        var boundLets = BindLets(lets, boundLetRecords, byName.Keys, diagnostics);

        var modifyBlocks = BindCompatibilityModifies(syntax, compatibilityModel, byName, diagnostics);
        var templates = ParseTemplates(source, diagnostics);
        var latticeFills = ParseLatticeFills(source, byName, diagnostics);
        var standaloneLatticeFills = ParseStandaloneLatticeFills(source, diagnostics);
        var recognizedRegions = ParseRecognizedRegions(source, byName, diagnostics);
        var replacements = ParseReplacements(source, byName, recognizedRegions, diagnostics);
        var (pmi, pmiBlock, boundPmi) = ParsePmi(source, byName, modifyBlocks, recognizedRegions, boundLets, boundLetRecords, diagnostics);
        var (manufacturingConcepts, featureConcepts) = ParseConceptApplications(source, boundLets, boundLetRecords, conceptCatalog, diagnostics);

        FirmamentV2Document? document = null;
        if (compatibilityModel?.Name is not null && units.Length == 1 && (solids.Count > 0 || standaloneLatticeFills.Count > 0) && !diagnostics.Any(d => d.StartsWith("firmament-v2-fill-", StringComparison.Ordinal)))
            document = new FirmamentV2Document(compatibilityModel.Name.Text, "mm", solids, modifyBlocks, templates, pmi, recognizedRegions, replacements, lets, boundLets, letRecords, boundLetRecords, manufacturingConcepts, featureConcepts, pmiBlock, boundPmi, null, latticeFills, standaloneLatticeFills);

        var hasFatalDiagnostics = diagnostics.Any(IsFatalDiagnosticCode);
        diagnostics.Add(document is null || hasFatalDiagnostics ? "firmament-v2-parse-failed" : "firmament-v2-parse-succeeded");
        diagnostics.Sort(StringComparer.Ordinal);
        if (!v2AdmissionCandidate)
            return FirmamentV2ParseResult.Failure(diagnostics, FirmamentV2ParseDisposition.NotRecognized);

        return document is null
            ? FirmamentV2ParseResult.Failure(diagnostics) with { ParserRoute = compatibilityCore ? "UnifiedCompatibility" : "CompatibilityExtensions", OwnsUnifiedAdmission = compatibilityCore, SyntaxDiagnostics = compatibilityCore ? syntax.Diagnostics : [] }
            : new FirmamentV2ParseResult(
                !hasFatalDiagnostics,
                document,
                diagnostics,
                hasFatalDiagnostics ? FirmamentV2ParseDisposition.RecognizedInvalid : FirmamentV2ParseDisposition.RecognizedValid) { ParserRoute = compatibilityCore ? "UnifiedCompatibility" : "CompatibilityExtensions", OwnsUnifiedAdmission = compatibilityCore, BoundSchema = compatibilityCore ? FirmamentFrontendSchema.Mechanical : null, SyntaxDiagnostics = compatibilityCore ? syntax.Diagnostics : [] };
    }

    private static FirmamentV2ParseResult Recognized(FirmamentV2ParseResult result) =>
        result with
        {
            Disposition = result.IsSuccess
                ? FirmamentV2ParseDisposition.RecognizedValid
                : FirmamentV2ParseDisposition.RecognizedInvalid
        };

    private static FirmamentV2ParseResult WithFeatureMetadata(
        FirmamentV2ParseResult result,
        FirmamentV2FeatureExpansion.Result expansion,
        FirmamentCompileTimeValues? values = null)
    {
        if (result.Document is null) return result;
        FirmamentV2FeatureInvocation BindResults(FirmamentV2FeatureInvocation invocation)
        {
            if (invocation.Pattern is null) return invocation;
            var patterns = result.Document.StaticAuthoring?.Patterns?.Where(p => p.Name == invocation.Pattern).ToArray() ?? [];
            if (patterns.Length == 1)
                return invocation with
                {
                    GeneratedResults = patterns[0].GeneratedIds,
                    IdentityKind = patterns[0].Associations is { Count: > 0 } ? "KeyedPattern" : "PositionalPattern"
                };
            var points = result.Document.ConceptIr?.PatternExpansions?
                .Where(p => p.Pattern.EndsWith("::" + invocation.Pattern, StringComparison.Ordinal)).ToArray() ?? [];
            return points.Length == 1
                ? invocation with { GeneratedResults = points[0].GeneratedDeclarations, IdentityKind = "PositionalPattern" }
                : invocation;
        }
        var document = result.Document with
        {
            FeatureDefinitions = expansion.Definitions,
            FeatureInvocations = expansion.Invocations.Select(BindResults).ToArray(),
            FeatureExpansion = expansion.Metrics,
            DataBindings = values?.DataBindings ?? []
        };
        // The geometry/source-map owner must receive authored call spans, never
        // offsets into the expanded text. Generated fields are not source edits.
        document = document with { ModifyBlocks = document.ModifyBlocks?.Select(block => block with
        {
            SemanticHoles = block.SemanticHoles.Select(hole =>
            {
                var application = FirmamentV2FeatureExpansion.ApplicationFor(document, hole.Name);
                return application is null ? hole : hole with
                {
                    SourceSpan = application.InvocationOrigin is { LocationNote: null } origin ? origin.SourceSpan : null,
                    AuthoredFields = null
                };
            }).ToArray()
        }).ToArray() };
        return result with { Document = document };
    }

    private static FirmamentV2ParseResult WithPolygonMetadata(
        FirmamentV2ParseResult result,
        ClosedBoundary2Authoring.Result expansion) =>
        result.Document is null
            ? result
            : result with { Document = result.Document with { Polygons = expansion.Polygons, Boundaries = expansion.Boundaries } };

    internal static bool IsV2AdmissionCandidate(string source)
    {
        // Classification is independent of whether a declaration is well formed.
        // Serialization-shaped V1 uses `model:` / `firmament:` instead.
        var tokens = FirmamentLanguageAnalysisService.Lex(source).Where(token => token.Shape != "comment").ToArray();
        if (tokens.Length >= 2 && (tokens[0].Text is "firmament" or "model" && tokens[1].Text == ":"
            || tokens[0].Text == "{" && tokens[1].Shape == "string")) return false;
        for (var index = 0; index < tokens.Length; index++)
            if (tokens[index].Shape == "identifier"
                && tokens[index].Text is "Model" or "model" or "Struct" or "Assembly" or "Scene" or "Concept" or "Interface" or "Template" or "template" or "Pattern" or "Profile" or "Panel" or "Enum" or "Options"
                && (index + 1 == tokens.Length || tokens[index + 1].Text != ":")) return true;
        return false;
    }

    private static IReadOnlyList<FirmamentV2LetRecordDeclaration> ParseLetRecords(string source, IEnumerable<string> topLevelNames, List<string> diagnostics)
    {
        var records = new List<FirmamentV2LetRecordDeclaration>();
        var names = new HashSet<string>(topLevelNames, StringComparer.Ordinal);
        foreach (Match match in LetRecordHeaderRegex.Matches(source))
        {
            var name = match.Groups["name"].Value;
            var open = source.IndexOf('{', match.Index);
            var close = FindMatchingBrace(source, open);
            if (close < 0) { diagnostics.Add(UnsupportedConstruct); continue; }
            if (names.Contains(name) || ReservedWords.Contains(name)) { diagnostics.Add(LetRecordDuplicateName); continue; }
            names.Add(name);
            var fields = new List<FirmamentV2LetRecordField>();
            var fieldNames = new HashSet<string>(StringComparer.Ordinal);
            var body = source[(open + 1)..close];
            if (LetRecordHeaderRegex.IsMatch(body)) { diagnostics.Add(UnsupportedConstruct); continue; }
            foreach (var rawLine in body.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim();
                if (line.Length == 0) continue;
                var fm = LetRecordFieldRegex.Match(line);
                if (!fm.Success) { diagnostics.Add(LetInvalidLiteral); continue; }
                var fieldName = fm.Groups["name"].Value;
                if (ReservedWords.Contains(fieldName) || !fieldNames.Add(fieldName)) { diagnostics.Add(LetRecordDuplicateField); continue; }
                if (!TryParseLetType(fm.Groups["type"].Value, out var type)) { diagnostics.Add(LetUnknownType); continue; }
                var (valueText, tolerance) = ParseOptionalTolerance(type, fm.Groups["literal"].Value.Trim(), diagnostics);
                var expr = ParseLetValueExpression(type, valueText, diagnostics);
                if (expr is FirmamentV2LiteralExpression) fields.Add(new(fieldName, type, expr, new FirmamentV2SourceSpan(open + 1 + body.IndexOf(rawLine, StringComparison.Ordinal), rawLine.Length), tolerance));
                else if (expr is not null) diagnostics.Add(LetLiteralOnly);
            }
            records.Add(new(name, fields, new FirmamentV2SourceSpan(match.Index, close - match.Index + 1)));
        }
        return records;
    }

    private static IReadOnlyList<FirmamentV2LetDeclaration> ParseLets(string source, IEnumerable<string> topLevelNames, List<string> diagnostics)
    {
        var lets = new List<FirmamentV2LetDeclaration>();
        var names = new HashSet<string>(topLevelNames, StringComparer.Ordinal);
        foreach (Match match in LetRegex.Matches(source))
        {
            var name = match.Groups["name"].Value;
            if (names.Contains(name) || ReservedWords.Contains(name)) { diagnostics.Add(LetDuplicateName); continue; }
            names.Add(name);
            if (!TryParseLetType(match.Groups["type"].Value, out var type)) { diagnostics.Add(LetUnknownType); continue; }
            var literal = match.Groups["literal"].Value.Trim();
            var (valueText, tolerance) = ParseOptionalTolerance(type, literal, diagnostics);
            var value = ParseLetValueExpression(type, valueText, diagnostics);
            if (value is not null) lets.Add(new FirmamentV2LetDeclaration(name, type, value, new FirmamentV2SourceSpan(match.Index, match.Length), tolerance));
        }
        return lets;
    }

    internal static bool TryParseLetType(string text, out FirmamentV2PrimitiveType type)
    {
        type = text switch
        {
            "int" or "Int" => FirmamentV2PrimitiveType.Int,
            "float" or "Float" => FirmamentV2PrimitiveType.Float,
            "length" or "Length" => FirmamentV2PrimitiveType.Length,
            "angle" or "Angle" => FirmamentV2PrimitiveType.Angle,
            "time" or "Time" => FirmamentV2PrimitiveType.Time,
            "string" or "String" => FirmamentV2PrimitiveType.String,
            "bool" or "Bool" => FirmamentV2PrimitiveType.Bool,
            _ => default
        };
        return text is "Time" or "time" or "int" or "float" or "length" or "angle" or "string" or "bool" or "Int" or "Float" or "Length" or "Angle" or "String" or "Bool";
    }

    private static FirmamentV2ValueExpression? ParseLetValueExpression(FirmamentV2PrimitiveType type, string literal, List<string> diagnostics)
    {
        if (literal.Contains(" tol ", StringComparison.Ordinal) || literal.Contains(" if ", StringComparison.Ordinal) || literal.StartsWith("if ", StringComparison.Ordinal) || Regex.IsMatch(literal, @"^[A-Za-z_][A-Za-z0-9_]*\s*\(", RegexOptions.CultureInvariant))
        { diagnostics.Add(literal.Contains("tol", StringComparison.Ordinal) ? ToleranceUnsupported : ExpressionUnsupported); return null; }
        if (literal.IndexOfAny(['+', '-', '*', '/', '(', ')']) >= 0)
        {
            var parser = new LetExpressionParser(literal, diagnostics);
            return parser.Parse();
        }
        var refMatch = DottedReferenceRegex.Match(literal);
        if (refMatch.Success) return new FirmamentV2DottedReferenceExpression(refMatch.Groups["record"].Value, refMatch.Groups["field"].Value, literal);
        if (Regex.IsMatch(literal, @"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant) && type is not FirmamentV2PrimitiveType.Bool) return new FirmamentV2IdentifierReferenceExpression(literal, literal);
        var value = ParseLetLiteral(type, literal, diagnostics);
        return value is null ? null : new FirmamentV2LiteralExpression(value);
    }

    private static (string ValueText, FirmamentV2Tolerance? Tolerance) ParseOptionalTolerance(FirmamentV2PrimitiveType type, string text, List<string> diagnostics)
    {
        if (Regex.IsMatch(text, @"\([^)]*\s+tol\s+", RegexOptions.CultureInvariant))
        {
            diagnostics.Add(ToleranceUnsupported);
            return (text, null);
        }
        var match = Regex.Match(text, @"^(?<value>.+?)\s+tol\s+(?<tol>.+)$", RegexOptions.CultureInvariant);
        if (!match.Success) return (text, null);
        if (type is not (FirmamentV2PrimitiveType.Length or FirmamentV2PrimitiveType.Angle)) diagnostics.Add(ToleranceInvalidType);
        var tolerance = ParseTolerance(type, match.Groups["tol"].Value.Trim(), diagnostics);
        return (match.Groups["value"].Value.Trim(), tolerance);
    }

    private static FirmamentV2Tolerance? ParseTolerance(FirmamentV2PrimitiveType type, string text, List<string> diagnostics)
    {
        var expectedUnit = type == FirmamentV2PrimitiveType.Length ? "mm" : type == FirmamentV2PrimitiveType.Angle ? "deg" : null;
        var bilateral = Regex.Match(text, @"^(?<num>[-+]?\d+(?:\.\d+)?)(?<unit>[A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.CultureInvariant);
        if (bilateral.Success && !text.StartsWith('+'))
        {
            var unit = bilateral.Groups["unit"].Value;
            if (expectedUnit is not null && unit != expectedUnit) diagnostics.Add(ToleranceUnitMismatch);
            var plus = double.Parse(bilateral.Groups["num"].Value, CultureInfo.InvariantCulture);
            if (plus < 0) diagnostics.Add(ToleranceNegativeBilateral);
            return plus < 0 ? null : new(FirmamentV2ToleranceKind.Bilateral, plus, plus, unit, type, new(0, text.Length));
        }
        var asymmetric = Regex.Match(text, @"^\+(?<plus>\d+(?:\.\d+)?)(?<plusUnit>[A-Za-z_][A-Za-z0-9_]*)\s+-(?<minus>\d+(?:\.\d+)?)(?<minusUnit>[A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.CultureInvariant);
        if (asymmetric.Success)
        {
            var plusUnit = asymmetric.Groups["plusUnit"].Value; var minusUnit = asymmetric.Groups["minusUnit"].Value;
            if (plusUnit != minusUnit || (expectedUnit is not null && plusUnit != expectedUnit)) diagnostics.Add(ToleranceUnitMismatch);
            return new(FirmamentV2ToleranceKind.Asymmetric, double.Parse(asymmetric.Groups["plus"].Value, CultureInfo.InvariantCulture), double.Parse(asymmetric.Groups["minus"].Value, CultureInfo.InvariantCulture), plusUnit, type, new(0, text.Length));
        }
        if (text.StartsWith('+')) diagnostics.Add(ToleranceMissingMinus);
        else if (text.StartsWith('-')) diagnostics.Add(ToleranceMissingPlus);
        else diagnostics.Add(ToleranceInvalidLiteral);
        return null;
    }

    private static FirmamentV2LiteralValue? ParseLetLiteral(FirmamentV2PrimitiveType type, string literal, List<string> diagnostics)
    {
        if (literal.Contains('/') || Regex.IsMatch(literal, @"^[A-Za-z_][A-Za-z0-9_]*\b", RegexOptions.CultureInvariant) && type is not FirmamentV2PrimitiveType.Bool)
        { diagnostics.Add(LetLiteralOnly); return null; }
        var m = Regex.Match(literal, @"^(?<num>[-+]?\d+(?:\.\d+)?)(?<unit>[A-Za-z_][A-Za-z0-9_]*)?$", RegexOptions.CultureInvariant);
        switch (type)
        {
            case FirmamentV2PrimitiveType.Int:
                if (!Regex.IsMatch(literal, @"^[-+]?\d+$", RegexOptions.CultureInvariant)) { diagnostics.Add(LetTypeMismatch); return null; }
                return new(type, int.Parse(literal, CultureInfo.InvariantCulture), int.Parse(literal, CultureInfo.InvariantCulture), null, literal);
            case FirmamentV2PrimitiveType.Float:
                if (!m.Success) { diagnostics.Add(LetInvalidLiteral); return null; }
                if (m.Groups["unit"].Success) { diagnostics.Add(LetUnitMismatch); return null; }
                var f = double.Parse(m.Groups["num"].Value, CultureInfo.InvariantCulture);
                return new(type, f, f, null, literal);
            case FirmamentV2PrimitiveType.Length:
                if (!m.Success) { diagnostics.Add(LetInvalidLiteral); return null; }
                if (m.Groups["unit"].Value != "mm") { diagnostics.Add(m.Groups["unit"].Success ? LetUnitMismatch : LetTypeMismatch); return null; }
                var length = double.Parse(m.Groups["num"].Value, CultureInfo.InvariantCulture);
                return new(type, length, length, "mm", literal);
            case FirmamentV2PrimitiveType.Angle:
                if (!m.Success) { diagnostics.Add(LetInvalidLiteral); return null; }
                if (m.Groups["unit"].Value != "deg") { diagnostics.Add(m.Groups["unit"].Success ? LetUnitMismatch : LetTypeMismatch); return null; }
                var angle = double.Parse(m.Groups["num"].Value, CultureInfo.InvariantCulture);
                return new(type, angle, angle, "deg", literal);
            case FirmamentV2PrimitiveType.Time:
                if (!FirmamentNumericEvaluation.TryEvaluate(literal, out var time) || time.Unit != "s") { diagnostics.Add(LetUnitMismatch); return null; }
                return new(type, time.Number, time.Number, "s", literal);
            case FirmamentV2PrimitiveType.String:
                if (!Regex.IsMatch(literal, "^\\\"[^\\\"]*\\\"$", RegexOptions.CultureInvariant)) { diagnostics.Add(LetInvalidLiteral); return null; }
                return new(type, literal[1..^1], null, null, literal);
            case FirmamentV2PrimitiveType.Bool:
                if (literal is not ("true" or "false")) { diagnostics.Add(LetInvalidLiteral); return null; }
                return new(type, literal == "true", null, null, literal);
            default: return null;
        }
    }

    private static IReadOnlyList<FirmamentV2BoundLetRecord> BindLetRecords(IReadOnlyList<FirmamentV2LetRecordDeclaration> records) =>
        records.Select(r => new FirmamentV2BoundLetRecord(r.Name, r.Fields.Where(f => f.ValueExpression is FirmamentV2LiteralExpression).Select(f => new FirmamentV2BoundLet(f.Name, f.DeclaredType, f.LiteralValue, f.SourceSpan, new FirmamentV2BoundExpression(f.DeclaredType, f.LiteralValue, new HashSet<string>(StringComparer.Ordinal), f.SourceSpan, f.Tolerance), new HashSet<string>(StringComparer.Ordinal), f.Tolerance)).ToDictionary(f => f.Name, StringComparer.Ordinal), r.SourceSpan)).ToArray();

    private static IReadOnlyList<FirmamentV2BoundLet> BindLets(IReadOnlyList<FirmamentV2LetDeclaration> lets, IReadOnlyList<FirmamentV2BoundLetRecord> records, IEnumerable<string> solidNames, List<string> diagnostics,
        List<FirmamentV2ValidationDiagnostic>? located = null)
    {
        var binder = new ExpressionBinder(lets, records, solidNames, diagnostics, located);
        return binder.BindLets();
    }

    private sealed class LetExpressionParser(string source, List<string> diagnostics)
    {
        private int _pos;
        public FirmamentV2ValueExpression? Parse()
        {
            var expr = ParseAdditive();
            Skip();
            if (expr is null || _pos != source.Length) { diagnostics.Add(ExpressionUnsupported); return null; }
            return expr;
        }
        private FirmamentV2ValueExpression? ParseAdditive()
        {
            var left = ParseMultiplicative();
            while (true)
            {
                Skip();
                if (!Match('+') && !Match('-')) return left;
                var op = source[_pos - 1].ToString();
                var right = ParseMultiplicative();
                if (left is null || right is null) return null;
                left = new FirmamentV2BinaryExpression(left, op, right, source);
            }
        }
        private FirmamentV2ValueExpression? ParseMultiplicative()
        {
            var left = ParsePrimary();
            while (true)
            {
                Skip();
                if (!Match('*') && !Match('/')) return left;
                var op = source[_pos - 1].ToString();
                var right = ParsePrimary();
                if (left is null || right is null) return null;
                left = new FirmamentV2BinaryExpression(left, op, right, source);
            }
        }
        private FirmamentV2ValueExpression? ParsePrimary()
        {
            Skip();
            if (Match('('))
            {
                var e = ParseAdditive();
                Skip();
                if (!Match(')')) diagnostics.Add(ExpressionUnsupported);
                return e;
            }
            var start = _pos;
            while (_pos < source.Length && !char.IsWhiteSpace(source[_pos]) && "+-*/()".IndexOf(source[_pos]) < 0) _pos++;
            if (_pos == start) { diagnostics.Add(ExpressionUnsupported); return null; }
            var token = source[start.._pos];
            var dm = DottedReferenceRegex.Match(token);
            if (dm.Success) return new FirmamentV2DottedReferenceExpression(dm.Groups["record"].Value, dm.Groups["field"].Value, token);
            if (Regex.IsMatch(token, @"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)) return new FirmamentV2IdentifierReferenceExpression(token, token);
            var literal = ParseLetLiteral(TokenType(token), token, diagnostics);
            return literal is null ? null : new FirmamentV2LiteralExpression(literal);
        }
        private FirmamentV2PrimitiveType TokenType(string token) => token.EndsWith("mm", StringComparison.Ordinal) ? FirmamentV2PrimitiveType.Length : token.EndsWith("deg", StringComparison.Ordinal) ? FirmamentV2PrimitiveType.Angle : token.Contains('.', StringComparison.Ordinal) ? FirmamentV2PrimitiveType.Float : FirmamentV2PrimitiveType.Int;
        private void Skip() { while (_pos < source.Length && char.IsWhiteSpace(source[_pos])) _pos++; }
        private bool Match(char c) { if (_pos >= source.Length || source[_pos] != c) return false; _pos++; return true; }
    }

    private sealed class ExpressionBinder
    {
        private readonly IReadOnlyList<FirmamentV2LetDeclaration> _lets;
        private readonly Dictionary<string, FirmamentV2BoundLetRecord> _records;
        private readonly HashSet<string> _solidNames;
        private readonly List<string> _diagnostics;
        private readonly Dictionary<string, FirmamentV2LetDeclaration> _letMap;
        private readonly Dictionary<string, FirmamentV2BoundLet> _bound = new(StringComparer.Ordinal);
        private readonly HashSet<string> _visiting = new(StringComparer.Ordinal);
        private readonly List<FirmamentV2ValidationDiagnostic>? _located;
        public ExpressionBinder(IReadOnlyList<FirmamentV2LetDeclaration> lets, IReadOnlyList<FirmamentV2BoundLetRecord> records, IEnumerable<string> solidNames, List<string> diagnostics,
            List<FirmamentV2ValidationDiagnostic>? located = null)
        { _lets = lets; _records = records.ToDictionary(r => r.Name, StringComparer.Ordinal); _solidNames = new(solidNames, StringComparer.Ordinal); _diagnostics = diagnostics; _letMap = lets.ToDictionary(l => l.Name, StringComparer.Ordinal); _located = located; }
        public IReadOnlyList<FirmamentV2BoundLet> BindLets() { foreach (var l in _lets) Bind(l.Name); return _lets.Where(l => _bound.ContainsKey(l.Name)).Select(l => _bound[l.Name]).ToArray(); }
        private FirmamentV2BoundLet? Bind(string name)
        {
            if (_bound.TryGetValue(name, out var existing)) return existing;
            if (!_letMap.TryGetValue(name, out var let)) return null;
            if (!_visiting.Add(name)) { _diagnostics.Add(ExpressionCycle); return null; }
            var expr = Eval(let.ValueExpression);
            _visiting.Remove(name);
            if (expr is null) return null;
            if (expr.InferredType != let.DeclaredType)
            {
                _diagnostics.Add(ExpressionTypeMismatch); _diagnostics.Add(LetTypeMismatch);
                _located?.Add(new(ExpressionTypeMismatch, "error", $"Compile-time value '{let.Name}' requires {let.DeclaredType}, but its expression has type {expr.InferredType}.", Target: let.Name)
                    { SourceSpan = let.SourceSpan, Stage = "Binding" });
                return null;
            }
            var tolerance = let.Tolerance ?? expr.AliasTolerance;
            if (tolerance is null && expr.UsesTolerancedValueInArithmetic) _diagnostics.Add(ToleranceDroppedThroughArithmetic);
            return _bound[name] = new FirmamentV2BoundLet(let.Name, let.DeclaredType, expr.Value, let.SourceSpan, expr, expr.Dependencies, tolerance);
        }
        private FirmamentV2BoundExpression? Eval(FirmamentV2ValueExpression e) => e switch
        {
            FirmamentV2LiteralExpression l => new(l.Value.Type, l.Value, new HashSet<string>(StringComparer.Ordinal), new(0, 0)),
            FirmamentV2IdentifierReferenceExpression r => EvalId(r),
            FirmamentV2DottedReferenceExpression r => EvalDotted(r),
            FirmamentV2BinaryExpression b => EvalBinary(b),
            FirmamentV2UnaryExpression u => EvalNot(u),
            _ => null
        };
        private FirmamentV2BoundExpression? EvalNot(FirmamentV2UnaryExpression unary)
        {
            var operand = Eval(unary.Operand);
            if (operand is null) return null;
            if (unary.Operator != "!" || operand.InferredType != FirmamentV2PrimitiveType.Bool) { _diagnostics.Add(ExpressionInvalidOperator); return null; }
            return operand with { Value = new(FirmamentV2PrimitiveType.Bool, !(bool)operand.Value.Value), AliasTolerance = null };
        }
        private FirmamentV2BoundExpression? EvalId(FirmamentV2IdentifierReferenceExpression r)
        {
            if (_records.ContainsKey(r.Name)) { _diagnostics.Add(ExpressionRecordUsedAsValue); _diagnostics.Add(LetReferenceRecordUsedAsValue); return null; }
            var b = Bind(r.Name);
            if (b is null) { _diagnostics.Add(ExpressionUnknownSymbol); return null; }
            return new(b.Type, b.Value, new HashSet<string>((b.Dependencies ?? new HashSet<string>()).Append(r.Name), StringComparer.Ordinal), b.SourceSpan, b.Tolerance);
        }
        private FirmamentV2BoundExpression? EvalDotted(FirmamentV2DottedReferenceExpression r)
        {
            if (_solidNames.Contains(r.RecordName) || _letMap.ContainsKey(r.RecordName)) { _diagnostics.Add(ExpressionScalarUsedAsRecord); _diagnostics.Add(LetReferenceNonRecord); return null; }
            if (!_records.TryGetValue(r.RecordName, out var record)) { _diagnostics.Add(ExpressionUnknownRecord); _diagnostics.Add(LetReferenceUnknownRecord); return null; }
            if (!record.Fields.TryGetValue(r.FieldName, out var f)) { _diagnostics.Add(ExpressionUnknownField); _diagnostics.Add(LetReferenceUnknownField); return null; }
            return new(f.Type, f.Value, new HashSet<string> { $"{r.RecordName}.{r.FieldName}" }, f.SourceSpan, f.Tolerance);
        }
        private FirmamentV2BoundExpression? EvalBinary(FirmamentV2BinaryExpression b)
        {
            var l = Eval(b.Left); var r = Eval(b.Right); if (l is null || r is null) return null;
            var type = ResultType(l.InferredType, b.Operator, r.InferredType);
            if (type is null) { _diagnostics.Add(ExpressionInvalidOperator); return null; }
            if (b.Operator == "/" && IsZero(r.Value)) { _diagnostics.Add(ExpressionDivisionByZero); return null; }
            var val = Compute(type.Value, l.Value, b.Operator, r.Value, b.Source);
            var arithmetic = b.Operator is "+" or "-" or "*" or "/";
            return new(type.Value, val, new HashSet<string>(l.Dependencies.Concat(r.Dependencies), StringComparer.Ordinal), new(0, b.Source.Length), null,
                arithmetic && (l.AliasTolerance is not null || r.AliasTolerance is not null || l.UsesTolerancedValueInArithmetic || r.UsesTolerancedValueInArithmetic));
        }
        private static bool IsZero(FirmamentV2LiteralValue v) => v.NumericValue is double d && d == 0d;
        internal static FirmamentV2PrimitiveType? ResultType(FirmamentV2PrimitiveType a, string op, FirmamentV2PrimitiveType b) =>
            op is "&&" or "||" ? a == FirmamentV2PrimitiveType.Bool && b == a ? FirmamentV2PrimitiveType.Bool : null
            : op is "==" or "!=" ? a == b ? FirmamentV2PrimitiveType.Bool : null
            : op is "<" or "<=" or ">" or ">=" ? a == b && a is FirmamentV2PrimitiveType.Int or FirmamentV2PrimitiveType.Float or FirmamentV2PrimitiveType.Length or FirmamentV2PrimitiveType.Angle or FirmamentV2PrimitiveType.Time ? FirmamentV2PrimitiveType.Bool : null
            : FirmamentNumericEvaluation.ResultType(a, op, b);
        private static FirmamentV2LiteralValue Compute(FirmamentV2PrimitiveType type, FirmamentV2LiteralValue l, string op, FirmamentV2LiteralValue r, string raw)
        {
            if (type == FirmamentV2PrimitiveType.Bool)
            {
                var order = l.NumericValue is { } ln && r.NumericValue is { } rn ? ln.CompareTo(rn) : Equals(l.Value, r.Value) ? 0 : 1;
                var result = op switch { "&&" => (bool)l.Value && (bool)r.Value, "||" => (bool)l.Value || (bool)r.Value,
                    "==" => order == 0, "!=" => order != 0, "<" => order < 0, "<=" => order <= 0, ">" => order > 0, _ => order >= 0 };
                return new(type, result, Raw: raw);
            }
            var a = Convert.ToDouble(l.NumericValue, CultureInfo.InvariantCulture); var b = Convert.ToDouble(r.NumericValue, CultureInfo.InvariantCulture);
            var operation = op switch { "+" => BinaryOperator.Add, "-" => BinaryOperator.Subtract, "*" => BinaryOperator.Multiply, "/" => BinaryOperator.Divide, _ => throw new InvalidOperationException("Unbound arithmetic operator.") };
            if (!FirmamentNumericEvaluation.TryBinary(new(a, l.Unit ?? ""), operation, new(b, r.Unit ?? ""), out var computed))
                return new(type, double.NaN, double.NaN, Raw: raw);
            var d = computed.Number;
            object value = type == FirmamentV2PrimitiveType.Int ? (object)(int)d : d;
            return new(type, value, d, type == FirmamentV2PrimitiveType.Length ? "mm" : type == FirmamentV2PrimitiveType.Angle ? "deg" : type == FirmamentV2PrimitiveType.Time ? "s" : null, raw);
        }
    }

    private static FirmamentV2SolidBinding? ParseInlineStep(string name, string body, List<string> diagnostics, string? sourceDirectory,
        IReadOnlyDictionary<string, string>? resources = null)
    {
        var match = Regex.Match(body, "\\b(?:path|Path)\\s*:\\s*\\\"(?<path>[^\\\"]+)\\\"", RegexOptions.CultureInvariant);
        if (!match.Success) { diagnostics.Add(InlineStepPathMissing); return null; }

        var sourcePath = match.Groups["path"].Value;
        if (Path.IsPathRooted(sourcePath))
        {
            diagnostics.Add(InlineStepPathInvalid);
            return null;
        }

        string normalizedPath;
        string stepText;
        if (resources is not null)
        {
            // Explicit browser/project snapshots never fall through to the host filesystem.
            try { normalizedPath = Assembly.FirmamentProjectSnapshot.NormalizePath((sourceDirectory is { Length: > 0 } ? sourceDirectory + "/" : "") + sourcePath); }
            catch (ArgumentException) { diagnostics.Add(InlineStepPathInvalid); return null; }
            if (!resources.TryGetValue(normalizedPath, out stepText!)) { diagnostics.Add(InlineStepFileMissing); return null; }
        }
        else
        {
            var baseDirectory = string.IsNullOrWhiteSpace(sourceDirectory) ? Directory.GetCurrentDirectory() : sourceDirectory;
            normalizedPath = Path.GetFullPath(Path.Combine(baseDirectory!, sourcePath));
            if (!File.Exists(normalizedPath)) { diagnostics.Add(InlineStepFileMissing); return null; }
            stepText = File.ReadAllText(normalizedPath, Encoding.UTF8);
        }
        if (!IsAetherisCanonicalStep(stepText, out var evidence))
        {
            diagnostics.Add(InlineStepRequiresCanonical);
            return null;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stepText))).ToLowerInvariant();
        return new FirmamentV2SolidBinding(name, "InlineStep", new FirmamentV2InlineStepRecord(sourcePath, normalizedPath, hash, true, evidence, BuildImportedStepTopologyMap(stepText)) { SourceText = stepText });
    }

    private static ImportedStepTopologyMap BuildImportedStepTopologyMap(string stepText)
    {
        var faceEntityToFaceId = new Dictionary<string, string>(StringComparer.Ordinal);
        var faceIdToFaceEntity = new Dictionary<string, string>(StringComparer.Ordinal);
        var imported = Aetheris.Kernel.Core.Step242.Step242Importer.ImportBody(stepText);
        foreach (var binding in imported.IsSuccess ? imported.Value.Bindings.FaceBindings : [])
        {
            if (binding.SourceStepEntityId is not { } sourceEntity) continue;
            var entity = "#" + sourceEntity;
            var faceId = $"face-{binding.FaceId.Value}";
            faceEntityToFaceId[entity] = faceId;
            faceIdToFaceEntity[faceId] = entity;
        }
        return new ImportedStepTopologyMap(faceEntityToFaceId, faceIdToFaceEntity);
    }

    private static bool IsAetherisCanonicalStep(string stepText, out string evidence)
    {
        if (stepText.Contains("Aetheris AP242 subset export", StringComparison.Ordinal)
            && stepText.Contains("Aetheris.Kernel", StringComparison.Ordinal)
            && stepText.Contains("FILE_SCHEMA(('AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF'))", StringComparison.Ordinal))
        {
            evidence = "aetheris-ap242-subset-export-header";
            return true;
        }

        evidence = string.Empty;
        return false;
    }

    /// <summary>
    /// Brace-depth extraction retained by legacy semantic adapters. Mechanical
    /// document and statement admission is owned by the unified syntax tree.
    /// </summary>
    private static int BraceDepth(string source, int start, int end)
    {
        var depth = 0;
        var quoted = false;
        for (var index = start; index < end; index++)
        {
            if (source[index] == '"' && (index == 0 || source[index - 1] != '\\')) quoted = !quoted;
            if (quoted) continue;
            if (source[index] == '{') depth++;
            else if (source[index] == '}') depth--;
        }
        return depth;
    }

    /// <summary>
    /// Parser-owned canonical admission for advanced declarations.  The existing
    /// production profile and section-stack readers remain semantic adapters; this
    /// method owns the root, units, disposition, and normalized declaration list.
    /// </summary>
    private static FirmamentV2ParseResult BindMechanicalConstruction(string source, DeclarationSyntax model, List<string> diagnostics, IReadOnlyList<ConceptIrTemplateInstantiation>? templateInstantiations, FirmamentCompileTimeValues? compileTime = null)
    {
        source = MechanicalConstructionSource(source, model, diagnostics);
        if (diagnostics.Any(IsFatalDiagnosticCode)) return CanonicalFailure(diagnostics, CanonicalDocumentMalformed);
        // Re-index the existing AST after local source transport; semantic binding
        // is already complete and is not delegated to another parser route.
        var projectedSyntax = FirmamentSyntaxParser.Parse(source).Document;
        model = model.Body!.Delimited
            ? FirmamentSyntaxTraversal.Declarations(projectedSyntax)
                .First(d => d.Span.Start == model.Span.Start && d.Type.Name.Text == model.Type.Name.Text && d.Name?.Text == model.Name?.Text)
            : model with { Span = projectedSyntax.Span, Body = model.Body with { Statements = projectedSyntax.Statements, Span = projectedSyntax.Span } };
        var bodySpan = model.Body!.ContentSpan;
        var body = source.Substring(bodySpan.Start, bodySpan.Length);
        var units = model.Body.Statements.OfType<FieldSyntax>().Where(f => FirmamentSyntaxFields.SameField(f.Name.Text, "Units")).ToArray();
        if (units.Length == 0 && model.Type.Name.Text != "Struct" || units.Any(f => FirmamentSyntaxValues.Reference(f.Value) != "mm"))
            return CanonicalFailure(diagnostics, CanonicalUnitsInvalid);
        var adapterSource = source[..model.Span.Start] + body + source[(model.Span.Start + model.Span.Length)..];
        var declarations = MechanicalDeclarations(model.Body.Statements).ToArray();
        if (TryParseExactCoaxialTemplateDocument(adapterSource, model.Name!.Text, templateInstantiations, diagnostics, out var exactDocument))
            return FirmamentV2ParseResult.Success(exactDocument!, diagnostics.Append("firmament-v2-exact-coaxial-template-parsed").Append("firmament-v2-parse-succeeded").Distinct(StringComparer.Ordinal).Order().ToArray());

        if (declarations.Any(d => d.Type.Name.Text == "Compose"))
        {
            var composition = PrismaticProfileCompositionParser.Parse(adapterSource);
            if (composition.Feature is null)
            {
                diagnostics.AddRange(composition.Diagnostics);
                return CanonicalFailure(diagnostics, CanonicalDocumentMalformed);
            }
            var feature = composition.Feature;
            var compositionSelectionDiagnostics = new List<string>();
            var compositionSelections = ParseCanonicalSelections(body, bodySpan.Start, compositionSelectionDiagnostics);
            ValidateCanonicalSelectionSources(body, compositionSelections, composition.Profiles.Keys, compositionSelectionDiagnostics);
            if (compositionSelectionDiagnostics.Count > 0)
            {
                diagnostics.AddRange(compositionSelectionDiagnostics);
                return CanonicalFailure(diagnostics, CanonicalDocumentMalformed);
            }
            var compositionSolids = new Dictionary<string, FirmamentV2SolidBinding>(StringComparer.Ordinal)
            {
                [feature.Name] = new FirmamentV2SolidBinding(feature.Name, "Compose", new FirmamentV2AdvancedMaterialRecord("Compose"))
            };
            var (compositionPmi, compositionPmiBlock, compositionBoundPmi) = ParsePmi(
                source, compositionSolids, [], [], [], [], diagnostics, (feature.ShaftHoles ?? []).Select(hole => hole.Name).Concat((feature.CounterboreHoles ?? []).Select(hole => hole.Name)));
            if (Regex.IsMatch(body, @"\bEdgeFinish\b", RegexOptions.CultureInvariant)
                && !ProfileBoundaryChamferSourceBinder.HasSemanticProfileBoundaryFinish(body)
                && composition.Profiles.Values.Any(profile => profile.Loops.Single().Segments.Count != 4))
            {
                diagnostics.Add($"{EdgeFinishProfileComposeBoundaryUnsupported}:host={feature.Name}:construction=Compose:face=+Z:target=Boundary:supported=primitive-box-or-explicit-semantic-chain:Profile/Compose polygon-boundary materialization is not implemented.");
                return CanonicalFailure(diagnostics, EdgeFinishProfileComposeBoundaryUnsupported);
            }
            if (diagnostics.Any(IsFatalDiagnosticCode)) return CanonicalFailure(diagnostics, CanonicalDocumentMalformed);
            var document = new FirmamentV2Document(
                model.Name!.Text,
                "mm",
                [new FirmamentV2SolidBinding(feature.Name, "Compose", new FirmamentV2AdvancedMaterialRecord("Compose"))],
                Profiles: composition.Profiles.Values.Select(profile => new FirmamentV2ProfileDecl(profile.Name, profile.EffectiveConstructionPlane.StableId, profile.LocalStartDepth ?? 0d, profile.LocalEndDepth ?? 0d, new(0, 0))).ToArray(),
                Composes: [new FirmamentV2ComposeDecl(feature.Name, feature.Operations.Select(operation => operation.Name).ToArray(), new(0, body.Length))],
                Bosses: (feature.Bosses ?? []).Select(item => new FirmamentV2BossDecl(item.Name, item.Host, item.SupportFace, item.ProfileReference, item.Height, item.StableId, new(0, body.Length))).ToArray(),
                Pockets: (feature.Pockets ?? []).Select(item => new FirmamentV2PocketDecl(item.Name, item.Host, item.SupportFace, item.ProfileReference, item.Depth, item.HostThickness, item.RemainingFloor, item.MinimumFloorThickness, item.MinimumFloorPolicySource, item.StableId, new(0, body.Length))).ToArray(),
                Templates: ParseTemplates(body, diagnostics),
                Pmi: compositionPmi,
                PmiBlock: compositionPmiBlock,
                BoundPmi: compositionBoundPmi,
                Selections: compositionSelections,
                TemplateInstantiations: templateInstantiations);
            diagnostics.Add("firmament-v2-unified-canonical-advanced-parsed");
            diagnostics.Add("firmament-v2-profile-compose-adapted");
            diagnostics.Add("firmament-v2-parse-succeeded");
            diagnostics.Sort(StringComparer.Ordinal);
            return FirmamentV2ParseResult.Success(document, diagnostics.Distinct(StringComparer.Ordinal).ToArray()) with { BoundComposition = composition, BoundSource = adapterSource };
        }

        if (declarations.Any(d => d.Type.Name.Text == "Extrude"))
        {
            var profile = ProfileAuthoringParser.Parse(adapterSource);
            if (profile.Profile is null)
            {
                diagnostics.AddRange(profile.Diagnostics);
                return CanonicalFailure(diagnostics, CanonicalDocumentMalformed);
            }
            var resolved = profile.Profile;
            var profileSelectionDiagnostics = new List<string>();
            var profileSelections = ParseCanonicalSelections(body, bodySpan.Start, profileSelectionDiagnostics);
            ValidateCanonicalSelectionSources(body, profileSelections, [resolved.Name], profileSelectionDiagnostics);
            if (profileSelectionDiagnostics.Count > 0)
            {
                diagnostics.AddRange(profileSelectionDiagnostics);
                return CanonicalFailure(diagnostics, CanonicalDocumentMalformed);
            }
            var profileSolids = new Dictionary<string, FirmamentV2SolidBinding>(StringComparer.Ordinal)
            {
                [resolved.Name] = new FirmamentV2SolidBinding(resolved.Name, "Profile", new FirmamentV2AdvancedMaterialRecord("Profile"))
            };
            var (profilePmi, profilePmiBlock, profileBoundPmi) = ParsePmi(source, profileSolids, [], [], [], [], diagnostics);
            if (Regex.IsMatch(body, @"\bEdgeFinish\b", RegexOptions.CultureInvariant)
                && !ProfileBoundaryChamferSourceBinder.HasSemanticProfileBoundaryFinish(body)
                && resolved.Loops.Single().Segments.Count != 4)
            {
                diagnostics.Add($"{EdgeFinishProfileComposeBoundaryUnsupported}:host={resolved.Name}:construction=Profile:face=+Z:target=Boundary:supported=primitive-box-or-explicit-semantic-chain:Profile/Compose polygon-boundary materialization is not implemented.");
                return CanonicalFailure(diagnostics, EdgeFinishProfileComposeBoundaryUnsupported);
            }
            if (diagnostics.Any(IsFatalDiagnosticCode)) return CanonicalFailure(diagnostics, CanonicalDocumentMalformed);
            var document = new FirmamentV2Document(
                model.Name!.Text,
                "mm",
                [new FirmamentV2SolidBinding(resolved.Name, "Profile", new FirmamentV2AdvancedMaterialRecord("Profile"))],
                Profiles: [new FirmamentV2ProfileDecl(resolved.Name, resolved.EffectiveConstructionPlane.StableId, resolved.LocalStartDepth ?? 0d, resolved.LocalEndDepth ?? profile.Height, new(0, body.Length))],
                Pmi: profilePmi,
                PmiBlock: profilePmiBlock,
                BoundPmi: profileBoundPmi,
                Selections: profileSelections,
                TemplateInstantiations: templateInstantiations);
            diagnostics.Add("firmament-v2-unified-canonical-advanced-parsed");
            diagnostics.Add("firmament-v2-profile-adapted");
            diagnostics.Add("firmament-v2-parse-succeeded");
            diagnostics.Sort(StringComparer.Ordinal);
            return FirmamentV2ParseResult.Success(document, diagnostics.Distinct(StringComparer.Ordinal).ToArray()) with { BoundProfile = resolved, ExtrusionHeight = profile.Height, BoundSource = adapterSource };
        }

        var concept = ParseConceptModelingDocument(declarations.Any(d => d.Type.Name.Text == "Struct" && !d.Modifiers.Any(m => m.Name.Text == "Concept")) ? adapterSource : source, diagnostics, templateInstantiations, compileTime);
        if (!concept.IsSuccess || concept.Document is null) return concept;
        var conceptSolids = concept.Document.Solids.ToDictionary(solid => solid.Name, StringComparer.Ordinal);
        var (conceptPmi, conceptPmiBlock, conceptBoundPmi) = ParsePmi(
            source,
            conceptSolids,
            concept.Document.ModifyBlocks ?? [],
            concept.Document.RecognizedRegions ?? [],
            concept.Document.BoundLets ?? [],
            concept.Document.BoundLetRecords ?? [],
            diagnostics);
        if (diagnostics.Any(IsFatalDiagnosticCode)) return CanonicalFailure(diagnostics, CanonicalDocumentMalformed);
        var conceptSelectionDiagnostics = new List<string>();
        var conceptSelections = ParseCanonicalSelections(body, bodySpan.Start, conceptSelectionDiagnostics);
        ValidateCanonicalSelectionSources(body, conceptSelections, [], conceptSelectionDiagnostics);
        if (conceptSelectionDiagnostics.Count > 0)
        {
            diagnostics.AddRange(conceptSelectionDiagnostics);
            return CanonicalFailure(diagnostics, CanonicalDocumentMalformed);
        }
        var normalized = concept.Document with
        {
            ModelName = model.Name!.Text,
            Selections = conceptSelections,
            Pmi = conceptPmi,
            PmiBlock = conceptPmiBlock,
            BoundPmi = conceptBoundPmi
        };
        diagnostics.Add("firmament-v2-unified-canonical-advanced-parsed");
        diagnostics.Sort(StringComparer.Ordinal);
        return FirmamentV2ParseResult.Success(normalized, diagnostics.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static bool TryParseExactCoaxialTemplateDocument(string source, string modelName, IReadOnlyList<ConceptIrTemplateInstantiation>? templateInstantiations,
        List<string> diagnostics, out FirmamentV2Document? document)
    {
        document = null;
        var primitive = Regex.Match(source, @"\bExactCoaxialPart\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant);
        if (!primitive.Success) return false;
        var open = source.IndexOf('{', primitive.Index); var close = FindMatchingBrace(source, open);
        var materialized = Regex.Match(source, @"\bStruct\s+(?<name>[A-Za-z_]\w*)\s*:\s*(?<concept>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant);
        if (close < 0 || !materialized.Success) { diagnostics.Add(CanonicalPrimitiveMalformed); return false; }
        if (ReadExactCoaxialTemplateRecipe(source[(open + 1)..close]) is not FirmamentV2ExactCoaxialPartRecord recipe)
        { diagnostics.Add(CanonicalPrimitiveMalformed); return false; }

        static string TextValue(string value) => value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;
        var instanceName = materialized.Groups["name"].Value; var conceptName = materialized.Groups["concept"].Value;
        var conceptSpan = new FirmamentV2SourceSpan(materialized.Index, 0);
        var definition = new FirmamentConcepts(source).Contracts.SingleOrDefault(d => d.Name == conceptName);
        if (definition is null) { diagnostics.Add(ConceptUnknownConcept); return false; }
        var requirements = definition.Members;
        var materializedOpen = source.IndexOf('{', materialized.Index); var materializedClose = FindMatchingBrace(source, materializedOpen);
        var materializedBody = source[(materializedOpen + 1)..materializedClose];
        var expose = Regex.Match(materializedBody, @"\bExpose\s*\{(?<body>.*?)\}", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        if (!expose.Success) { diagnostics.Add(ConceptIrResolver.MissingMember + ":" + instanceName); return false; }
        var exposedSources = Regex.Matches(expose.Groups["body"].Value, @"(?m)^\s*(?<name>[A-Za-z_]\w*)\s*:\s*(?<value>[^\r\n}]+?)\s*$", RegexOptions.CultureInvariant)
            .Cast<Match>().ToDictionary(match => match.Groups["name"].Value, match => match.Groups["value"].Value.Trim(), StringComparer.Ordinal);
        var values = new List<ConceptIrValue>(); var exposed = new List<ConceptIrSemanticMember>();
        foreach (var requirement in requirements.Values)
        {
            if (!exposedSources.TryGetValue(requirement.Name, out var authored)) { diagnostics.Add(ConceptIrResolver.MissingMember + $":{instanceName}.{requirement.Name}"); return false; }
            var stableId = $"template:{instanceName}.{requirement.Name}"; ConceptIrValue? value = requirement.Type.Name switch
            {
                "Length" when Regex.Match(authored, @"^(?<n>[-+]?\d+(?:\.\d+)?)mm$") is { Success: true } length => new ConceptIrLengthValue(stableId, double.Parse(length.Groups["n"].Value, CultureInfo.InvariantCulture), "mm", authored),
                "Angle" when Regex.Match(authored, @"^(?<n>[-+]?\d+(?:\.\d+)?)deg$") is { Success: true } angle => new ConceptIrAngleValue(stableId, double.Parse(angle.Groups["n"].Value, CultureInfo.InvariantCulture), "deg", authored),
                "String" when authored.Length >= 2 && authored[0] == '"' && authored[^1] == '"' => new ConceptIrStringValue(stableId, TextValue(authored), authored),
                "Axis" when authored.EndsWith(".Axis", StringComparison.Ordinal) || authored == "+X" => new ConceptIrAxisValue(stableId, new(0d, 0d, 0d), new(1d, 0d, 0d), authored),
                _ => null
            };
            if (value is null) { diagnostics.Add(ConceptIrResolver.TypeMismatch + $":{instanceName}.{requirement.Name}:expected-{requirement.Type}:actual-{authored}"); return false; }
            values.Add(value);
            exposed.Add(new(requirement.Name, requirement.Type, value, null, ConceptIrSemanticPhase.ConceptIr,
                ConceptIrMaterializationCategory.CompileTimeValue, value.Provenance, conceptSpan, $"materialized:{instanceName}.exposed:{requirement.Name}"));
        }
        var ir = new ConceptIrDocument(
            [new ConceptIrDefinition(conceptName, requirements, conceptSpan)], [], values,
            new(instanceName, "Struct", [conceptName], new(materialized.Index, materializedClose - materialized.Index + 1), exposed, "Valid"), [],
            TemplateInstantiations: templateInstantiations ?? []);
        document = new FirmamentV2Document(modelName, "mm", [new FirmamentV2SolidBinding(primitive.Groups["name"].Value, "ExactCoaxialPart", recipe)], ConceptIr: ir);
        return true;
    }

    private static IReadOnlyList<FirmamentV2SelectionDecl> ParseCanonicalSelections(string body, int offset, List<string> diagnostics)
    {
        var selections = new List<FirmamentV2SelectionDecl>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var syntax = FirmamentSyntaxParser.Parse(body);
        foreach (var declaration in FirmamentSyntaxTraversal.Declarations(syntax.Document).Where(d => d.Type.Name.Text == "Selection"))
        {
            var name = declaration.Name?.Text ?? "";
            if (!names.Add(name)) { diagnostics.Add($"{SelectionDuplicateName}:{name}"); continue; }
            var fields = new FirmamentSyntaxFields(declaration, diagnostics);
            var target = fields.Word("Target");
            var sources = fields.TakeAll("Source");
            var requirement = fields.Word("Require");
            fields.Complete();
            if (name.Length == 0 || sources.Count == 0 || requirement is null)
            { diagnostics.Add($"{SelectionMalformed}:{name}"); continue; }
            var sourceTexts = sources.Select(f => body.Substring(f.Value.Span.Start, f.Value.Span.Length)).ToArray();
            selections.Add(new(name, target ?? "ProfileBoundary", sourceTexts[0], requirement,
                new(offset + declaration.Span.Start, declaration.Span.Length)) { Sources = sourceTexts });
        }
        return selections;
    }

    private static string RemoveCanonicalVolumeAssertions(string source) => FirmamentAssertionSyntax.Mask(source);

    private static FirmamentV2ParseResult AttachVolumeAssertionsAndSymbols(FirmamentV2ParseResult parsed, string source, FirmamentV2StaticAuthoringDocument? staticAuthoring, List<string> diagnostics)
    {
        if (parsed.Document is null) return parsed;
        var threads = ParseCanonicalThreads(source, diagnostics);
        if (threads is null)
            return FirmamentV2ParseResult.Failure(parsed.Diagnostics.Concat(diagnostics).Distinct(StringComparer.Ordinal).Order().ToArray(), FirmamentV2ParseDisposition.RecognizedInvalid);
        var assertions = FirmamentAssertionSyntax.Parse(source, diagnostics);
        var normalized = parsed.Document with { StaticAuthoring = staticAuthoring, VolumeAssertions = assertions.Volume, GeometryAssertions = assertions.Geometry, Threads = threads };
        normalized = BindSemanticConstraintProjections(normalized, diagnostics);
        if (diagnostics.Any(IsFatalDiagnosticCode))
            return FirmamentV2ParseResult.Failure(parsed.Diagnostics.Concat(diagnostics).Distinct(StringComparer.Ordinal).Order().ToArray(), FirmamentV2ParseDisposition.RecognizedInvalid);
        var symbolBinding = FirmamentV2CanonicalSymbolBinder.Bind(normalized, source);
        if (symbolBinding.Diagnostics.Count > 0)
            return FirmamentV2ParseResult.Failure(parsed.Diagnostics.Concat(symbolBinding.Diagnostics).Distinct(StringComparer.Ordinal).Order().ToArray(), FirmamentV2ParseDisposition.RecognizedInvalid);
        return parsed with
        {
            Document = normalized with { SymbolTable = symbolBinding.Table },
            Diagnostics = parsed.Diagnostics.Append("firmament-v2-unified-canonical-symbols-bound").Distinct(StringComparer.Ordinal).Order().ToArray()
        };
    }

    private static FirmamentV2Document BindSemanticConstraintProjections(FirmamentV2Document document, List<string> diagnostics)
    {
        var staticAuthoring = document.StaticAuthoring;
        if (staticAuthoring?.SemanticConstraints is not { Count: > 0 }) return document;
        var holes = (document.ModifyBlocks ?? []).SelectMany(block => block.SemanticHoles).ToDictionary(hole => hole.Name, StringComparer.Ordinal);
        var constraints = new List<FirmamentV2SemanticConstraint>();
        foreach (var constraint in staticAuthoring.SemanticConstraints)
        {
            var hasHole = holes.TryGetValue(constraint.Subject, out var hole);
            var valid = constraint.Property == "Diameter" && hasHole;
            if (!valid) diagnostics.Add(SemanticConstraintUnsupported + ":" + constraint.Id);
            else
            {
                var actual = hole!.ShaftDiameter;
                var delta = actual - constraint.NominalValue.NumericValue!.Value;
                var admitted = delta >= 0d ? constraint.Tolerance?.Plus ?? 0d : constraint.Tolerance?.Minus ?? 0d;
                valid = Math.Abs(delta) <= admitted + 1e-9;
                if (!valid) diagnostics.Add(SemanticConstraintValidationFailed + ":" + constraint.Id);
            }
            constraints.Add(constraint with { ValidationSucceeded = valid });
        }
        var byId = constraints.ToDictionary(constraint => constraint.Id, StringComparer.Ordinal);
        var projections = staticAuthoring.PmiProjections ?? new Dictionary<string, FirmamentV2PmiProjection>(StringComparer.Ordinal);
        if (projections.Count == 0) return document with { StaticAuthoring = staticAuthoring with { SemanticConstraints = constraints } };
        var records = (document.PmiBlock?.Records ?? []).Select(record => projections.TryGetValue(record.Name, out var projection)
            ? record with { Projection = projection }
            : record).ToArray();
        foreach (var projection in projections)
            if (!byId.TryGetValue(projection.Value.SourceRequireId, out var constraint) || !constraint.ValidationSucceeded)
                diagnostics.Add(SemanticConstraintValidationFailed + ":" + projection.Value.SourceRequireId);
        FirmamentV2BoundPmiBlock? bound = document.BoundPmi;
        if (bound is not null)
        {
            FirmamentV2BoundPmiRecord Tag(FirmamentV2BoundPmiRecord record) => projections.TryGetValue(record.Name, out var projection)
                ? record with { ProjectionSource = projection.SourceRequireId } : record;
            bound = new(bound.Datums.Select(Tag).ToArray(), bound.Dimensions.Select(Tag).ToArray(), bound.Controls.Select(Tag).ToArray(), bound.Diagnostics);
        }
        return document with { StaticAuthoring = staticAuthoring with { SemanticConstraints = constraints }, PmiBlock = document.PmiBlock is null ? null : document.PmiBlock with { Records = records }, BoundPmi = bound };
    }

    private static void ValidateCanonicalSelectionSources(string body, IReadOnlyList<FirmamentV2SelectionDecl> selections,
        IEnumerable<string> profileNames, List<string> diagnostics)
    {
        var profiles = profileNames.ToHashSet(StringComparer.Ordinal);
        var declarations = FirmamentSyntaxTraversal.Declarations(FirmamentSyntaxParser.Parse(body).Document).ToArray();
        foreach (var selection in selections)
        {
            // Result categories retain the existing semantic contract. Source syntax
            // is bound structurally, including the admitted profile member-set form.
            if (selection.Target is not ("ProfileBoundary" or "TopBoundary" or "BottomBoundary" or "SideBoundary" or "HoleEntry" or "HoleExit" or "HoleWall" or "ShaftToDrillPoint" or "DrillPoint" or "Tip" or "SlotEntry" or "SlotExit" or "SlotWall" or "SlotStraightWall" or "SlotEndWall"))
                diagnostics.Add($"{SelectionResultKindInvalid}:{selection.Name}:{selection.Target}");
            foreach (var text in selection.Sources ?? [selection.Source])
            {
                var expression = FirmamentSyntaxParser.ParseExpression(text).Expression;
                string? owner = null; string? kind = null;
                if (expression is CallSyntax { Target: ReferenceSyntax r, Arguments.Count: 1 } call && r.Name.Text is "Slot" or "Hole")
                { kind = r.Name.Text; owner = FirmamentSyntaxValues.Reference(call.Arguments[0].Value); }
                else
                {
                    var target = expression switch { MemberSetSyntax set => set.Target, CallSyntax invocation => invocation.Target, _ => expression };
                    while (target is MemberSyntax member) { owner = FirmamentSyntaxValues.Reference(member.Target); target = member.Target; }
                    kind = "Profile";
                }
                var found = owner is not null && (kind == "Profile" ? profiles.Contains(owner) : declarations.Any(d => d.Type.Name.Text == kind && d.Name?.Text == owner));
                if (!found) diagnostics.Add($"{SelectionUnknownSource}:{selection.Name}:{owner}");
            }
        }
    }

    /// <summary>Extracts an already admitted canonical advanced body for the
    /// production materializer boundary.  It is intentionally not a second parser.</summary>
    public static bool TryGetCanonicalAdvancedBody(string source, out string body)
    {
        var syntax = FirmamentSyntaxParser.Parse(source);
        var model = syntax.Document.Statements.OfType<DeclarationSyntax>().FirstOrDefault(d => d.Type.Name.Text == "Model" && !d.Modifiers.Any(m => m.Name.Text == "Template") && d.Body is not null);
        body = model?.Body is { } block ? source[..model.Span.Start] + source.Substring(block.ContentSpan.Start, block.ContentSpan.Length) + source[(model.Span.Start + model.Span.Length)..] : string.Empty;
        return model?.Body is not null;
    }

    // Source provenance is read from lexical field boundaries, independently of the legacy
    // recognition expressions. A duplicate or unbound field is never a rewrite target.
    internal static IReadOnlyList<FirmamentV2AuthoredField> ReadAuthoredFields(string body, int baseOffset)
    {
        var result = new List<FirmamentV2AuthoredField>();
        string? name = null;
        var declarationStart = -1;
        var valueStart = -1;
        var valueEnd = -1;
        var depth = 0;
        void Finish()
        {
            if (name is not null && valueEnd >= valueStart)
                result.Add(new FirmamentV2AuthoredField(name,
                    new FirmamentV2SourceSpan(baseOffset + declarationStart, valueEnd - declarationStart + 1),
                    new FirmamentV2SourceSpan(baseOffset + valueStart, valueEnd - valueStart + 1)));
        }
        for (var i = 0; i < body.Length; i++)
        {
            if (body[i] == '/' && i + 1 < body.Length && body[i + 1] == '/')
            {
                i = body.IndexOf('\n', i + 2) is var lineEnd && lineEnd >= 0 ? lineEnd : body.Length;
                continue;
            }
            if (body[i] == '/' && i + 1 < body.Length && body[i + 1] == '*')
            {
                var commentEnd = body.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = commentEnd >= 0 ? commentEnd + 1 : body.Length;
                continue;
            }
            if (body[i] == '"')
            {
                var start = i++;
                while (i < body.Length && (body[i] != '"' || body[i - 1] == '\\')) i++;
                if (name is not null) valueEnd = Math.Min(i, body.Length - 1);
                continue;
            }
            if (depth == 0 && (char.IsLetter(body[i]) || body[i] == '_'))
            {
                var start = i;
                while (i + 1 < body.Length && (char.IsLetterOrDigit(body[i + 1]) || body[i + 1] == '_')) i++;
                var afterName = i + 1;
                while (afterName < body.Length && char.IsWhiteSpace(body[afterName])) afterName++;
                if (afterName < body.Length && body[afterName] == ':')
                {
                    Finish();
                    name = body[start..(i + 1)];
                    declarationStart = start;
                    valueStart = afterName + 1;
                    while (valueStart < body.Length && char.IsWhiteSpace(body[valueStart])) valueStart++;
                    valueEnd = valueStart - 1;
                    i = afterName;
                    continue;
                }
            }
            if (body[i] is '(' or '[' or '{') depth++;
            else if (body[i] is ')' or ']' or '}') depth--;
            if (name is not null && !char.IsWhiteSpace(body[i]) && body[i] != ';') valueEnd = i;
        }
        Finish();
        return result;
    }

    private static IReadOnlyList<FirmamentV2ThreadDecl>? ParseCanonicalThreads(string source, List<string> diagnostics)
    {
        var model = CanonicalModelRegex.Match(source);
        var rootOpen = model.Success ? source.IndexOf('{', model.Index) : -1;
        var rootClose = rootOpen >= 0 ? FindMatchingBrace(source, rootOpen) : -1;
        if (rootClose < 0) return [];
        var threads = new List<FirmamentV2ThreadDecl>();
        foreach (Match header in CanonicalThreadHeaderRegex.Matches(source))
        {
            if (header.Index <= rootOpen || header.Index >= rootClose || BraceDepth(source, rootOpen + 1, header.Index) != 0) continue;
            var open = source.IndexOf('{', header.Index);
            var close = FindMatchingBrace(source, open);
            if (close < 0 || close > rootClose) { diagnostics.Add("firmament-v2-thread-malformed"); return null; }
            var body = source[(open + 1)..close];
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match field in Regex.Matches(body, @"(?m)^\s*(?<name>[A-Za-z_]\w*)\s*:\s*(?<value>[^\r\n}]+)", RegexOptions.CultureInvariant))
            {
                var name = field.Groups["name"].Value;
                if (name is not ("Surface" or "MajorDiameter" or "Pitch" or "Length" or "StartOffset" or "Hand") ||
                    !fields.TryAdd(name, field.Groups["value"].Value.Trim().TrimEnd(';').Trim()))
                { diagnostics.Add("firmament-v2-thread-field-invalid:" + name); return null; }
            }
            if (fields.Count == 0 || !fields.TryGetValue("Surface", out var support) ||
                !fields.TryGetValue("MajorDiameter", out var major) ||
                !fields.TryGetValue("Pitch", out var pitch) ||
                !fields.TryGetValue("Length", out var length))
            { diagnostics.Add("firmament-v2-thread-required-fields-missing"); return null; }
            var selector = Regex.Match(support, @"^face\(\s*(?<solid>[A-Za-z_]\w*)\.(?<role>[A-Za-z_+][A-Za-z0-9_+\-]*)\s*\)$", RegexOptions.CultureInvariant);
            if (!selector.Success ||
                !TryLength(major, out var majorMm) || !TryLength(pitch, out var pitchMm) ||
                !TryLength(length, out var lengthMm) ||
                (fields.TryGetValue("StartOffset", out var offset) && !TryLength(offset, out _)))
            { diagnostics.Add("firmament-v2-thread-field-value-invalid"); return null; }
            var startOffset = fields.TryGetValue("StartOffset", out var start)
                ? double.Parse(start[..^2], CultureInfo.InvariantCulture) : (double?)null;
            if (fields.TryGetValue("Hand", out var hand) && hand != "Right")
            { diagnostics.Add("firmament-v2-thread-hand-unsupported:" + hand); return null; }
            if (threads.Any(thread => thread.Name == header.Groups["name"].Value))
            { diagnostics.Add("firmament-v2-thread-name-duplicate:" + header.Groups["name"].Value); return null; }
            threads.Add(new(header.Groups["name"].Value, selector.Groups["solid"].Value,
                selector.Groups["role"].Value, majorMm, pitchMm, lengthMm, startOffset,
                fields.TryGetValue("Hand", out var authoredHand) ? authoredHand : "Right",
                new FirmamentV2SourceSpan(header.Index, close - header.Index + 1)));
        }
        return threads;

        static bool TryLength(string text, out double value)
        {
            var match = Regex.Match(text, @"^(?<number>[-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)mm$", RegexOptions.CultureInvariant);
            return double.TryParse(match.Groups["number"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && match.Success && double.IsFinite(value);
        }
    }

    private static FirmamentV2ExactCoaxialPartRecord? ReadExactCoaxialTemplateRecipe(string body)
    {
        var matches = Regex.Matches(body, "\\b(?<name>[A-Za-z_]\\w*)\\s*:\\s*(?<value>\"[^\"]*\"|[A-Za-z_]\\w*|[-+]?\\d+(?:\\.\\d+)?(?:mm|deg)?)", RegexOptions.CultureInvariant).Cast<Match>().ToArray();
        var fields = matches.GroupBy(match => match.Groups["name"].Value, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Last().Groups["value"].Value, StringComparer.Ordinal);
        var required = new[] { "StableId", "Sides", "AcrossFlats", "AxialHeight", "FlatDiameter", "ConeAngle", "BlendRadius", "ShaftDiameter", "AxialLength", "EndChamferLength", "EndDiameter", "SemanticAxialRegionLength", "Designation", "Grade" };
        return required.All(fields.ContainsKey) ? new(fields) : null;
    }

    private static IReadOnlyList<FirmamentV2PerforationDecl> ParseCanonicalPerforations(string body, int bodyOffset, List<string> diagnostics)
    {
        var result = new List<FirmamentV2PerforationDecl>();
        var headers = CanonicalPerforationHeaderRegex.Matches(body);
        if (Regex.Matches(body, @"\bPerforation\s+[A-Za-z_]", RegexOptions.CultureInvariant).Count != headers.Count)
            diagnostics.Add(CanonicalModifyMalformed);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match header in headers)
        {
            var open = body.IndexOf('{', header.Index);
            var close = FindMatchingBrace(body, open);
            if (close < 0 || !names.Add(header.Groups["name"].Value)) { diagnostics.Add(CanonicalModifyMalformed); continue; }
            var featureBody = body[(open + 1)..close];
            if (CanonicalBodyHasUnknownField(featureBody, ["On", "Diameter", "Layout", "Pitch", "Margin", "MinimumLigament", "OffsetX", "OffsetY", "CircumferentialPitch", "MarginTop", "MarginBottom", "StartAngle"]))
            { diagnostics.Add(CanonicalFieldUnknown); continue; }
            string? ReadWord(string name) => Regex.Match(featureBody, $@"\b{name}\s*:\s*(?<value>[A-Za-z_+\-][A-Za-z_0-9+\-]*)\b", RegexOptions.CultureInvariant) is { Success: true } match ? match.Groups["value"].Value : null;
            double ReadLength(string name, double fallback)
            {
                var match = Regex.Match(featureBody, $@"\b{name}\s*:\s*(?<value>[-+0-9.eE]+)mm\b", RegexOptions.CultureInvariant);
                return !match.Success
                    ? Regex.IsMatch(featureBody, $@"\b{name}\s*:", RegexOptions.CultureInvariant) ? double.NaN : fallback
                    : double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : double.NaN;
            }
            var on = ReadWord("On"); var layout = ReadWord("Layout");
            var diameter = ReadLength("Diameter", double.NaN); var pitch = ReadLength("Pitch", double.NaN);
            var margin = ReadLength("Margin", 0); var ligament = ReadLength("MinimumLigament", 0);
            var offsetX = ReadLength("OffsetX", 0); var offsetY = ReadLength("OffsetY", 0);
            var circumferentialPitch = ReadLength("CircumferentialPitch", double.NaN);
            var marginTop = ReadLength("MarginTop", double.NaN); var marginBottom = ReadLength("MarginBottom", double.NaN);
            var startAngleMatch = Regex.Match(featureBody, @"\bStartAngle\s*:\s*(?<value>[-+0-9.eE]+)deg\b", RegexOptions.CultureInvariant);
            var startAngle = !startAngleMatch.Success ? 0d : double.TryParse(startAngleMatch.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var degrees) ? degrees : double.NaN;
            if (on is null || layout is null || !double.IsFinite(diameter) || diameter <= 0 || !double.IsFinite(pitch) || pitch <= 0 || !double.IsFinite(margin) || margin < 0 || !double.IsFinite(ligament) || ligament < 0 || !double.IsFinite(offsetX) || !double.IsFinite(offsetY))
            { diagnostics.Add("perforation-field-invalid"); continue; }
            if (Regex.IsMatch(featureBody, @"\bStartAngle\s*:", RegexOptions.CultureInvariant) && !startAngleMatch.Success || !double.IsFinite(startAngle))
            { diagnostics.Add("perforation-field-invalid"); continue; }
            if (new[] { "CircumferentialPitch", "MarginTop", "MarginBottom" }.Any(name =>
                    Regex.IsMatch(featureBody, $@"\b{name}\s*:", RegexOptions.CultureInvariant) &&
                    double.IsNaN(name switch { "CircumferentialPitch" => circumferentialPitch, "MarginTop" => marginTop, _ => marginBottom })))
            { diagnostics.Add("perforation-field-invalid"); continue; }
            result.Add(new(header.Groups["name"].Value, on, layout, diameter, pitch, margin, ligament, offsetX, offsetY,
                new FirmamentV2SourceSpan(bodyOffset + header.Index, close - header.Index + 1),
                double.IsNaN(circumferentialPitch) ? null : circumferentialPitch,
                double.IsNaN(marginTop) ? null : marginTop,
                double.IsNaN(marginBottom) ? null : marginBottom, startAngle));
        }
        return result;
    }

    private static bool CanonicalBodyHasUnknownField(string body, IReadOnlyCollection<string> knownFields) =>
        ReadAuthoredFields(body, 0).Any(field => !knownFields.Contains(field.Name, StringComparer.Ordinal));

    // Canonical semantic STEP declarations bind directly to the normalized V2 nodes
    // used by the compatibility parser.  FaceRefs deliberately remain STEP entities
    // downstream; the public canonical surface accepts analysis-facing ordinals.
    private static IReadOnlyList<FirmamentV2RecognizedRegion> ParseCanonicalRecognizedRegions(string source, Dictionary<string, FirmamentV2SolidBinding> solids, List<string> diagnostics)
    {
        var regions = new List<FirmamentV2RecognizedRegion>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match recognize in CanonicalRecognizeHeaderRegex.Matches(source))
        {
            var bodyName = recognize.Groups["body"].Value;
            if (!solids.TryGetValue(bodyName, out var solid) || solid.InlineStep is null) { diagnostics.Add(UnknownRecognitionBody); continue; }
            var open = source.IndexOf('{', recognize.Index); var close = FindMatchingBrace(source, open);
            if (close < 0) { diagnostics.Add(CanonicalDeclarationUnrecognized); continue; }
            var recognizeBody = source[(open + 1)..close];
            foreach (Match region in CanonicalRegionHeaderRegex.Matches(recognizeBody))
            {
                var name = region.Groups["name"].Value;
                if (!seen.Add($"{bodyName}.{name}")) { diagnostics.Add(DuplicateRegion); continue; }
                var regionOpen = recognizeBody.IndexOf('{', region.Index); var regionClose = FindMatchingBrace(recognizeBody, regionOpen);
                if (regionClose < 0) { diagnostics.Add(CanonicalDeclarationUnrecognized); continue; }
                var regionBody = recognizeBody[(regionOpen + 1)..regionClose];
                var kindMatch = Regex.Match(regionBody, @"\bKind\s*:\s*(?<value>[A-Za-z_][A-Za-z0-9_]*(?:\s*<\s*[A-Za-z_][A-Za-z0-9_]*\s*>)?)", RegexOptions.CultureInvariant);
                var kind = kindMatch.Success ? NormalizeRecognitionKind(kindMatch.Groups["value"].Value) : string.Empty;
                if (kind is not ("datumPlane" or "holeShaft")) { diagnostics.Add(InvalidRecognitionKind); continue; }
                var confidenceMatch = Regex.Match(regionBody, @"\bConfidence\s*:\s*(?<value>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant);
                var confidence = confidenceMatch.Success ? confidenceMatch.Groups["value"].Value.ToLowerInvariant() : "medium";
                if (confidence is not ("low" or "medium" or "high" or "certain")) { diagnostics.Add(InvalidRecognitionConfidence); continue; }
                var faces = ParseCanonicalFaceReferences(regionBody, solid.InlineStep.TopologyMap, diagnostics);
                if (faces.Count == 0) continue;
                var evidence = ParseCanonicalRecognitionEvidence(regionBody, diagnostics);
                regions.Add(new FirmamentV2RecognizedRegion(bodyName, name, kind, faces, confidence, evidence));
            }
        }
        return regions;
    }

    private static IReadOnlyList<string> ParseCanonicalFaceReferences(string regionBody, ImportedStepTopologyMap topologyMap, List<string> diagnostics)
    {
        var faces = new List<string>();
        var friendly = Regex.Match(regionBody, @"\bFaces\s*:\s*\[(?<faces>[^\]]*)\]", RegexOptions.CultureInvariant | RegexOptions.Singleline);
        var raw = Regex.Match(regionBody, @"\bStepFaceEntities\s*:\s*\[(?<faces>[^\]]*)\]", RegexOptions.CultureInvariant | RegexOptions.Singleline);
        if (friendly.Success && raw.Success) { diagnostics.Add(UnknownRecognitionFace); return []; }
        if (friendly.Success)
        {
            foreach (var token in friendly.Groups["faces"].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var faceId) || faceId <= 0 || !topologyMap.TryResolveSequentialFaceId(faceId, out var entity)) { diagnostics.Add(UnknownRecognitionFace); return []; }
                faces.Add(entity);
            }
        }
        else if (raw.Success)
        {
            foreach (Match entity in Regex.Matches(raw.Groups["faces"].Value, "\\\"(?<entity>#[0-9]+)\\\"", RegexOptions.CultureInvariant))
            {
                var value = entity.Groups["entity"].Value;
                if (!topologyMap.TryResolveFaceEntity(value, out _)) { diagnostics.Add(UnknownRecognitionFace); return []; }
                faces.Add(value);
            }
        }
        if (faces.Count == 0 || faces.Distinct(StringComparer.Ordinal).Count() != faces.Count) { diagnostics.Add(UnknownRecognitionFace); return []; }
        return faces;
    }

    private static FirmamentV2RecognitionEvidence? ParseCanonicalRecognitionEvidence(string regionBody, List<string> diagnostics)
    {
        var header = Regex.Match(regionBody, @"\bEvidence\s*:\?\s*\{", RegexOptions.CultureInvariant);
        if (!header.Success) return null;
        var open = regionBody.IndexOf('{', header.Index); var close = FindMatchingBrace(regionBody, open);
        if (close < 0) { diagnostics.Add(CanonicalDeclarationUnrecognized); return null; }
        var body = regionBody[(open + 1)..close];
        var surface = Regex.Match(body, @"\bSurfaceFamily\s*:\s*(?<value>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant);
        var families = new List<string>();
        if (surface.Success)
        {
            var family = surface.Groups["value"].Value.ToLowerInvariant();
            if (family is not ("cylindrical" or "planar")) diagnostics.Add(RecognitionEvidenceSurfaceFamilyUnknown); else families.Add(family);
        }
        double? radius = null;
        if (Regex.IsMatch(body, @"\bRadius\s*:", RegexOptions.CultureInvariant))
        {
            var match = Regex.Match(body, @"\bRadius\s*:\s*(?<value>[-+0-9.eE]+)mm\b", RegexOptions.CultureInvariant);
            if (!match.Success || !double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value <= 0d || !double.IsFinite(value)) diagnostics.Add(RecognitionEvidenceRadiusInvalid); else radius = value;
        }
        bool? through = null;
        var throughMatch = Regex.Match(body, @"\bThrough\s*:\s*(?<value>true|false)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (throughMatch.Success) through = bool.Parse(throughMatch.Groups["value"].Value);
        return new FirmamentV2RecognitionEvidence(families, radius, Through: through, Notes: []);
    }

    private static IReadOnlyList<FirmamentV2ReplacementDecl> ParseCanonicalReplacements(string source, Dictionary<string, FirmamentV2SolidBinding> solids, IReadOnlyList<FirmamentV2RecognizedRegion> recognizedRegions, List<string> diagnostics)
    {
        var replacements = new List<FirmamentV2ReplacementDecl>();
        foreach (Match replace in CanonicalReplaceHeaderRegex.Matches(source))
        {
            var bodyName = replace.Groups["body"].Value; var regionName = replace.Groups["region"].Value;
            if (!solids.TryGetValue(bodyName, out var solid) || solid.InlineStep is null) { diagnostics.Add(UnknownReplacementBody); continue; }
            var region = recognizedRegions.SingleOrDefault(candidate => candidate.BodyName == bodyName && candidate.RegionName == regionName);
            if (region is null) { diagnostics.Add(UnknownReplacementRegion); continue; }
            var kind = NormalizeRecognitionKind(replace.Groups["kind"].Value);
            if (kind != "holeShaft") { diagnostics.Add(ReplacementUnsupportedKind); continue; }
            if (region.Kind != kind) { diagnostics.Add(ReplacementKindMismatch); continue; }
            var open = source.IndexOf('{', replace.Index); var close = FindMatchingBrace(source, open);
            if (close < 0) { diagnostics.Add(CanonicalDeclarationUnrecognized); continue; }
            var body = source[(open + 1)..close];
            var face = Regex.Match(body, @"\bOn\s*:\s*(?<body>[A-Za-z_][A-Za-z0-9_]*)\.Face\(\s*(?<face>[0-9]+)\s*\)", RegexOptions.CultureInvariant);
            if (!face.Success || face.Groups["body"].Value != bodyName || !int.TryParse(face.Groups["face"].Value, out var faceId) || !solid.InlineStep.TopologyMap.TryResolveSequentialFaceId(faceId, out var entity)) { diagnostics.Add(ReplacementFaceUnresolved); continue; }
            var center = Regex.Match(body, @"\bCenter\s*:\s*Point2\(\s*(?<u>[-+0-9.eE]+)mm\s*,\s*(?<v>[-+0-9.eE]+)mm\s*\)", RegexOptions.CultureInvariant);
            if (!center.Success || !double.TryParse(center.Groups["u"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var u) || !double.TryParse(center.Groups["v"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) { diagnostics.Add(HoleCenterMissing); continue; }
            var diameter = Regex.Match(body, @"\bDiameter\s*:\s*(?<value>[-+0-9.eE]+)mm\b", RegexOptions.CultureInvariant);
            if (!diameter.Success || !double.TryParse(diameter.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) || d <= 0d || !double.IsFinite(d)) { diagnostics.Add(ReplacementRadiusInvalid); continue; }
            if (!Regex.IsMatch(body, @"\bEnd\s*:\s*ThroughAll\b", RegexOptions.CultureInvariant)) { diagnostics.Add(ReplacementEndUnsupported); continue; }
            var host = Regex.Match(body, @"\bHostSize\s*:\s*\[(?<values>[^\]]*)\]", RegexOptions.CultureInvariant);
            var hostSize = host.Success ? ParseCanonicalLengthTriple(host.Groups["values"].Value) : null;
            if (hostSize is null) { diagnostics.Add(ReplacementVerificationFailed); continue; }
            replacements.Add(new FirmamentV2ReplacementDecl(bodyName, regionName, kind, replace.Groups["name"].Value, $"{bodyName}.face(\"{entity}\")", new FirmamentV2FaceLocalPoint2D(u, v, string.Empty), d / 2d, "throughAll", hostSize, replace.Value));
        }
        return replacements;
    }

    private static IReadOnlyList<double>? ParseCanonicalLengthTriple(string raw)
    {
        var values = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(ParsePhase3Length)
            .ToArray();
        return values.Length == 3 && values.All(value => double.IsFinite(value) && value > 0d) ? values : null;
    }

    private static FirmamentV2ParseResult CanonicalFailure(List<string> diagnostics, string diagnostic)
    {
        // A precise producer error already explains rejection. Do not append a
        // locationless document-malformed error for a semantically parsed Model.
        if (diagnostic != CanonicalDocumentMalformed || !diagnostics.Any(IsFatalDiagnosticCode))
            diagnostics.Add(diagnostic);
        diagnostics.Add("firmament-v2-parse-failed");
        return FirmamentV2ParseResult.Failure(diagnostics.Distinct(StringComparer.Ordinal).Order().ToArray());
    }

    private static FirmamentV2ParseResult ParseConceptModelingDocument(string source, List<string> diagnostics, IReadOnlyList<ConceptIrTemplateInstantiation>? templateInstantiations = null, FirmamentCompileTimeValues? scalars = null)
    {
        var before = scalars?.Diagnostics.Count ?? 0;
        var resolution = ConceptIrResolver.Resolve(source, diagnostics, scalars);
        if (resolution is null || diagnostics.Any(IsFatalDiagnosticCode))
        {
            diagnostics.Add("firmament-v2-parse-failed");
            diagnostics.Sort(StringComparer.Ordinal);
            return FirmamentV2ParseResult.Failure(diagnostics.Distinct(StringComparer.Ordinal).ToArray())
                with { SyntaxDiagnostics = scalars?.Diagnostics.Skip(before).ToArray() ?? [] };
        }

        var solid = new FirmamentV2SolidBinding(
            resolution.BoxName,
            "Box",
            new FirmamentV2BoxRecord(resolution.BoxSize, []),
            Provenance: new Dictionary<string, string>(StringComparer.Ordinal) { ["Bounds"] = resolution.BoxBoundsProvenance });
        var document = new FirmamentV2Document(
            resolution.ModelName,
            resolution.Units,
            [solid],
            [resolution.ModifyBlock],
            ConceptIr: resolution.ConceptIr with { TemplateInstantiations = templateInstantiations ?? [] });
        diagnostics.Add("firmament-concept-ir-resolved");
        diagnostics.Add("firmament-concept-struct-erased-before-feature-air");
        if ((templateInstantiations?.Count ?? 0) > 0) diagnostics.Add("firmament-template-expanded-before-feature-air");
        if (resolution.ModifyBlock.EdgeFinishes?.Count > 0) diagnostics.Add("firmament-v2-phase3-edge-finish-parsed");
        if (resolution.ModifyBlock.SemanticHoles.Count > 0) diagnostics.Add("firmament-concept-point3-semantic-holes-parsed");
        diagnostics.Add("firmament-v2-parse-succeeded");
        diagnostics.Sort(StringComparer.Ordinal);
        return FirmamentV2ParseResult.Success(document, diagnostics.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static double ParsePhase3Length(string source)
    {
        source = source.Trim();
        if (!source.EndsWith("mm", StringComparison.Ordinal)) return double.NaN;
        return double.TryParse(source[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : double.NaN;
    }

    public static bool IsFatalDiagnosticCode(string code) =>
        FirmamentInterop.FirmamentV2DiagnosticAdapter.ParserSeverity(code)
            == Aetheris.Forge.Abstractions.FirmamentInterop.FirmamentDiagnosticSeverity.Fatal;

    private static IReadOnlyList<double>? ParseSizeValues(string raw, List<string> diagnostics)
    {
        var parts = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3) { diagnostics.Add(BoxSizeArity); return null; }
        var values = new List<double>(3);
        foreach (var part in parts)
        {
            if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) { diagnostics.Add(WithFieldTypeMismatch); return null; }
            if (value <= 0) diagnostics.Add(DegenerateDimension);
            values.Add(value);
        }
        return diagnostics.Contains(DegenerateDimension) ? null : values;
    }

    private static FirmamentV2SemanticHoleEnd? ParseEnd(string body, List<string> diagnostics)
    {
        var m = Regex.Match(body, @"\bend\s*:\s*(?<kind>throughAll|depth)(?:\s+(?<value>[^\s}]+))?", RegexOptions.CultureInvariant);
        if (!m.Success) { diagnostics.Add(HoleEndMissing); return null; }
        if (m.Groups["kind"].Value == "throughAll") return new FirmamentV2SemanticHoleEnd(FirmamentV2SemanticHoleEndKind.ThroughAll);
        if (!double.TryParse(m.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var depth) || !double.IsFinite(depth) || depth <= 0) { diagnostics.Add(HoleDepthInvalid); return null; }
        return new FirmamentV2SemanticHoleEnd(FirmamentV2SemanticHoleEndKind.Depth, depth);
    }

    private static bool ReadPositive(string body, string[] names, out double value)
    {
        foreach (var name in names)
        {
            var m = Regex.Match(body, $@"\b{name}\s*:\s*(?<value>[^\s}}]+)", RegexOptions.CultureInvariant);
            if (m.Success)
            {
                if (!double.TryParse(m.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || !double.IsFinite(value) || value <= 0) return false;
                return true;
            }
        }
        value = 0;
        return false;
    }

    private static FirmamentV2FaceLocalPoint2D? ParseCenter(string toolBody, List<string> diagnostics)
    {
        var match = CenterRegex.Match(toolBody);
        if (!match.Success) return null;
        var parts = match.Groups["values"].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) { diagnostics.Add(CylinderCenterArityInvalid); return null; }
        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var u) || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) { diagnostics.Add(CylinderCenterInvalid); return null; }
        if (!double.IsFinite(u) || !double.IsFinite(v)) { diagnostics.Add(CylinderCenterNotFinite); return null; }
        return new FirmamentV2FaceLocalPoint2D(u, v, string.Empty);
    }


    private static FirmamentV2FaceSelector? ParseFaceSelector(string selector, string diagnostic, List<string> diagnostics)
    {
        var face = FaceSelectorRegex.Match(selector);
        if (!face.Success || face.Groups["sub"].Success) { diagnostics.Add(diagnostic); return null; }
        return new(face.Groups["axis"].Value);
    }

    private static FirmamentV2FaceTarget? ResolveFaceTarget(string source, FirmamentV2SolidBinding solid, string directDiagnostic, List<string> diagnostics)
    {
        source = source.Trim();
        if (source.StartsWith("face(", StringComparison.Ordinal))
        {
            var selector = ParseFaceSelector(source, directDiagnostic, diagnostics);
            return selector is null ? null : FirmamentV2FaceTarget.Direct(selector.Axis);
        }

        var exposure = solid.Box!.Exposures.FirstOrDefault(e => string.Equals(e.Alias, source, StringComparison.Ordinal));
        if (exposure is null) { diagnostics.Add(AliasUnresolved); return null; }
        if (!string.Equals(exposure.RefType, "FaceRef", StringComparison.Ordinal))
        {
            diagnostics.Add(AliasRefTypeUnsupported);
            diagnostics.Add(SideHoleAliasMustResolveToFace);
            return null;
        }
        return FirmamentV2FaceTarget.Alias(source, exposure.Axis);
    }

    private static (IReadOnlyList<FirmamentV2ManufacturingConceptDeclaration> Manufacturing, IReadOnlyList<FirmamentV2FeatureConceptDeclaration> Features) ParseConceptApplications(
        string source,
        IReadOnlyList<FirmamentV2BoundLet> boundLets,
        IReadOnlyList<FirmamentV2BoundLetRecord> boundLetRecords,
        FirmamentV2ForgeConceptCatalog conceptCatalog,
        List<string> diagnostics)
    {
        var manufacturing = new List<FirmamentV2ManufacturingConceptDeclaration>();
        foreach (Match match in ManufacturingConceptHeaderRegex.Matches(source))
        {
            var open = source.IndexOf('{', match.Index);
            var close = FindMatchingBrace(source, open);
            if (close < 0) { diagnostics.Add(UnsupportedConstruct); continue; }
            var application = new FirmamentV2ConceptApplication(match.Groups["family"].Value, match.Groups["concept"].Value, new(match.Groups["family"].Index, match.Groups["concept"].Index + match.Groups["concept"].Length - match.Groups["family"].Index));
            var fields = ParseConceptFields(source[(open + 1)..close], open + 1, diagnostics);
            var bound = ValidateConceptApplication(application, fields, boundLets, boundLetRecords, conceptCatalog, diagnostics);
            manufacturing.Add(new(application, fields, new(match.Index, close - match.Index + 1), bound));
        }

        var features = new List<FirmamentV2FeatureConceptDeclaration>();
        foreach (Match match in FeatureConceptHeaderRegex.Matches(source))
        {
            var open = source.IndexOf('{', match.Index);
            var close = FindMatchingBrace(source, open);
            if (close < 0) { diagnostics.Add(UnsupportedConstruct); continue; }
            var application = new FirmamentV2ConceptApplication(match.Groups["family"].Value, match.Groups["concept"].Value, new(match.Groups["family"].Index, match.Groups["concept"].Index + match.Groups["concept"].Length - match.Groups["family"].Index));
            var fields = ParseConceptFields(source[(open + 1)..close], open + 1, diagnostics);
            var bound = ValidateConceptApplication(application, fields, boundLets, boundLetRecords, conceptCatalog, diagnostics);
            features.Add(new(match.Groups["name"].Value, application, fields, new(match.Index, close - match.Index + 1), bound));
        }

        return (manufacturing, features);
    }

    private static IReadOnlyList<FirmamentV2ConceptField> ParseConceptFields(string body, int bodyStart, List<string> diagnostics)
    {
        var fields = new List<FirmamentV2ConceptField>();
        var offset = 0;
        foreach (var rawLine in body.Split('\n'))
        {
            var lineStart = bodyStart + offset;
            offset += rawLine.Length + 1;
            var line = rawLine.Trim();
            if (line.Length == 0) continue;
            var match = ConceptFieldLineRegex.Match(line);
            if (!match.Success) { diagnostics.Add(UnsupportedConstruct); continue; }
            var valueSource = match.Groups["value"].Value.Trim();
            var expression = ParseConceptFieldExpression(valueSource, diagnostics);
            if (expression is not null)
                fields.Add(new(match.Groups["name"].Value, expression, valueSource, new(lineStart + rawLine.IndexOf(match.Groups["name"].Value, StringComparison.Ordinal), rawLine.Length)));
        }

        return fields;
    }

    private static FirmamentV2ValueExpression? ParseConceptFieldExpression(string valueSource, List<string> diagnostics)
    {
        if (TargetExpressionRegex.IsMatch(valueSource)) return new FirmamentV2IdentifierReferenceExpression(valueSource, valueSource);
        if (valueSource.IndexOfAny(['+', '-', '*', '/', '(', ')']) >= 0 && !valueSource.EndsWith(")", StringComparison.Ordinal))
        {
            diagnostics.Add(ExpressionUnsupported);
            return null;
        }

        var dotted = DottedReferenceRegex.Match(valueSource);
        if (dotted.Success) return new FirmamentV2DottedReferenceExpression(dotted.Groups["record"].Value, dotted.Groups["field"].Value, valueSource);
        if (valueSource is "true" or "false") return new FirmamentV2LiteralExpression(new(FirmamentV2PrimitiveType.Bool, valueSource == "true", null, null, valueSource));
        if (Regex.IsMatch(valueSource, "^\\\"[^\\\"]*\\\"$", RegexOptions.CultureInvariant)) return new FirmamentV2LiteralExpression(new(FirmamentV2PrimitiveType.String, valueSource[1..^1], null, null, valueSource));
        if (Regex.IsMatch(valueSource, @"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)) return new FirmamentV2IdentifierReferenceExpression(valueSource, valueSource);
        var type = valueSource.EndsWith("mm", StringComparison.Ordinal) ? FirmamentV2PrimitiveType.Length : valueSource.EndsWith("deg", StringComparison.Ordinal) ? FirmamentV2PrimitiveType.Angle : valueSource.Contains('.', StringComparison.Ordinal) ? FirmamentV2PrimitiveType.Float : FirmamentV2PrimitiveType.Int;
        var literal = ParseLetLiteral(type, valueSource, diagnostics);
        if (literal is not null) return new FirmamentV2LiteralExpression(literal);
        diagnostics.Add(ExpressionUnsupported);
        return null;
    }

    private static IReadOnlyList<FirmamentV2BoundConceptField> ValidateConceptApplication(
        FirmamentV2ConceptApplication application,
        IReadOnlyList<FirmamentV2ConceptField> fields,
        IReadOnlyList<FirmamentV2BoundLet> boundLets,
        IReadOnlyList<FirmamentV2BoundLetRecord> boundLetRecords,
        FirmamentV2ForgeConceptCatalog conceptCatalog,
        List<string> diagnostics)
    {
        if (!conceptCatalog.TryGet(application.FamilyName, application.ConceptName, out var descriptor))
        {
            diagnostics.Add(conceptCatalog.HasFamily(application.FamilyName) ? ConceptUnknownConcept : ConceptUnknownFamily);
            return [];
        }

        var bound = new List<FirmamentV2BoundConceptField>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var fieldNames = fields.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (!seen.Add(field.Name)) { diagnostics.Add(ConceptDuplicateField); continue; }
            if (!descriptor.Fields.TryGetValue(field.Name, out var schema)) { diagnostics.Add(ConceptUnknownField); continue; }
            bound.Add(BindConceptField(field, schema, boundLets, boundLetRecords, diagnostics));
        }

        foreach (var required in descriptor.Fields.Values.Where(f => f.Required))
            if (!fieldNames.Contains(required.Name)) diagnostics.Add(ConceptMissingRequiredField);
        return bound;
    }

    private static FirmamentV2BoundConceptField BindConceptField(FirmamentV2ConceptField field, FirmamentV2ForgeFieldDescriptor schema, IReadOnlyList<FirmamentV2BoundLet> boundLets, IReadOnlyList<FirmamentV2BoundLetRecord> boundLetRecords, List<string> diagnostics)
    {
        if (schema.Kind == FirmamentV2ForgeFieldKind.Target)
        {
            if (!TargetExpressionRegex.IsMatch(field.Source)) diagnostics.Add(ConceptInvalidTarget);
            return new(field.Name, field, null, field.Source);
        }

        FirmamentV2BoundExpression? bound = field.ValueExpression switch
        {
            FirmamentV2LiteralExpression l => new(l.Value.Type, l.Value, new HashSet<string>(StringComparer.Ordinal), field.SourceSpan),
            FirmamentV2IdentifierReferenceExpression r when boundLets.FirstOrDefault(l => l.Name == r.Name) is { } let => new(let.Type, let.Value, new HashSet<string>((let.Dependencies ?? new HashSet<string>()).Append(let.Name), StringComparer.Ordinal), let.SourceSpan, let.Tolerance),
            FirmamentV2IdentifierReferenceExpression r when schema.Kind == FirmamentV2ForgeFieldKind.Material => new(FirmamentV2PrimitiveType.String, new(FirmamentV2PrimitiveType.String, r.Name, null, null, r.Source), new HashSet<string>(StringComparer.Ordinal), field.SourceSpan),
            FirmamentV2DottedReferenceExpression r when boundLetRecords.FirstOrDefault(x => x.Name == r.RecordName)?.Fields.TryGetValue(r.FieldName, out var f) == true => new(f.Type, f.Value, new HashSet<string> { $"{r.RecordName}.{r.FieldName}" }, f.SourceSpan, f.Tolerance),
            _ => null
        };

        if (bound is null) diagnostics.Add(ExpressionUnknownSymbol);
        else if (!schema.Accepts(bound.InferredType)) diagnostics.Add(ConceptFieldTypeMismatch);
        return new(field.Name, field, bound);
    }
    private static IReadOnlyList<FirmamentV2TemplateDecl> ParseTemplates(string source, List<string> diagnostics)
    {
        var templates = new List<FirmamentV2TemplateDecl>();
        foreach (Match tm in TemplateHeaderRegex.Matches(source))
        {
            var open = source.IndexOf('{', tm.Index);
            var close = FindMatchingBrace(source, open);
            if (close < 0) { diagnostics.Add(UnsupportedConstruct); continue; }
            var body = source[(open + 1)..close];
            var concepts = new List<FirmamentV2ConceptDecl>();
            foreach (Match cm in ConceptRegex.Matches(body))
            {
                if (double.TryParse(cm.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value))
                {
                    concepts.Add(new FirmamentV2ConceptDecl(cm.Groups["name"].Value, cm.Value.Trim(), value, cm.Groups["unit"].Success ? cm.Groups["unit"].Value : null));
                }
            }
            templates.Add(new FirmamentV2TemplateDecl(tm.Groups["process"].Value, tm.Groups["name"].Value, concepts));
        }
        return templates;
    }

    // M9's region and fill declarations are top-level authority declarations. They do not reuse
    // Modify/Region, whose existing meaning is a face-attached side-hole construction island.
    private static IReadOnlyList<FirmamentV2LatticeFillDecl> ParseLatticeFills(string source, Dictionary<string, FirmamentV2SolidBinding> solids, List<string> diagnostics)
    {
        var regions = new Dictionary<string, FirmamentV2FillRegionDecl>(StringComparer.Ordinal);
        foreach (Match match in FillRegionHeaderRegex.Matches(source))
        {
            var open = source.IndexOf('{', match.Index);
            var close = FindMatchingBrace(source, open);
            if (close < 0) { diagnostics.Add(FillRegionInvalid); continue; }
            var body = source[(open + 1)..close];
            // Do not interpret the legacy `region X on face(...)` syntax as a FillRegion.
            if (Regex.IsMatch(source.Substring(match.Index, open - match.Index), @"\bon\s+", RegexOptions.CultureInvariant)) continue;
            var box = Regex.Match(body, @"\bbox\s*\{", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            // Recognition regions also use the word `region`; without an explicit Box they are
            // unrelated metadata and must not be reinterpreted as a material-authority boundary.
            if (!box.Success) continue;
            var boxOpen = body.IndexOf('{', box.Index);
            var boxClose = FindMatchingBrace(body, boxOpen);
            if (boxClose < 0) { diagnostics.Add(FillRegionInvalid); continue; }
            var boxBody = body[(boxOpen + 1)..boxClose];
            var size = Regex.Match(boxBody, @"\bsize\s*:\s*\[(?<v>[^\]]+)\]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            var center = Regex.Match(boxBody, @"\bcenter\s*:\s*\[(?<v>[^\]]+)\]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            IReadOnlyList<double> centerValues = [0d, 0d, 0d];
            if (!size.Success || !TryParseLatticeTriple(size.Groups["v"].Value, positive: true, out var sizeValues) || (center.Success && !TryParseLatticeTriple(center.Groups["v"].Value, positive: false, out centerValues))) { diagnostics.Add(FillRegionInvalid); continue; }
            if (!regions.TryAdd(match.Groups["name"].Value, new(match.Groups["name"].Value, sizeValues, centerValues, new(match.Index, close - match.Index + 1)))) diagnostics.Add(FillRegionInvalid);
        }

        var fills = new List<FirmamentV2LatticeFillDecl>();
        foreach (Match match in FillHeaderRegex.Matches(source))
        {
            var open = source.IndexOf('{', match.Index);
            var close = FindMatchingBrace(source, open);
            if (close < 0) { diagnostics.Add(FillRegionInvalid); continue; }
            var body = source[(open + 1)..close];
            if (!regions.TryGetValue(match.Groups["name"].Value, out var region)) { diagnostics.Add(FillUnknownRegion); continue; }
            var pattern = Regex.Match(body, @"\bpattern\s*:\s*(?<v>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            var host = Regex.Match(body, @"\bhost\s*:\s*(?<v>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            if (!host.Success && pattern.Success && string.Equals(pattern.Groups["v"].Value, "CubicTruss", StringComparison.OrdinalIgnoreCase)) continue;
            if (!host.Success || !solids.ContainsKey(host.Groups["v"].Value)) { diagnostics.Add(FillUnknownHost); continue; }
            if (!pattern.Success || !string.Equals(pattern.Groups["v"].Value, "OctetTruss", StringComparison.OrdinalIgnoreCase)) { diagnostics.Add(FillUnsupportedPattern); continue; }
            var patternOpen = body.IndexOf('{', pattern.Index);
            var patternClose = FindMatchingBrace(body, patternOpen);
            var patternBody = patternClose < 0 ? string.Empty : body[(patternOpen + 1)..patternClose];
            if (!TryReadLatticePositive(patternBody, "cellSize", out var cellSize)) { diagnostics.Add(FillInvalidCellSize); continue; }
            if (!TryReadLatticePositive(patternBody, "strutRadius", out var strutRadius)) { diagnostics.Add(FillInvalidStrutRadius); continue; }
            var policy = Regex.Match(body, @"\bboundaryPolicy\s*:\s*(?<v>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            if (!policy.Success || !string.Equals(policy.Groups["v"].Value, "Bond", StringComparison.OrdinalIgnoreCase)) { diagnostics.Add(FillUnsupportedBoundaryPolicy); continue; }
            fills.Add(new(match.Groups["name"].Value, host.Groups["v"].Value, region, "OctetTruss", cellSize, strutRadius, "Bond", new(match.Index, close - match.Index + 1)));
        }
        if (fills.Count > 1) diagnostics.Add(FillMultipleUnsupported);
        return fills;
    }

    private static IReadOnlyList<FirmamentV2StandaloneLatticeFillDecl> ParseStandaloneLatticeFills(string source, List<string> diagnostics)
    {
        var regions = new Dictionary<string, FirmamentV2FillRegionDecl>(StringComparer.Ordinal);
        foreach (Match match in FillRegionHeaderRegex.Matches(source))
        {
            var open = source.IndexOf('{', match.Index); var close = FindMatchingBrace(source, open);
            if (close < 0) continue;
            var body = source[(open + 1)..close]; var box = Regex.Match(body, @"\bbox\s*\{", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            if (!box.Success) continue;
            var boxOpen = body.IndexOf('{', box.Index); var boxClose = FindMatchingBrace(body, boxOpen);
            if (boxClose < 0) { diagnostics.Add(StandaloneFillRegionRequired); continue; }
            var boxBody = body[(boxOpen + 1)..boxClose];
            var size = Regex.Match(boxBody, @"\bsize\s*:\s*\[(?<v>[^\]]+)\]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            var center = Regex.Match(boxBody, @"\bcenter\s*:\s*\[(?<v>[^\]]+)\]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            IReadOnlyList<double> location = [0d, 0d, 0d];
            if (!size.Success || !TryParseLatticeTriple(size.Groups["v"].Value, positive: true, out var dimensions) || (center.Success && !TryParseLatticeTriple(center.Groups["v"].Value, positive: false, out location))) { diagnostics.Add(StandaloneFillRegionRequired); continue; }
            regions[match.Groups["name"].Value] = new FirmamentV2FillRegionDecl(match.Groups["name"].Value, dimensions, location, new FirmamentV2SourceSpan(match.Index, close - match.Index + 1));
        }

        var fills = new List<FirmamentV2StandaloneLatticeFillDecl>();
        foreach (Match match in FillHeaderRegex.Matches(source))
        {
            var open = source.IndexOf('{', match.Index); var close = FindMatchingBrace(source, open);
            if (close < 0) continue;
            var body = source[(open + 1)..close];
            var pattern = Regex.Match(body, @"\bpattern\s*:\s*(?<v>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            if (!pattern.Success || !string.Equals(pattern.Groups["v"].Value, "CubicTruss", StringComparison.OrdinalIgnoreCase)) continue;
            if (Regex.IsMatch(body, @"\bhost\s*:", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)) { diagnostics.Add(UnsupportedStandaloneFillPattern); continue; }
            if (!regions.TryGetValue(match.Groups["name"].Value, out var region)) { diagnostics.Add(StandaloneFillRegionRequired); continue; }
            var patternOpen = body.IndexOf('{', pattern.Index); var patternClose = FindMatchingBrace(body, patternOpen);
            if (patternClose < 0) { diagnostics.Add(UnsupportedStandaloneFillPattern); continue; }
            var p = body[(patternOpen + 1)..patternClose];
            var cells = Regex.Match(p, @"\bcells\s*:\s*\[(?<v>[^\]]+)\]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            if (!cells.Success || !TryParseLatticeCellTriple(cells.Groups["v"].Value, out var counts)) { diagnostics.Add(InvalidCellCount); continue; }
            if (!TryReadLatticePositive(p, "cellSize", out var cellSize)) { diagnostics.Add(CellSizeMustBePositive); continue; }
            if (!TryReadLatticePositive(p, "strutRadius", out var strutRadius)) { diagnostics.Add(StrutRadiusMustBePositive); continue; }
            if (!TryReadLatticePositive(p, "nodeRadius", out var nodeRadius)) { diagnostics.Add(NodeRadiusMustBePositive); continue; }
            var placement = Regex.Match(body, @"\bplacement\s*:\s*(?<v>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            if (!placement.Success || !string.Equals(placement.Groups["v"].Value, "MaterialBounds", StringComparison.OrdinalIgnoreCase)) { diagnostics.Add(MaterialBoundsMismatch); continue; }
            fills.Add(new(match.Groups["name"].Value, region, "CubicTruss", counts[0], counts[1], counts[2], cellSize, strutRadius, nodeRadius, "MaterialBounds", new FirmamentV2SourceSpan(match.Index, close - match.Index + 1)));
        }
        if (fills.Count > 1) diagnostics.Add(StandaloneFillMultipleUnsupported);
        return fills;
    }

    private static bool TryParseLatticeCellTriple(string text, out IReadOnlyList<int> values)
    {
        var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var parsed = new List<int>(3);
        foreach (var part in parts)
        {
            if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value <= 0) { values = []; return false; }
            parsed.Add(value);
        }
        values = parsed;
        return parsed.Count == 3;
    }

    private static bool TryReadLatticePositive(string body, string name, out double value)
    {
        value = 0d;
        var match = Regex.Match(body, $@"\b{name}\s*:\s*(?<v>[-+0-9.eE]+)\s*(?:mm)?", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        return match.Success && double.TryParse(match.Groups["v"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value) && value > 0d;
    }

    private static bool TryParseLatticeTriple(string text, bool positive, out IReadOnlyList<double> values)
    {
        var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var parsed = new List<double>(3);
        foreach (var part in parts)
        {
            var raw = part.EndsWith("mm", StringComparison.OrdinalIgnoreCase) ? part[..^2] : part;
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value) || (positive && value <= 0d)) { values = []; return false; }
            parsed.Add(value);
        }
        values = parsed;
        return parsed.Count == 3;
    }


    private static IReadOnlyList<FirmamentV2RecognizedRegion> ParseRecognizedRegions(string source, Dictionary<string, FirmamentV2SolidBinding> solids, List<string> diagnostics)
    {
        var result = new List<FirmamentV2RecognizedRegion>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match rm in RecognizeHeaderRegex.Matches(source))
        {
            var bodyName = rm.Groups["body"].Value;
            if (!solids.TryGetValue(bodyName, out var solid) || solid.InlineStep is null)
            {
                diagnostics.Add(UnknownRecognitionBody);
                continue;
            }
            var open = source.IndexOf('{', rm.Index);
            var close = FindMatchingBrace(source, open);
            if (close < 0) { diagnostics.Add(UnsupportedConstruct); continue; }
            var block = source[(open + 1)..close];
            foreach (Match rr in RecognitionRegionHeaderRegex.Matches(block))
            {
                var regionName = rr.Groups["name"].Value;
                if (!seen.Add($"{bodyName}.{regionName}")) { diagnostics.Add(DuplicateRegion); continue; }
                var regionOpen = block.IndexOf('{', rr.Index);
                var regionClose = FindMatchingBrace(block, regionOpen);
                if (regionClose < 0) { diagnostics.Add(UnsupportedConstruct); continue; }
                var rb = block[(regionOpen + 1)..regionClose];
                var kindMatch = KindRegex.Match(rb);
                var kind = kindMatch.Success ? NormalizeRecognitionKind(kindMatch.Groups["kind"].Value) : string.Empty;
                if (kind is not ("datumPlane" or "holeShaft")) { diagnostics.Add(InvalidRecognitionKind); continue; }
                var confidenceMatch = ConfidenceRegex.Match(rb);
                var confidence = confidenceMatch.Success ? confidenceMatch.Groups["confidence"].Value : "medium";
                if (confidence is not ("low" or "medium" or "high" or "certain")) { diagnostics.Add(InvalidRecognitionConfidence); continue; }
                var facesMatch = FacesRegex.Match(rb);
                var faces = facesMatch.Success
                    ? Regex.Matches(facesMatch.Groups["faces"].Value, "\\\"(?<face>#[0-9]+)\\\"", RegexOptions.CultureInvariant).Select(m => m.Groups["face"].Value).ToArray()
                    : [];
                if (faces.Length == 0 || faces.Any(f => !solid.InlineStep.TopologyMap.TryResolveFaceEntity(f, out _))) { diagnostics.Add(UnknownRecognitionFace); continue; }
                var evidence = ParseRecognitionEvidence(rb, diagnostics);
                var proposal = ParseSemanticProposal(rb, solid, kind, regionName, diagnostics);
                result.Add(new FirmamentV2RecognizedRegion(bodyName, regionName, kind, faces, confidence, evidence, proposal));
            }
        }
        return result;
    }

    private static string NormalizeRecognitionKind(string raw)
    {
        raw = raw.Trim().Trim('"').Replace(" ", string.Empty, StringComparison.Ordinal);
        return string.Equals(raw, "hole<shaft>", StringComparison.OrdinalIgnoreCase) || string.Equals(raw, "holeShaft", StringComparison.OrdinalIgnoreCase)
            ? "holeShaft"
            : string.Equals(raw, "datumPlane", StringComparison.OrdinalIgnoreCase) ? "datumPlane" : raw;
    }

    private static FirmamentV2RecognitionEvidence? ParseRecognitionEvidence(string regionBody, List<string> diagnostics)
    {
        var match = EvidenceHeaderRegex.Match(regionBody);
        if (!match.Success) return null;
        var open = regionBody.IndexOf('{', match.Index);
        var close = FindMatchingBrace(regionBody, open);
        if (close < 0) { diagnostics.Add(UnsupportedConstruct); return null; }
        var body = regionBody[(open + 1)..close];
        var families = new List<string>();
        foreach (Match familyMatch in SurfaceFamilyRegex.Matches(body))
        {
            var family = familyMatch.Groups["value"].Value;
            if (family is not ("cylindrical" or "planar")) diagnostics.Add(RecognitionEvidenceSurfaceFamilyUnknown);
            else families.Add(family);
        }

        double? radius = null;
        if (Regex.IsMatch(body, @"\bradius\s*:", RegexOptions.CultureInvariant))
        {
            if (!TryReadPositiveNumberWithOptionalMm(body, "radius", out var value)) diagnostics.Add(RecognitionEvidenceRadiusInvalid);
            else radius = value;
        }

        string? axis = null;
        if (Regex.IsMatch(body, @"\baxis\s*:", RegexOptions.CultureInvariant))
        {
            var axisMatch = AxisRegex.Match(body);
            if (!axisMatch.Success) diagnostics.Add(RecognitionEvidenceAxisInvalid);
            else axis = axisMatch.Groups["value"].Value;
        }

        var center = CenterRegex.IsMatch(body) ? ParseCenterWithOptionalUnits(body, diagnostics) : null;
        bool? through = null;
        var throughMatch = BooleanThroughRegex.Match(body);
        if (throughMatch.Success) through = bool.Parse(throughMatch.Groups["value"].Value);
        return new FirmamentV2RecognitionEvidence(families, radius, axis, center, through, []);
    }

    private static FirmamentV2SemanticProposal? ParseSemanticProposal(string regionBody, FirmamentV2SolidBinding solid, string regionKind, string regionName, List<string> diagnostics)
    {
        var match = ProposalHeaderRegex.Match(regionBody);
        if (!match.Success) return null;
        var proposalKind = NormalizeRecognitionKind(match.Groups["kind"].Value);
        if (!string.Equals(proposalKind, regionKind, StringComparison.Ordinal)) diagnostics.Add(SemanticProposalKindMismatch);
        if (proposalKind != "holeShaft") diagnostics.Add(SemanticProposalKindMismatch);
        var open = regionBody.IndexOf('{', match.Index);
        var close = FindMatchingBrace(regionBody, open);
        if (close < 0) { diagnostics.Add(UnsupportedConstruct); return null; }
        var body = regionBody[(open + 1)..close];
        string? placementTarget = null;
        var on = OnRegex.Match(body);
        if (on.Success)
        {
            placementTarget = on.Groups["target"].Value.Trim();
            if (!TryValidateReplacementFaceTarget(placementTarget, solid)) diagnostics.Add(SemanticProposalTargetUnresolved);
        }

        var center = CenterRegex.IsMatch(body) ? ParseCenterWithOptionalUnits(body, diagnostics) : null;
        double? radius = null;
        if (Regex.IsMatch(body, @"\bradius\s*:", RegexOptions.CultureInvariant))
        {
            if (!TryReadPositiveNumberWithOptionalMm(body, "radius", out var value)) diagnostics.Add(SemanticProposalRadiusInvalid);
            else radius = value;
        }

        string? end = null;
        var endMatch = EndConditionRegex.Match(body);
        if (endMatch.Success)
        {
            end = endMatch.Groups["value"].Value;
            if (end != "throughAll") diagnostics.Add(SemanticProposalEndUnsupported);
        }

        var featureName = match.Groups["name"].Success && !string.IsNullOrWhiteSpace(match.Groups["name"].Value) ? match.Groups["name"].Value : regionName;
        return new FirmamentV2SemanticProposal(proposalKind, featureName, placementTarget, center, radius, end);
    }

    private static FirmamentV2FaceLocalPoint2D? ParseCenterWithOptionalUnits(string body, List<string> diagnostics)
    {
        var match = CenterRegex.Match(body);
        if (!match.Success) return null;
        var parts = match.Groups["values"].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) { diagnostics.Add(CylinderCenterArityInvalid); return null; }
        if (!TryParseNumberWithOptionalMm(parts[0], out var u) || !TryParseNumberWithOptionalMm(parts[1], out var v)) { diagnostics.Add(CylinderCenterInvalid); return null; }
        if (!double.IsFinite(u) || !double.IsFinite(v)) { diagnostics.Add(CylinderCenterNotFinite); return null; }
        return new FirmamentV2FaceLocalPoint2D(u, v, string.Empty);
    }

    private static bool TryReadPositiveNumberWithOptionalMm(string body, string field, out double value)
    {
        value = 0d;
        var m = Regex.Match(body, $@"\b{field}\s*:\s*(?<value>[^\s}}]+)", RegexOptions.CultureInvariant);
        return m.Success && TryParseNumberWithOptionalMm(m.Groups["value"].Value, out value) && double.IsFinite(value) && value > 0d;
    }

    private static bool TryParseNumberWithOptionalMm(string raw, out double value)
    {
        raw = raw.Trim();
        if (raw.EndsWith("mm", StringComparison.Ordinal)) raw = raw[..^2];
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }


    private static IReadOnlyList<FirmamentV2ReplacementDecl> ParseReplacements(string source, Dictionary<string, FirmamentV2SolidBinding> solids, IReadOnlyList<FirmamentV2RecognizedRegion> recognizedRegions, List<string> diagnostics)
    {
        var replacements = new List<FirmamentV2ReplacementDecl>();
        var regions = recognizedRegions.ToDictionary(r => r.TargetSource, r => r, StringComparer.Ordinal);
        foreach (Match rm in ReplaceHeaderRegex.Matches(source))
        {
            var target = rm.Groups["target"].Value;
            var targetMatch = RecognizedRegionTargetRegex.Match(target);
            var bodyName = targetMatch.Groups["body"].Value;
            var regionName = targetMatch.Groups["region"].Value;
            if (!solids.TryGetValue(bodyName, out var solid) || solid.InlineStep is null) { diagnostics.Add(UnknownReplacementBody); continue; }
            if (!regions.TryGetValue(target, out var region)) { diagnostics.Add(UnknownReplacementRegion); continue; }
            var kind = NormalizeRecognitionKind(rm.Groups["kind"].Value);
            if (kind != "holeShaft") { diagnostics.Add(ReplacementUnsupportedKind); continue; }
            if (region.Kind != "holeShaft") { diagnostics.Add(ReplacementKindMismatch); continue; }
            var open = source.IndexOf('{', rm.Index);
            var close = FindMatchingBrace(source, open);
            if (close < 0) { diagnostics.Add(UnsupportedConstruct); continue; }
            var body = source[(open + 1)..close];
            var on = OnRegex.Match(body);
            if (!on.Success || !TryValidateReplacementFaceTarget(on.Groups["target"].Value.Trim(), solid)) { diagnostics.Add(ReplacementFaceUnresolved); continue; }
            var center = CenterRegex.IsMatch(body) ? ParseCenter(body, diagnostics) : null;
            if (center is null) { diagnostics.Add(HoleCenterMissing); continue; }
            var hasRadius = ReadPositive(body, ["radius"], out var radius) || (ReadPositive(body, ["diameter"], out var diameter) && (radius = diameter / 2d) > 0);
            if (!hasRadius || radius <= 0) { diagnostics.Add(ReplacementRadiusInvalid); continue; }
            var end = ParseEnd(body, diagnostics);
            if (end?.Kind != FirmamentV2SemanticHoleEndKind.ThroughAll) { diagnostics.Add(ReplacementEndUnsupported); continue; }
            var hostMatch = HostSizeRegex.Match(body);
            var hostSize = hostMatch.Success ? ParseSizeValues(hostMatch.Groups["values"].Value, diagnostics) : null;
            if (hostSize is null) { diagnostics.Add(ReplacementVerificationFailed); continue; }
            replacements.Add(new FirmamentV2ReplacementDecl(bodyName, regionName, kind, rm.Groups["name"].Value, on.Groups["target"].Value.Trim(), center, radius, "throughAll", hostSize, rm.Value));
        }
        return replacements;
    }

    private static bool TryValidateReplacementFaceTarget(string target, FirmamentV2SolidBinding solid)
    {
        var imported = Regex.Match(target, "^(?<body>[A-Za-z_][A-Za-z0-9_]*)\\.face\\(\"(?<entity>#[0-9]+)\"\\)?$", RegexOptions.CultureInvariant);
        return imported.Success && string.Equals(imported.Groups["body"].Value, solid.Name, StringComparison.Ordinal) && solid.InlineStep is not null && solid.InlineStep.TopologyMap.TryResolveFaceEntity(imported.Groups["entity"].Value, out _);
    }

    private static (IReadOnlyList<FirmamentV2PmiDecl> Legacy, FirmamentV2PmiBlock? Block, FirmamentV2BoundPmiBlock? Bound) ParsePmi(
        string source,
        Dictionary<string, FirmamentV2SolidBinding> solids,
        IReadOnlyList<FirmamentV2ModifyBlock> modifyBlocks,
        IReadOnlyList<FirmamentV2RecognizedRegion> recognizedRegions,
        IReadOnlyList<FirmamentV2BoundLet> boundLets,
        IReadOnlyList<FirmamentV2BoundLetRecord> boundLetRecords,
        List<string> diagnostics,
        IEnumerable<string>? additionalHoleNames = null)
    {
        var matches = PmiHeaderRegex.Matches(source);
        if (matches.Count == 0) return ([], null, null);
        if (matches.Count > 1) diagnostics.Add(PmiDuplicateBlock);
        var match = matches[0];
        var open = source.IndexOf('{', match.Index);
        var close = FindMatchingBrace(source, open);
        if (close < 0) { diagnostics.Add(UnsupportedConstruct); return ([], null, null); }
        var body = source[(open + 1)..close];
        var legacy = new List<FirmamentV2PmiDecl>();
        var records = new List<FirmamentV2PmiRecord>();
        var bound = new List<FirmamentV2BoundPmiRecord>();
        var recordNames = new HashSet<string>(StringComparer.Ordinal);
        var datumLabels = new HashSet<string>(StringComparer.Ordinal);
        var knownDatums = new HashSet<string>(StringComparer.Ordinal);
        var letMap = boundLets.ToDictionary(l => l.Name, StringComparer.Ordinal);
        var recordMap = boundLetRecords.ToDictionary(r => r.Name, StringComparer.Ordinal);
        var holeNames = modifyBlocks.SelectMany(m => m.SemanticHoles).Select(h => h.Name).Concat(additionalHoleNames ?? []).ToHashSet(StringComparer.Ordinal);
        var regionByTarget = recognizedRegions.ToDictionary(r => r.TargetSource, r => r, StringComparer.Ordinal);
        var aliases = solids.Values.SelectMany(s => s.Box?.Exposures ?? []).Select(e => e.Alias).ToHashSet(StringComparer.Ordinal);

        foreach (Match em in PmiEntryHeaderRegex.Matches(body))
        {
            var kindRaw = NormalizePmiKind(em.Groups["kind"].Value);
            var name = em.Groups["name"].Value;
            if (!TryPmiKind(kindRaw, out var kind)) { diagnostics.Add(PmiUnknownRecordKind); diagnostics.Add(PmiKindUnknown); continue; }
            if (kind == FirmamentV2PmiKind.DatumPlane && !datumLabels.Add(name)) { diagnostics.Add(PmiDuplicateDatum); }
            if (!recordNames.Add(name)) { diagnostics.Add(PmiDuplicateRecord); diagnostics.Add(PmiDuplicateName); continue; }
            var entryOpen = body.IndexOf('{', em.Index);
            var entryClose = FindMatchingBrace(body, entryOpen);
            if (entryClose < 0) { diagnostics.Add(UnsupportedConstruct); continue; }
            var eb = body[(entryOpen + 1)..entryClose];
            var fields = ParsePmiFields(eb, open + 1 + entryOpen, diagnostics);
            ValidatePmiFieldSet(kindRaw, name, fields, diagnostics);
            var record = new FirmamentV2PmiRecord(kind, name, fields, new FirmamentV2SourceSpan(open + 1 + em.Index, entryClose - em.Index + 1));
            records.Add(record);
            if (!TryBindPmiRecord(record, solids, modifyBlocks, regionByTarget, aliases, holeNames, letMap, recordMap, knownDatums, diagnostics, out var b)) continue;
            bound.Add(b);
            if (kind == FirmamentV2PmiKind.DatumPlane) knownDatums.Add(name);
            if (kind is FirmamentV2PmiKind.DatumPlane or FirmamentV2PmiKind.HoleDiameter)
                legacy.Add(new FirmamentV2PmiDecl(name, kind, b.Targets[0], b.DimensionValue?.NumericValue));
        }

        var block = new FirmamentV2PmiBlock(records, new FirmamentV2SourceSpan(match.Index, close - match.Index + 1));
        var boundBlock = new FirmamentV2BoundPmiBlock(bound.Where(r => r.Kind == FirmamentV2PmiKind.DatumPlane).ToArray(), bound.Where(r => r.Kind is FirmamentV2PmiKind.HoleDiameter or FirmamentV2PmiKind.Distance).ToArray(), bound.Where(r => r.Kind is FirmamentV2PmiKind.Flatness or FirmamentV2PmiKind.Parallel or FirmamentV2PmiKind.Perpendicular or FirmamentV2PmiKind.Coplanar).ToArray(), diagnostics.Where(d => d.StartsWith("firmament-v2-pmi-", StringComparison.Ordinal)).ToArray());
        return (legacy, block, boundBlock);
    }

    private static Dictionary<string, FirmamentV2PmiField> ParsePmiFields(string body, int baseOffset, List<string> diagnostics)
    {
        var fields = new Dictionary<string, FirmamentV2PmiField>(StringComparer.Ordinal);
        // Capture every field first.  A whitelist-only capture silently lost
        // misspellings such as Tolerence, precisely what canonical PMI must not do.
        var fieldRegex = new Regex(@"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<value>.*?)(?=\s+[A-Za-z_][A-Za-z0-9_]*\s*:|$)", RegexOptions.CultureInvariant | RegexOptions.Singleline);
        foreach (Match m in fieldRegex.Matches(body))
        {
            var n = NormalizePmiField(m.Groups["name"].Value);
            var v = m.Groups["value"].Value.Trim();
            if (v.EndsWith("}", StringComparison.Ordinal)) v = v[..^1].Trim();
            if (string.Equals(m.Groups["name"].Value, "DatumRefs", StringComparison.Ordinal)
                && v.StartsWith("[", StringComparison.Ordinal) && v.EndsWith("]", StringComparison.Ordinal))
                v = v[1..^1].Trim();
            if (fields.ContainsKey(n)) { diagnostics.Add(PmiDuplicateField); continue; }
            fields[n] = new(n, v, new FirmamentV2SourceSpan(baseOffset + m.Index, m.Length), null);
        }
        if (fields.Count == 0 && body.Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries).Any(l => l.Trim().Length > 0)) diagnostics.Add(PmiUnsupported);
        return fields;
    }

    private static string NormalizePmiKind(string raw) => raw switch
    {
        "Datum" => "datum",
        "HoleDiameter" => "diameter",
        "Distance" => "distance",
        "Flatness" => "flatness",
        "Parallel" => "parallel",
        "Perpendicular" => "perpendicular",
        "Coplanar" => "coplanar",
        _ => raw
    };

    private static string NormalizePmiField(string raw) => raw switch
    {
        "Target" => "target",
        "TargetA" => "targetA",
        "TargetB" => "targetB",
        "Dimension" => "dimension",
        "Value" => "value",
        "Diameter" => "diameter",
        "Tolerance" => "tolerance",
        "Datum" => "datum",
        "DatumRefs" => "datum",
        _ => raw
    };

    private static bool TryPmiKind(string raw, out FirmamentV2PmiKind kind) { kind = raw switch { "diameter"=>FirmamentV2PmiKind.HoleDiameter, "datum"=>FirmamentV2PmiKind.DatumPlane, "distance"=>FirmamentV2PmiKind.Distance, "flatness"=>FirmamentV2PmiKind.Flatness, "parallel"=>FirmamentV2PmiKind.Parallel, "perpendicular"=>FirmamentV2PmiKind.Perpendicular, "coplanar"=>FirmamentV2PmiKind.Coplanar, _=>default }; return raw is "diameter" or "datum" or "distance" or "flatness" or "parallel" or "perpendicular" or "coplanar"; }

    private static void ValidatePmiFieldSet(string kind, string name, IReadOnlyDictionary<string,FirmamentV2PmiField> fields, List<string> diagnostics)
    {
        string[] required = kind switch { "datum"=>["target"], "diameter"=> fields.ContainsKey("dimension") ? ["target","dimension"] : fields.ContainsKey("diameter") ? ["target","diameter"] : ["target","value"], "distance"=>["targetA","targetB","dimension"], "flatness"=>["target","tolerance"], "parallel" or "perpendicular" or "coplanar"=>["target","datum","tolerance"], _=>[] };
        var allowed = required.Concat(kind=="diameter"?["tolerance","diameter","value","dimension","datum"]:[]).ToHashSet(StringComparer.Ordinal);
        foreach (var r in required) if (!fields.ContainsKey(r)) diagnostics.Add(PmiMissingRequiredField);
        foreach (var f in fields.Keys) if (!allowed.Contains(f)) diagnostics.Add(PmiUnknownField);
    }

    private static bool TryBindPmiRecord(FirmamentV2PmiRecord r, Dictionary<string,FirmamentV2SolidBinding> solids, IReadOnlyList<FirmamentV2ModifyBlock> modifyBlocks, IReadOnlyDictionary<string,FirmamentV2RecognizedRegion> regions, HashSet<string> aliases, HashSet<string> holeNames, IReadOnlyDictionary<string,FirmamentV2BoundLet> lets, IReadOnlyDictionary<string,FirmamentV2BoundLetRecord> records, HashSet<string> datums, List<string> diagnostics, out FirmamentV2BoundPmiRecord bound)
    {
        bound=null!;
        string? target = Field(r,"target"); string? targetA=Field(r,"targetA"); string? targetB=Field(r,"targetB");
        var targets = new[]{target,targetA,targetB}.Where(t=>t is not null).Cast<string>().ToArray();
        FirmamentV2HoleWallSelector? wallTarget = null;
        foreach (var t in targets)
        {
            if (!FirmamentV2HoleWallSelector.TryParse(t, out var holeName))
            {
                ValidateTarget(t, r.Kind, solids, regions, aliases, holeNames, diagnostics);
                continue;
            }
            if (r.Kind != FirmamentV2PmiKind.HoleDiameter) { diagnostics.Add($"{HoleWallConsumerInvalid}:{r.Name}"); continue; }
            var owners = modifyBlocks.Where(block => block.SemanticHoles.Any(hole => hole.Name == holeName)).Select(block => block.TargetSolid).Distinct(StringComparer.Ordinal).ToArray();
            if (owners.Length > 1) diagnostics.Add($"{HoleWallAmbiguous}:{holeName}");
            else if (owners.Length == 0) diagnostics.Add($"{(solids.ContainsKey(holeName) ? HoleWallNotHole : HoleWallUnknownHole)}:{holeName}");
            else wallTarget = new(holeName, owners[0]);
        }
        FirmamentV2LiteralValue? dim=null; FirmamentV2Tolerance? dimTol=null; FirmamentV2LiteralValue? ctlTol=null; var datumRefs=new List<string>();
        if (r.Kind is FirmamentV2PmiKind.HoleDiameter or FirmamentV2PmiKind.Distance)
        {
            if (Field(r,"dimension") is string d) { var b=ResolveBoundLet(d, lets, records); if (b is null) diagnostics.Add(PmiDimensionTypeMismatch); else if (b.Type!=FirmamentV2PrimitiveType.Length) diagnostics.Add(PmiDimensionTypeMismatch); else { dim=b.Value; dimTol=b.Tolerance; } }
            else if (Field(r,"value") is string v && TryParsePmiLength(NormalizePmiLengthLiteral(v), diagnostics, out var lit)) dim=lit;
            else if (Field(r,"diameter") is string legacy && TryParsePmiLength(NormalizePmiLengthLiteral(legacy), diagnostics, out var legacyLit)) dim=legacyLit;
            if (Field(r,"tolerance") is string t && TryParseToleranceLiteral(NormalizePmiLengthLiteral(t), diagnostics, out var tol)) dimTol=tol;
            if (dim is null) { diagnostics.Add(PmiDiameterInvalid); return false; }
            if (r.Kind == FirmamentV2PmiKind.HoleDiameter && dim.NumericValue is <= 0) { diagnostics.Add(PmiDiameterInvalid); return false; }
            if (dimTol is null && Field(r,"diameter") is null && Field(r,"value") is null) { diagnostics.Add(PmiDimensionMissingTolerance); return false; }
        }
        if (r.Kind is FirmamentV2PmiKind.Flatness or FirmamentV2PmiKind.Parallel or FirmamentV2PmiKind.Perpendicular or FirmamentV2PmiKind.Coplanar)
        { if (Field(r,"tolerance") is string t && TryParsePmiLength(NormalizePmiLengthLiteral(t), diagnostics, out var lit)) ctlTol=lit; else diagnostics.Add(PmiToleranceTypeMismatch); }
        if (r.Kind is FirmamentV2PmiKind.Parallel or FirmamentV2PmiKind.Perpendicular or FirmamentV2PmiKind.Coplanar)
        { var d=Field(r,"datum"); if (d is null || !datums.Contains(d)) diagnostics.Add(PmiUnknownDatum); else datumRefs.Add(d); }
        if (r.Kind == FirmamentV2PmiKind.HoleDiameter && Field(r,"datum") is string datum)
        { if (!datums.Contains(datum)) diagnostics.Add(PmiUnknownDatum); else datumRefs.Add(datum); }
        var dimensionSource = Field(r, "dimension");
        var dimensionBinding = dimensionSource is null ? null : ResolveBoundLet(dimensionSource, lets, records);
        bound=new(r.Kind,r.Name,targets,dim,dimTol,ctlTol,datumRefs,r.SourceSpan,
            ProjectionSource: dimensionBinding?.Origins.Any(o => o.Binding == dimensionSource) == true ? dimensionSource
                : dimensionBinding?.Origins.FirstOrDefault()?.Binding ?? dimensionSource, HoleWallTarget: wallTarget)
            { DimensionOrigins = dimensionBinding?.Origins ?? [] };
        return !diagnostics.Any(x => (x.StartsWith("firmament-v2-pmi-", StringComparison.Ordinal) && x is not PmiUnsupported)
            || x.StartsWith("firmament-v2-hole-wall-", StringComparison.Ordinal));
    }
    private static string? Field(FirmamentV2PmiRecord r,string name)=>r.Fields.TryGetValue(name,out var f)?f.Source:null;
    private static FirmamentV2BoundLet? ResolveBoundLet(string s,IReadOnlyDictionary<string,FirmamentV2BoundLet> lets,IReadOnlyDictionary<string,FirmamentV2BoundLetRecord> records){var m=DottedReferenceRegex.Match(s); if(m.Success&&records.TryGetValue(m.Groups["record"].Value,out var rec)&&rec.Fields.TryGetValue(m.Groups["field"].Value,out var f))return f; return lets.TryGetValue(s,out var l)?l:null;}
    private static string NormalizePmiLengthLiteral(string s) => Regex.Replace(s.Trim(), @"(?<=\d)\s+(?=mm$)", "", RegexOptions.CultureInvariant);
    private static bool TryParsePmiLength(string s,List<string> d,out FirmamentV2LiteralValue lit){lit=null!; var v=ParseLetLiteral(FirmamentV2PrimitiveType.Length,s,d); if(v is null){d.Add(PmiToleranceTypeMismatch); return false;} if(v.NumericValue is < 0){ d.Add(PmiDiameterInvalid); return false; } lit=v; return true;}
    private static bool TryParseToleranceLiteral(string s,List<string>d,out FirmamentV2Tolerance tol)
    {
        tol=null!;
        var plusMinus = Regex.Match(s, @"^PlusMinus\(\s*(?<plus>[-+]?\d+(?:\.\d+)?)mm\s*,\s*(?<minus>[-+]?\d+(?:\.\d+)?)mm\s*\)$", RegexOptions.CultureInvariant);
        if (plusMinus.Success)
        {
            var plus = double.Parse(plusMinus.Groups["plus"].Value, CultureInfo.InvariantCulture);
            var minus = double.Parse(plusMinus.Groups["minus"].Value, CultureInfo.InvariantCulture);
            if (plus < 0 || minus < 0) { d.Add(PmiToleranceTypeMismatch); return false; }
            tol = new(FirmamentV2ToleranceKind.Asymmetric, plus, minus, "mm", FirmamentV2PrimitiveType.Length, new(0, s.Length));
            return true;
        }
        var t=ParseTolerance(FirmamentV2PrimitiveType.Length,s,d); if(t is null){d.Add(PmiToleranceTypeMismatch); return false;} tol=t; return true;
    }
    private static void ValidateTarget(string target,FirmamentV2PmiKind kind,Dictionary<string,FirmamentV2SolidBinding> solids,IReadOnlyDictionary<string,FirmamentV2RecognizedRegion> regions,HashSet<string> aliases,HashSet<string> holeNames,List<string> diagnostics)
    { if (TryValidateImportedFaceTarget(target, solids, diagnostics)) return; if (RecognizedRegionTargetRegex.IsMatch(target)){ var m=RecognizedRegionTargetRegex.Match(target); var body=m.Groups["body"].Value; if(regions.TryGetValue(target,out var region)){ if((kind==FirmamentV2PmiKind.HoleDiameter&&region.Kind!="holeShaft") || (kind==FirmamentV2PmiKind.DatumPlane&&region.Kind!="datumPlane")) diagnostics.Add(PmiRecognizedRegionKindMismatch); } else if(regions.Values.Any(r=>r.BodyName==body) || diagnostics.Contains(UnknownRecognitionFace)) diagnostics.Add(UnknownRecognitionRegion); return;} if(aliases.Contains(target)||FaceSelectorRegex.IsMatch(target)||holeNames.Contains(target))return; if(!target.Contains(".region(",StringComparison.Ordinal)) { diagnostics.Add(PmiInvalidTarget); diagnostics.Add(PmiTargetUnresolved); } }

    private static bool TryValidateImportedFaceTarget(string target, Dictionary<string, FirmamentV2SolidBinding> solids, List<string> diagnostics)
    {
        if (target.Contains(".face(", StringComparison.Ordinal) && !ImportedFaceTargetRegex.IsMatch(target))
        {
            diagnostics.Add(PmiInvalidImportedTarget);
            return false;
        }

        var imported = ImportedFaceTargetRegex.Match(target);
        if (!imported.Success) return false;
        var body = imported.Groups["body"].Value;
        var entity = imported.Groups["entity"].Value;
        if (!solids.TryGetValue(body, out var solid))
        {
            diagnostics.Add(InlineStepUnknownBody);
            return false;
        }

        if (solid.InlineStep is null)
        {
            diagnostics.Add(PmiImportedTargetRequiresCanonicalStep);
            return false;
        }

        if (!solid.InlineStep.CanonicalInput)
        {
            diagnostics.Add(PmiImportedTargetRequiresCanonicalStep);
            return false;
        }

        if (!solid.InlineStep.TopologyMap.TryResolveFaceEntity(entity, out _))
        {
            diagnostics.Add(InlineStepUnknownFace);
            return false;
        }

        return true;
    }

    private static bool TryParsePositiveNumberWithOptionalMm(string raw, out double value)
    {
        raw = raw.Trim();
        if (raw.EndsWith("mm", StringComparison.Ordinal)) raw = raw[..^2];
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value) && value > 0d;
    }

    private static bool ContainsUnsupportedConstruct(string source) =>
        Regex.IsMatch(source, @"\b(PMI|where|add|shell|fillet|chamfer|regions|profile)\b|<\s*Process\s*>", RegexOptions.CultureInvariant);

    private static bool ContainsRawBackendId(string source) =>
        Regex.IsMatch(source, @"\b(brep|backend|coedge)\s*\.|STEP\s*#", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static int FindMatchingBrace(string source, int open)
    {
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0) return i;
        }
        return -1;
    }

    /// <summary>Exposes the canonical static-erasure boundary to inspection clients.
    /// Materialization and inspection therefore observe identical generated feature identities.</summary>
    public static bool TryExpandCanonicalStaticAuthoring(string source, out string expandedSource, out IReadOnlyList<string> diagnostics)
    {
        var collected = new List<string>();
        var expanded = CanonicalStaticAuthoring.Expand(source, collected);
        expandedSource = expanded?.Source ?? source;
        diagnostics = collected.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return expanded is not null;
    }

    private static string MaskCommentTrivia(string sourceText)
    {
        // Legacy semantic readers still need trivia erased. Use the shared lexer,
        // preserving offsets and quoted // text; do not erase malformed tokens.
        var text = sourceText.ToCharArray();
        foreach (var comment in FirmamentLanguageAnalysisService.Lex(sourceText, lossless: true)
            .Where(t => t.Shape == "comment" && t.Error is null))
            for (var i = comment.Start; i < comment.Start + comment.Length; i++)
                if (text[i] is not ('\r' or '\n')) text[i] = ' ';
        return new(text);
    }
}
