using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public static class FirmamentV2Parser
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

    public const string ModifyTargetUnresolved = "firmament-v2-modify-target-unresolved";
    public const string ModifyTargetNotSolid = "firmament-v2-modify-target-not-solid";
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

    private static readonly Regex ModelRegex = new(@"\bmodel\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex UnitsRegex = new(@"\bunits\s+(?<units>[A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> ReservedWords = new(StringComparer.Ordinal) { "let", "model", "units", "solid", "modify", "region", "cut", "template", "concept", "pmi", "recognize", "replace", "with", "manufacturing", "process", "feature", "true", "false", "int", "float", "length", "angle", "string", "bool", "tol" };
    private static readonly Regex LetRegex = new(@"\blet\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<type>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<literal>[^\r\n{}]+)", RegexOptions.CultureInvariant);
    private static readonly Regex LetRecordHeaderRegex = new(@"^\s*let\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant | RegexOptions.Multiline);
    private static readonly Regex LetRecordFieldRegex = new(@"^\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<type>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<literal>.+?)\s*$", RegexOptions.CultureInvariant);
    private static readonly Regex DottedReferenceRegex = new(@"^(?<record>[A-Za-z_][A-Za-z0-9_]*)\.(?<field>[A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.CultureInvariant);
    private static readonly Regex SolidHeaderRegex = new(@"\bsolid\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<target>[A-Za-z_][A-Za-z0-9_]*)(?<with>\s+with)?\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex LegacyEqualsSolidRegex = new(@"\bsolid\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<target>[A-Za-z_][A-Za-z0-9_]*)(?<with>\s+with)?\s*\{(?<body>.*?)\}", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex SizeRegex = new(@"\bsize\s*:\s*\[(?<values>[^\]]*)\]", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex ScalarFieldRegex = new(@"\b(?<field>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<value>[-+0-9.eE]+(?:mm)?)", RegexOptions.CultureInvariant);
    private static readonly Regex FieldRegex = new(@"(?<field>@[A-Za-z_][A-Za-z0-9_]*|[A-Za-z_][A-Za-z0-9_\.]*)\s*:", RegexOptions.CultureInvariant);
    private static readonly Regex ExposeRegex = new(@"\bexpose\s*\{(?<body>.*?)\}", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex ExposureLineRegex = new(@"^\s*(?<selector>.+?)\s*=>\s*(?<alias>[A-Za-z_][A-Za-z0-9_]*)\s*$", RegexOptions.CultureInvariant);
    private static readonly Regex FaceSelectorRegex = new(@"^face\((?<axis>[+-][XYZ])\)(?<sub>\.[A-Za-z_][A-Za-z0-9_]*)?$", RegexOptions.CultureInvariant);
    private static readonly Regex ModifyHeaderRegex = new(@"\bmodify\s+(?<target>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex RegionHeaderRegex = new(@"\bregion\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s+on\s+(?<target>face\([^)]*\)|[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex CutHeaderRegex = new(@"\bcut\s+(?<tool>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex RadiusRegex = new(@"\bradius\s*:\s*(?<value>[^\s}]+)", RegexOptions.CultureInvariant);
    private static readonly Regex ThroughRegex = new(@"\bthrough\s*:\s*(?<target>face\([^)]*\)|[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant);
    private static readonly Regex CenterRegex = new(@"\bcenter\s*:\s*\[(?<values>[^\]]*)\]", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex SemanticHoleHeaderRegex = new(@"\bhole\s*<\s*(?<variant>[A-Za-z_][A-Za-z0-9_]*)\s*>\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
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
    private static readonly Regex PmiHeaderRegex = new(@"\bpmi\s*\{", RegexOptions.CultureInvariant);
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
    private static readonly Regex CanonicalPrimitiveHeaderRegex = new(@"\b(?<type>Box|Cylinder|RoundedBox|Frustum)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex CanonicalInlineStepHeaderRegex = new(@"\bInlineStep\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex CanonicalModifyHeaderRegex = new(@"\bModify\s+(?<target>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    // Match the declaration-shaped header first; variant admission belongs in the
    // binder so a misspelled family can never disappear from a Modify block.
    private static readonly Regex CanonicalHoleHeaderRegex = new(@"\bHole\s*<\s*(?<variant>[A-Za-z_][A-Za-z0-9_]*)\s*>\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex CanonicalEdgeHeaderRegex = new(@"\bEdgeFinish\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex CanonicalRecognizeHeaderRegex = new(@"\bRecognize\s+(?<body>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex CanonicalRegionHeaderRegex = new(@"\bRegion\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex CanonicalReplaceHeaderRegex = new(@"\bReplace\s+(?<body>[A-Za-z_][A-Za-z0-9_]*)\.(?<region>[A-Za-z_][A-Za-z0-9_]*)\s+With\s+(?<kind>[A-Za-z_][A-Za-z0-9_]*(?:\s*<\s*[A-Za-z_][A-Za-z0-9_]*\s*>)?)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);

    public static FirmamentV2ParseResult Parse(string sourceText) => Parse(sourceText, null, null);

    public static FirmamentV2ParseResult Parse(string sourceText, string? sourceDirectory) => Parse(sourceText, sourceDirectory, null);

    public static FirmamentV2ParseResult Parse(
        string sourceText,
        string? sourceDirectory,
        FirmamentV2ForgeConceptCatalog? conceptCatalog)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        var diagnostics = new List<string> { "firmament-v2-parser-invoked" };
        var source = StripLineComments(sourceText);
        var v2AdmissionCandidate = IsV2AdmissionCandidate(source);
        conceptCatalog ??= FirmamentV2ForgeConceptRegistry.Catalog;

        var canonicalStaticExpansion = CanonicalStaticAuthoring.Expand(source, diagnostics);
        if (canonicalStaticExpansion is null)
        {
            diagnostics.Add("firmament-v2-parse-failed");
            return FirmamentV2ParseResult.Failure(diagnostics.Distinct(StringComparer.Ordinal).Order().ToArray(),
                v2AdmissionCandidate ? FirmamentV2ParseDisposition.RecognizedInvalid : FirmamentV2ParseDisposition.NotRecognized);
        }
        source = canonicalStaticExpansion.Source;

        var templateExpansion = FirmamentV2TemplateExpansion.Expand(source, diagnostics);
        if (templateExpansion is null)
        {
            diagnostics.Add("firmament-v2-parse-failed");
            return FirmamentV2ParseResult.Failure(
                diagnostics.Distinct(StringComparer.Ordinal).Order().ToArray(),
                v2AdmissionCandidate ? FirmamentV2ParseDisposition.RecognizedInvalid : FirmamentV2ParseDisposition.NotRecognized);
        }
        source = templateExpansion.Source;
        v2AdmissionCandidate |= IsV2AdmissionCandidate(source);

        // Canonical V2 is parsed as one document, before compatibility adapters.  The
        // adapters below remain only for old fixtures and no longer define the language
        // an author must select for ordinary mechanical work.
        if (CanonicalModelRegex.IsMatch(source))
        {
            var parsed = Recognized(ContainsCanonicalAdvancedDeclaration(source)
                ? ParseCanonicalAdvancedDocument(source, diagnostics, templateExpansion.Instantiations)
                : ParseCanonicalDocument(source, diagnostics, sourceDirectory));
            if (parsed.Document is null) return parsed;

            var normalized = parsed.Document with { StaticAuthoring = canonicalStaticExpansion.Document };
            var symbolBinding = FirmamentV2CanonicalSymbolBinder.Bind(normalized, source);
            if (symbolBinding.Diagnostics.Count > 0)
            {
                return FirmamentV2ParseResult.Failure(
                    parsed.Diagnostics.Concat(symbolBinding.Diagnostics).Distinct(StringComparer.Ordinal).Order().ToArray(),
                    FirmamentV2ParseDisposition.RecognizedInvalid);
            }

            return parsed with
            {
                Document = normalized with { SymbolTable = symbolBinding.Table },
                Diagnostics = parsed.Diagnostics.Append("firmament-v2-unified-canonical-symbols-bound").Distinct(StringComparer.Ordinal).Order().ToArray()
            };
        }

        if (Regex.IsMatch(source, @"\b(?:RoundedBox|Frustum|Box)\s*<\s*[A-Za-z_][A-Za-z0-9_]*\s*>\s+[A-Za-z_]", RegexOptions.CultureInvariant))
            return Recognized(ParsePrimitiveConstructionPolicyDocument(source, diagnostics));

        if (Regex.IsMatch(source, @"\bRoundedBox\s+[A-Za-z_]", RegexOptions.CultureInvariant))
            return Recognized(ParseRoundedBoxModelingDocument(source, diagnostics));

        if (Regex.IsMatch(source, @"\bConcept\s+(?:Struct\s+)?[A-Za-z_]", RegexOptions.CultureInvariant)
            || Regex.IsMatch(source, @"\b(?:Struct|Model)\s+[A-Za-z_][A-Za-z0-9_]*\s*(?::\s*[A-Za-z_][A-Za-z0-9_]*)?\s*\{", RegexOptions.CultureInvariant))
            return Recognized(ParseConceptModelingDocument(source, diagnostics, templateExpansion.Instantiations));

        if (Regex.IsMatch(source, @"^\s*Model\b", RegexOptions.CultureInvariant))
            return Recognized(ParsePhase3ModelingDocument(source, diagnostics));

        if (ContainsRawBackendId(source)) diagnostics.Add(RawBackendIdReferenceForbidden);
        if (ContainsUnsupportedConstruct(source)) diagnostics.Add(UnsupportedConstruct);

        var modelMatch = ModelRegex.Match(source);
        if (!modelMatch.Success) diagnostics.Add(MissingModel);

        var unitsMatch = UnitsRegex.Match(source);
        if (!unitsMatch.Success) diagnostics.Add(MissingUnits);
        else if (!string.Equals(unitsMatch.Groups["units"].Value, "mm", StringComparison.Ordinal)) diagnostics.Add(UnsupportedConstruct);

        var solidMatches = FindSolids(source).ToArray();
        if (solidMatches.Length == 0 && LegacyEqualsSolidRegex.IsMatch(source)) diagnostics.Add(UnsupportedConstruct);
        var appearsStandaloneLattice = Regex.IsMatch(source, @"\bfill\s+[A-Za-z_][A-Za-z0-9_]*\s*\{[\s\S]*?\bpattern\s*:\s*CubicTruss\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (solidMatches.Length == 0 && !appearsStandaloneLattice) diagnostics.Add(MissingSolid);

        var solids = new List<FirmamentV2SolidBinding>();
        var byName = new Dictionary<string, FirmamentV2SolidBinding>(StringComparer.Ordinal);
        if (modelMatch.Success && unitsMatch.Success)
        {
            foreach (var solid in solidMatches)
            {
                var name = solid.Name;
                var target = solid.Target;
                var body = solid.Body;
                var isWith = solid.IsWith;
                if (byName.ContainsKey(name)) { diagnostics.Add(DuplicateName); continue; }

                FirmamentV2SolidBinding? binding = isWith
                    ? ParseDerived(name, target, body, byName, diagnostics)
                    : ParseDirect(name, target, body, diagnostics, sourceDirectory);
                if (binding is not null)
                {
                    solids.Add(binding);
                    byName.Add(name, binding);
                }
            }
        }

        var letRecords = ParseLetRecords(source, byName.Keys, diagnostics);
        var lets = ParseLets(source, byName.Keys.Concat(letRecords.Select(r => r.Name)), diagnostics);
        var boundLetRecords = BindLetRecords(letRecords);
        var boundLets = BindLets(lets, boundLetRecords, byName.Keys, diagnostics);

        var modifyBlocks = ParseModifyBlocks(source, byName, diagnostics);
        var templates = ParseTemplates(source, diagnostics);
        var latticeFills = ParseLatticeFills(source, byName, diagnostics);
        var standaloneLatticeFills = ParseStandaloneLatticeFills(source, diagnostics);
        var recognizedRegions = ParseRecognizedRegions(source, byName, diagnostics);
        var replacements = ParseReplacements(source, byName, recognizedRegions, diagnostics);
        var (pmi, pmiBlock, boundPmi) = ParsePmi(source, byName, modifyBlocks, recognizedRegions, boundLets, boundLetRecords, diagnostics);
        var (manufacturingConcepts, featureConcepts) = ParseConceptApplications(source, boundLets, boundLetRecords, conceptCatalog, diagnostics);

        FirmamentV2Document? document = null;
        if (modelMatch.Success && unitsMatch.Success && (solids.Count > 0 || standaloneLatticeFills.Count > 0) && !diagnostics.Any(d => d.StartsWith("firmament-v2-fill-", StringComparison.Ordinal)))
            document = new FirmamentV2Document(modelMatch.Groups["name"].Value, unitsMatch.Groups["units"].Value, solids, modifyBlocks, templates, pmi, recognizedRegions, replacements, lets, boundLets, letRecords, boundLetRecords, manufacturingConcepts, featureConcepts, pmiBlock, boundPmi, null, latticeFills, standaloneLatticeFills);

        var hasFatalDiagnostics = diagnostics.Any(IsFatalDiagnosticCode);
        diagnostics.Add(document is null || hasFatalDiagnostics ? "firmament-v2-parse-failed" : "firmament-v2-parse-succeeded");
        diagnostics.Sort(StringComparer.Ordinal);
        if (!v2AdmissionCandidate)
            return FirmamentV2ParseResult.Failure(diagnostics, FirmamentV2ParseDisposition.NotRecognized);

        return document is null
            ? FirmamentV2ParseResult.Failure(diagnostics)
            : new FirmamentV2ParseResult(
                !hasFatalDiagnostics,
                document,
                diagnostics,
                hasFatalDiagnostics ? FirmamentV2ParseDisposition.RecognizedInvalid : FirmamentV2ParseDisposition.RecognizedValid);
    }

    private static FirmamentV2ParseResult Recognized(FirmamentV2ParseResult result) =>
        result with
        {
            Disposition = result.IsSuccess
                ? FirmamentV2ParseDisposition.RecognizedValid
                : FirmamentV2ParseDisposition.RecognizedInvalid
        };

    private static bool IsV2AdmissionCandidate(string source) =>
        ModelRegex.IsMatch(source)
        || Regex.IsMatch(source, @"^\s*model\s+[A-Za-z_][A-Za-z0-9_]*\b", RegexOptions.CultureInvariant)
        || TemplateHeaderRegex.IsMatch(source)
        || Regex.IsMatch(source, @"\b(?:RoundedBox|Frustum|Box)\s*<\s*[A-Za-z_][A-Za-z0-9_]*\s*>\s+[A-Za-z_]", RegexOptions.CultureInvariant)
        || Regex.IsMatch(source, @"\bRoundedBox\s+[A-Za-z_]", RegexOptions.CultureInvariant)
        || Regex.IsMatch(source, @"\bConcept\s+(?:Struct\s+)?[A-Za-z_]", RegexOptions.CultureInvariant)
        || Regex.IsMatch(source, @"\b(?:Struct|Model)\s+[A-Za-z_][A-Za-z0-9_]*\s*(?::\s*[A-Za-z_][A-Za-z0-9_]*)?\s*\{", RegexOptions.CultureInvariant)
        || Regex.IsMatch(source, @"^\s*Model\b", RegexOptions.CultureInvariant);

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

    private static bool TryParseLetType(string text, out FirmamentV2PrimitiveType type)
    {
        type = text switch
        {
            "int" => FirmamentV2PrimitiveType.Int,
            "float" => FirmamentV2PrimitiveType.Float,
            "length" => FirmamentV2PrimitiveType.Length,
            "angle" => FirmamentV2PrimitiveType.Angle,
            "string" => FirmamentV2PrimitiveType.String,
            "bool" => FirmamentV2PrimitiveType.Bool,
            _ => default
        };
        return text is "int" or "float" or "length" or "angle" or "string" or "bool";
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

    private static IReadOnlyList<FirmamentV2BoundLet> BindLets(IReadOnlyList<FirmamentV2LetDeclaration> lets, IReadOnlyList<FirmamentV2BoundLetRecord> records, IEnumerable<string> solidNames, List<string> diagnostics)
    {
        var binder = new ExpressionBinder(lets, records, solidNames, diagnostics);
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
        public ExpressionBinder(IReadOnlyList<FirmamentV2LetDeclaration> lets, IReadOnlyList<FirmamentV2BoundLetRecord> records, IEnumerable<string> solidNames, List<string> diagnostics)
        { _lets = lets; _records = records.ToDictionary(r => r.Name, StringComparer.Ordinal); _solidNames = new(solidNames, StringComparer.Ordinal); _diagnostics = diagnostics; _letMap = lets.ToDictionary(l => l.Name, StringComparer.Ordinal); }
        public IReadOnlyList<FirmamentV2BoundLet> BindLets() { foreach (var l in _lets) Bind(l.Name); return _lets.Where(l => _bound.ContainsKey(l.Name)).Select(l => _bound[l.Name]).ToArray(); }
        private FirmamentV2BoundLet? Bind(string name)
        {
            if (_bound.TryGetValue(name, out var existing)) return existing;
            if (!_letMap.TryGetValue(name, out var let)) return null;
            if (!_visiting.Add(name)) { _diagnostics.Add(ExpressionCycle); return null; }
            var expr = Eval(let.ValueExpression);
            _visiting.Remove(name);
            if (expr is null) return null;
            if (expr.InferredType != let.DeclaredType) { _diagnostics.Add(ExpressionTypeMismatch); _diagnostics.Add(LetTypeMismatch); return null; }
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
            _ => null
        };
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
            return new(type.Value, val, new HashSet<string>(l.Dependencies.Concat(r.Dependencies), StringComparer.Ordinal), new(0, b.Source.Length), null, l.AliasTolerance is not null || r.AliasTolerance is not null || l.UsesTolerancedValueInArithmetic || r.UsesTolerancedValueInArithmetic);
        }
        private static bool IsZero(FirmamentV2LiteralValue v) => v.NumericValue is double d && d == 0d;
        private static FirmamentV2PrimitiveType? ResultType(FirmamentV2PrimitiveType a, string op, FirmamentV2PrimitiveType b) => op switch
        {
            "+" or "-" when a == b && a is FirmamentV2PrimitiveType.Int or FirmamentV2PrimitiveType.Float or FirmamentV2PrimitiveType.Length or FirmamentV2PrimitiveType.Angle => a,
            "+" or "-" when IsNumeric(a) && IsNumeric(b) => FirmamentV2PrimitiveType.Float,
            "*" when IsNumeric(a) && IsNumeric(b) => a == FirmamentV2PrimitiveType.Int && b == FirmamentV2PrimitiveType.Int ? FirmamentV2PrimitiveType.Int : FirmamentV2PrimitiveType.Float,
            "*" when IsDim(a) && IsNumeric(b) => a,
            "*" when IsNumeric(a) && IsDim(b) => b,
            "/" when IsNumeric(a) && IsNumeric(b) => FirmamentV2PrimitiveType.Float,
            "/" when IsDim(a) && IsNumeric(b) => a,
            "/" when IsDim(a) && a == b => FirmamentV2PrimitiveType.Float,
            _ => null
        };
        private static bool IsNumeric(FirmamentV2PrimitiveType t) => t is FirmamentV2PrimitiveType.Int or FirmamentV2PrimitiveType.Float;
        private static bool IsDim(FirmamentV2PrimitiveType t) => t is FirmamentV2PrimitiveType.Length or FirmamentV2PrimitiveType.Angle;
        private static FirmamentV2LiteralValue Compute(FirmamentV2PrimitiveType type, FirmamentV2LiteralValue l, string op, FirmamentV2LiteralValue r, string raw)
        {
            var a = Convert.ToDouble(l.NumericValue, CultureInfo.InvariantCulture); var b = Convert.ToDouble(r.NumericValue, CultureInfo.InvariantCulture);
            var d = op switch { "+" => a + b, "-" => a - b, "*" => a * b, "/" => a / b, _ => 0 };
            object value = type == FirmamentV2PrimitiveType.Int ? (object)(int)d : d;
            return new(type, value, d, type == FirmamentV2PrimitiveType.Length ? "mm" : type == FirmamentV2PrimitiveType.Angle ? "deg" : null, raw);
        }
    }

    private static FirmamentV2SolidBinding? ParseDirect(string name, string recordType, string body, List<string> diagnostics, string? sourceDirectory)
    {
        if (string.Equals(recordType, "Box", StringComparison.Ordinal))
        {
            var values = ParseSizeField(body, diagnostics, BoxMissingSize);
            var exposures = ParseExposures(body, diagnostics);
            return values is null ? null : new(name, "Box", new FirmamentV2BoxRecord(values, exposures));
        }

        if (string.Equals(recordType, "InlineStep", StringComparison.Ordinal))
        {
            return ParseInlineStep(name, body, diagnostics, sourceDirectory);
        }

        if (recordType is not ("Cylinder" or "Cone" or "Sphere" or "Torus")) { diagnostics.Add(UnknownRecordType); return null; }

        var scalars = ParseScalarFields(body, diagnostics);
        FirmamentV2PrimitiveRecord? primitive = recordType switch
        {
            "Cylinder" => RequireFields(scalars, diagnostics, "radius", "height") is null ? null : new FirmamentV2CylinderRecord(scalars["radius"], scalars["height"]),
            "Cone" => RequireFields(scalars, diagnostics, "bottomRadius", "topRadius", "height") is null ? null : new FirmamentV2ConeRecord(scalars["bottomRadius"], scalars["topRadius"], scalars["height"]),
            "Sphere" => RequireFields(scalars, diagnostics, "radius") is null ? null : new FirmamentV2SphereRecord(scalars["radius"]),
            "Torus" => RequireFields(scalars, diagnostics, "majorRadius", "minorRadius") is null ? null : new FirmamentV2TorusRecord(scalars["majorRadius"], scalars["minorRadius"]),
            _ => null
        };
        if (primitive is null) { return null; }
        ValidatePrimitive(recordType, scalars, diagnostics);
        return diagnostics.Contains(DegenerateDimension) || diagnostics.Contains(PrimitiveFieldInvalid) || diagnostics.Contains(PrimitiveFieldUnknown) || diagnostics.Contains(PrimitiveFieldMissing)
            ? null
            : new(name, recordType, primitive);
    }

    private static FirmamentV2SolidBinding? ParseInlineStep(string name, string body, List<string> diagnostics, string? sourceDirectory)
    {
        var match = Regex.Match(body, "\\b(?:path|Path)\\s*:\\s*\\\"(?<path>[^\\\"]+)\\\"", RegexOptions.CultureInvariant);
        if (!match.Success) { diagnostics.Add(InlineStepPathMissing); return null; }

        var sourcePath = match.Groups["path"].Value;
        if (Path.IsPathRooted(sourcePath))
        {
            diagnostics.Add(InlineStepPathInvalid);
            return null;
        }

        var baseDirectory = string.IsNullOrWhiteSpace(sourceDirectory) ? Directory.GetCurrentDirectory() : sourceDirectory;
        var normalizedPath = Path.GetFullPath(Path.Combine(baseDirectory!, sourcePath));
        if (!File.Exists(normalizedPath))
        {
            diagnostics.Add(InlineStepFileMissing);
            return null;
        }

        var stepText = File.ReadAllText(normalizedPath, Encoding.UTF8);
        if (!IsAetherisCanonicalStep(stepText, out var evidence))
        {
            diagnostics.Add(InlineStepRequiresCanonical);
            return null;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stepText))).ToLowerInvariant();
        return new FirmamentV2SolidBinding(name, "InlineStep", new FirmamentV2InlineStepRecord(sourcePath, normalizedPath, hash, true, evidence, BuildImportedStepTopologyMap(stepText)));
    }

    private static ImportedStepTopologyMap BuildImportedStepTopologyMap(string stepText)
    {
        var faceEntityToFaceId = new Dictionary<string, string>(StringComparer.Ordinal);
        var faceIdToFaceEntity = new Dictionary<string, string>(StringComparer.Ordinal);
        var ordinal = 0;
        foreach (Match match in Regex.Matches(stepText, @"(?m)^\s*(#[0-9]+)\s*=\s*ADVANCED_FACE\s*\(", RegexOptions.CultureInvariant))
        {
            ordinal++;
            var entity = match.Groups[1].Value;
            var faceId = $"face-{ordinal}";
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

    private static FirmamentV2SolidBinding? ParseDerived(string name, string baseName, string body, Dictionary<string, FirmamentV2SolidBinding> byName, List<string> diagnostics)
    {
        if (!byName.TryGetValue(baseName, out var baseSolid)) { diagnostics.Add(NameUnresolved); if (Regex.IsMatch(body, @"\bsize\s*:", RegexOptions.CultureInvariant)) diagnostics.Add(WithForwardReference); return null; }
        if (!string.Equals(baseSolid.RecordType, "Box", StringComparison.Ordinal)) { diagnostics.Add(WithRequiresBoxRecord); return null; }
        var fields = FieldRegex.Matches(body).Select(m => m.Groups["field"].Value).ToArray();
        if (fields.Length == 0) { diagnostics.Add(WithFieldNotFound); return null; }
        if (fields.Any(f => f.StartsWith('@'))) { diagnostics.Add(WithRequiresRecord); return null; }
        if (ExposeRegex.IsMatch(body)) { diagnostics.Add(ExposeBlockUnsupported); return null; }
        if (fields.Any(f => !string.Equals(f, "size", StringComparison.Ordinal))) { diagnostics.Add(WithFieldNotFound); return null; }
        var values = ParseSizeField(body, diagnostics, WithFieldTypeMismatch);
        if (values is null) { diagnostics.Add(WithDerivedRecordInvalid); return null; }
        return new(name, "Box", new FirmamentV2BoxRecord(values, []), baseName, new Dictionary<string, IReadOnlyList<double>>(StringComparer.Ordinal) { ["size"] = values });
    }

    /// <summary>
    /// The author-facing V2 grammar.  It deliberately builds the same normalized
    /// document as all compatibility forms: materializer selection sees primitive
    /// and feature semantics, never the spelling that introduced them.
    /// </summary>
    private static bool ContainsCanonicalAdvancedDeclaration(string source) =>
        Regex.IsMatch(source, @"\b(?:Concept|Struct|Construction\s+Plane|Profile|Compose|Selection|Template|Enum|Pattern|Require|Match)\b", RegexOptions.CultureInvariant);

    /// <summary>
    /// Parser-owned canonical admission for advanced declarations.  The existing
    /// production profile and section-stack readers remain semantic adapters; this
    /// method owns the root, units, disposition, and normalized declaration list.
    /// </summary>
    private static FirmamentV2ParseResult ParseCanonicalAdvancedDocument(string source, List<string> diagnostics, IReadOnlyList<ConceptIrTemplateInstantiation>? templateInstantiations)
    {
        var model = CanonicalModelRegex.Match(source);
        var rootOpen = model.Success ? source.IndexOf('{', model.Index) : -1;
        var rootClose = rootOpen >= 0 ? FindMatchingBrace(source, rootOpen) : -1;
        if (!model.Success || rootClose < 0 || !string.IsNullOrWhiteSpace(source[(rootClose + 1)..]))
            return CanonicalFailure(diagnostics, CanonicalDocumentMalformed);

        var body = source[(rootOpen + 1)..rootClose];
        var units = Regex.Match(body, @"\bUnits\s*:\s*(?<value>[A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.CultureInvariant);
        if (!units.Success || units.Groups["value"].Value != "mm")
            return CanonicalFailure(diagnostics, CanonicalUnitsInvalid);
        if (Regex.IsMatch(body, @"\b(?!(?:InlineStep|Recognize|Replace)\b)(?:Inline[A-Za-z_][A-Za-z0-9_]*|Recogniz[A-Za-z_][A-Za-z0-9_]*|Replac[A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.CultureInvariant))
            return CanonicalFailure(diagnostics, CanonicalDeclarationUnrecognized);
        var adapterSource = Regex.Replace(body, @"(?m)^\s*Units\s*:\s*mm\s*$", string.Empty, RegexOptions.CultureInvariant);

        if (PrismaticProfileCompositionParser.IsCompositionSource(adapterSource))
        {
            var composition = PrismaticProfileCompositionParser.Parse(adapterSource);
            if (composition.Feature is null)
            {
                diagnostics.AddRange(composition.Diagnostics);
                return CanonicalFailure(diagnostics, CanonicalDocumentMalformed);
            }
            var feature = composition.Feature;
            var compositionSelectionDiagnostics = new List<string>();
            var compositionSelections = ParseCanonicalSelections(body, rootOpen + 1, compositionSelectionDiagnostics);
            ValidateCanonicalSelectionSources(body, compositionSelections, composition.Profiles.Keys, compositionSelectionDiagnostics);
            if (compositionSelectionDiagnostics.Count > 0)
            {
                diagnostics.AddRange(compositionSelectionDiagnostics);
                return CanonicalFailure(diagnostics, CanonicalDocumentMalformed);
            }
            var document = new FirmamentV2Document(
                model.Groups["name"].Value,
                "mm",
                [new FirmamentV2SolidBinding(feature.Name, "Compose", new FirmamentV2AdvancedMaterialRecord("Compose"))],
                Profiles: composition.Profiles.Values.Select(profile => new FirmamentV2ProfileDecl(profile.Name, profile.EffectiveConstructionPlane.StableId, profile.LocalStartDepth ?? 0d, profile.LocalEndDepth ?? 0d, new(0, 0))).ToArray(),
                Composes: [new FirmamentV2ComposeDecl(feature.Name, feature.Operations.Select(operation => operation.Name).ToArray(), new(0, body.Length))],
                Selections: compositionSelections);
            diagnostics.Add("firmament-v2-unified-canonical-advanced-parsed");
            diagnostics.Add("firmament-v2-profile-compose-adapted");
            diagnostics.Add("firmament-v2-parse-succeeded");
            diagnostics.Sort(StringComparer.Ordinal);
            return FirmamentV2ParseResult.Success(document, diagnostics.Distinct(StringComparer.Ordinal).ToArray());
        }

        if (ProfileAuthoringParser.IsProfileSource(adapterSource))
        {
            var profile = ProfileAuthoringParser.Parse(adapterSource);
            if (profile.Profile is null)
            {
                diagnostics.AddRange(profile.Diagnostics);
                return CanonicalFailure(diagnostics, CanonicalDocumentMalformed);
            }
            var resolved = profile.Profile;
            var profileSelectionDiagnostics = new List<string>();
            var profileSelections = ParseCanonicalSelections(body, rootOpen + 1, profileSelectionDiagnostics);
            ValidateCanonicalSelectionSources(body, profileSelections, [resolved.Name], profileSelectionDiagnostics);
            if (profileSelectionDiagnostics.Count > 0)
            {
                diagnostics.AddRange(profileSelectionDiagnostics);
                return CanonicalFailure(diagnostics, CanonicalDocumentMalformed);
            }
            var document = new FirmamentV2Document(
                model.Groups["name"].Value,
                "mm",
                [new FirmamentV2SolidBinding(resolved.Name, "Profile", new FirmamentV2AdvancedMaterialRecord("Profile"))],
                Profiles: [new FirmamentV2ProfileDecl(resolved.Name, resolved.EffectiveConstructionPlane.StableId, resolved.LocalStartDepth ?? 0d, resolved.LocalEndDepth ?? profile.Height, new(0, body.Length))],
                Selections: profileSelections);
            diagnostics.Add("firmament-v2-unified-canonical-advanced-parsed");
            diagnostics.Add("firmament-v2-profile-adapted");
            diagnostics.Add("firmament-v2-parse-succeeded");
            diagnostics.Sort(StringComparer.Ordinal);
            return FirmamentV2ParseResult.Success(document, diagnostics.Distinct(StringComparer.Ordinal).ToArray());
        }

        var concept = ParseConceptModelingDocument(adapterSource, diagnostics, templateInstantiations);
        if (!concept.IsSuccess || concept.Document is null) return concept;
        var conceptSelectionDiagnostics = new List<string>();
        var conceptSelections = ParseCanonicalSelections(body, rootOpen + 1, conceptSelectionDiagnostics);
        ValidateCanonicalSelectionSources(body, conceptSelections, [], conceptSelectionDiagnostics);
        if (conceptSelectionDiagnostics.Count > 0)
        {
            diagnostics.AddRange(conceptSelectionDiagnostics);
            return CanonicalFailure(diagnostics, CanonicalDocumentMalformed);
        }
        var normalized = concept.Document with
        {
            ModelName = model.Groups["name"].Value,
            Selections = conceptSelections
        };
        diagnostics.Add("firmament-v2-unified-canonical-advanced-parsed");
        diagnostics.Sort(StringComparer.Ordinal);
        return FirmamentV2ParseResult.Success(normalized, diagnostics.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static IReadOnlyList<FirmamentV2SelectionDecl> ParseCanonicalSelections(string body, int offset, List<string> diagnostics)
    {
        var selections = new List<FirmamentV2SelectionDecl>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match header in Regex.Matches(body, @"\bSelection\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant))
        {
            var name = header.Groups["name"].Value;
            var open = body.IndexOf('{', header.Index);
            var close = FindMatchingBrace(body, open);
            if (close < 0)
            {
                diagnostics.Add($"{SelectionMalformed}:{name}");
                continue;
            }

            if (!names.Add(name))
            {
                diagnostics.Add($"{SelectionDuplicateName}:{name}");
                continue;
            }

            var content = body[(open + 1)..close];
            string Field(string field) => Regex.Match(content, $@"\b{Regex.Escape(field)}\s*:\s*(?<value>[\s\S]*?)(?=\s+(?:Target|Source|Require)\s*:|\z)", RegexOptions.CultureInvariant).Groups["value"].Value.Trim();
            var target = Field("Target");
            var source = Field("Source");
            var requirement = Field("Require");
            if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(requirement))
            {
                diagnostics.Add($"{SelectionMalformed}:{name}");
                continue;
            }

            selections.Add(new(name, target, source, requirement, new(offset + header.Index, close - header.Index + 1)));
        }

        return selections;
    }

    private static void ValidateCanonicalSelectionSources(
        string body,
        IReadOnlyList<FirmamentV2SelectionDecl> selections,
        IEnumerable<string> profileNames,
        List<string> diagnostics)
    {
        var profiles = profileNames.ToHashSet(StringComparer.Ordinal);
        var slots = Regex.Matches(body, @"\bSlot\s*<[^>]+>\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant)
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);
        var holes = Regex.Matches(body, @"\bHole\s*<[^>]+>\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant)
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);
        var allowedTargets = new HashSet<string>(StringComparer.Ordinal)
        {
            "TopBoundary", "BottomBoundary", "SideBoundary", "HoleEntry", "HoleExit", "HoleWall", "ShaftToDrillPoint", "DrillPoint", "Tip",
            "SlotEntry", "SlotExit", "SlotWall", "SlotStraightWall", "SlotEndWall"
        };

        foreach (var selection in selections)
        {
            if (!allowedTargets.Contains(selection.Target)) diagnostics.Add($"{SelectionResultKindInvalid}:{selection.Name}:{selection.Target}");
            var profile = Regex.Match(selection.Source, @"^(?<profile>[A-Za-z_][A-Za-z0-9_]*)\.Profile(?:Segments|Loop)\s*\(", RegexOptions.CultureInvariant);
            var slot = Regex.Match(selection.Source, @"^Slot\s*\(\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\)$", RegexOptions.CultureInvariant);
            var hole = Regex.Match(selection.Source, @"^Hole\s*\(\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\)$", RegexOptions.CultureInvariant);
            if (profile.Success && !profiles.Contains(profile.Groups["profile"].Value)) diagnostics.Add($"{SelectionUnknownSource}:{selection.Name}:{profile.Groups["profile"].Value}");
            else if (slot.Success && !slots.Contains(slot.Groups["name"].Value)) diagnostics.Add($"{SelectionUnknownSource}:{selection.Name}:{slot.Groups["name"].Value}");
            else if (hole.Success && !holes.Contains(hole.Groups["name"].Value)) diagnostics.Add($"{SelectionUnknownSource}:{selection.Name}:{hole.Groups["name"].Value}");
            else if (!profile.Success && !slot.Success && !hole.Success) diagnostics.Add($"{SelectionUnknownSource}:{selection.Name}");
        }
    }

    /// <summary>Extracts an already admitted canonical advanced body for the
    /// production materializer boundary.  It is intentionally not a second parser.</summary>
    public static bool TryGetCanonicalAdvancedBody(string source, out string body)
    {
        body = string.Empty;
        var model = CanonicalModelRegex.Match(source);
        if (!model.Success || !ContainsCanonicalAdvancedDeclaration(source)) return false;
        var open = source.IndexOf('{', model.Index); var close = FindMatchingBrace(source, open);
        if (open < 0 || close < 0 || !string.IsNullOrWhiteSpace(source[(close + 1)..])) return false;
        var candidate = source[(open + 1)..close];
        if (!Regex.IsMatch(candidate, @"\bUnits\s*:\s*mm\b", RegexOptions.CultureInvariant)) return false;
        body = Regex.Replace(candidate, @"(?m)^\s*Units\s*:\s*mm\s*$", string.Empty, RegexOptions.CultureInvariant);
        return true;
    }

    private static FirmamentV2ParseResult ParseCanonicalDocument(string source, List<string> diagnostics, string? sourceDirectory)
    {
        var model = CanonicalModelRegex.Match(source);
        var rootOpen = model.Success ? source.IndexOf('{', model.Index) : -1;
        var rootClose = rootOpen >= 0 ? FindMatchingBrace(source, rootOpen) : -1;
        if (!model.Success || rootClose < 0 || !string.IsNullOrWhiteSpace(source[(rootClose + 1)..]))
            return CanonicalFailure(diagnostics, CanonicalDocumentMalformed);

        var body = source[(rootOpen + 1)..rootClose];
        var units = Regex.Match(body, @"\bUnits\s*:\s*(?<value>[A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.CultureInvariant);
        if (!units.Success || units.Groups["value"].Value != "mm")
            return CanonicalFailure(diagnostics, CanonicalUnitsInvalid);
        if (Regex.IsMatch(body, @"\b(?!(?:InlineStep|Recognize|Replace)\b)(?:Inline[A-Za-z_][A-Za-z0-9_]*|Recogniz[A-Za-z_][A-Za-z0-9_]*|Recognis[A-Za-z_][A-Za-z0-9_]*|Replac[A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.CultureInvariant))
            return CanonicalFailure(diagnostics, CanonicalDeclarationUnrecognized);

        var solids = new List<FirmamentV2SolidBinding>();
        var byName = new Dictionary<string, FirmamentV2SolidBinding>(StringComparer.Ordinal);
        foreach (Match primitive in CanonicalPrimitiveHeaderRegex.Matches(body))
        {
            var open = body.IndexOf('{', primitive.Index);
            var close = FindMatchingBrace(body, open);
            if (close < 0) return CanonicalFailure(diagnostics, CanonicalPrimitiveMalformed);
            var name = primitive.Groups["name"].Value;
            if (!byName.TryAdd(name, null!)) return CanonicalFailure(diagnostics, DuplicateName);
            var primitiveBody = body[(open + 1)..close];
            var record = ParseCanonicalPrimitive(primitive.Groups["type"].Value, primitiveBody, diagnostics);
            if (record is null) return CanonicalFailure(diagnostics, CanonicalPrimitiveMalformed);
            var solid = new FirmamentV2SolidBinding(name, primitive.Groups["type"].Value, record);
            byName[name] = solid;
            solids.Add(solid);
        }
        foreach (Match inlineStep in CanonicalInlineStepHeaderRegex.Matches(body))
        {
            var open = body.IndexOf('{', inlineStep.Index);
            var close = FindMatchingBrace(body, open);
            if (close < 0) return CanonicalFailure(diagnostics, CanonicalPrimitiveMalformed);
            var name = inlineStep.Groups["name"].Value;
            if (!byName.TryAdd(name, null!)) return CanonicalFailure(diagnostics, DuplicateName);
            var binding = ParseInlineStep(name, body[(open + 1)..close], diagnostics, sourceDirectory);
            if (binding is null) return CanonicalFailure(diagnostics, InlineStepPathMissing);
            byName[name] = binding;
            solids.Add(binding);
        }
        if (solids.Count == 0) return CanonicalFailure(diagnostics, MissingSolid);

        var modifies = new List<FirmamentV2ModifyBlock>();
        foreach (Match modify in CanonicalModifyHeaderRegex.Matches(body))
        {
            var target = modify.Groups["target"].Value;
            if (!byName.TryGetValue(target, out var solid)) return CanonicalFailure(diagnostics, ModifyTargetUnresolved);
            if (solid.Box is null) return CanonicalFailure(diagnostics, ModifyTargetNotSolid);
            var open = body.IndexOf('{', modify.Index);
            var close = FindMatchingBrace(body, open);
            if (close < 0) return CanonicalFailure(diagnostics, CanonicalModifyMalformed);
            var modifyBody = body[(open + 1)..close];
            var holes = ParseCanonicalHoles(modifyBody, solid, rootOpen + 1 + open + 1, diagnostics);
            var finishes = ParseCanonicalEdgeFinishes(modifyBody, rootOpen + 1 + open + 1, diagnostics);
            if (diagnostics.Any(IsFatalDiagnosticCode) || diagnostics.Contains(CanonicalPoint2Invalid) || diagnostics.Contains(CanonicalModifyMalformed) || diagnostics.Contains(CanonicalFieldUnknown))
                return CanonicalFailure(diagnostics, CanonicalModifyMalformed);
            // Empty Modify blocks are valid; feature admission belongs to the selected
            // production route, not to a phase parser's minimum-count rule.
            modifies.Add(new FirmamentV2ModifyBlock(target, [], holes, finishes));
        }

        var recognizedRegions = ParseCanonicalRecognizedRegions(body, byName, diagnostics);
        var replacements = ParseCanonicalReplacements(body, byName, recognizedRegions, diagnostics);
        if (diagnostics.Any(IsFatalDiagnosticCode) || diagnostics.Contains(CanonicalDeclarationUnrecognized) || diagnostics.Contains(CanonicalFieldUnknown))
            return CanonicalFailure(diagnostics, CanonicalDeclarationUnrecognized);

        diagnostics.Add("firmament-v2-unified-canonical-parsed");
        diagnostics.Add("firmament-v2-parse-succeeded");
        diagnostics.Sort(StringComparer.Ordinal);
        return FirmamentV2ParseResult.Success(new FirmamentV2Document(model.Groups["name"].Value, "mm", solids, modifies, RecognizedRegions: recognizedRegions, Replacements: replacements), diagnostics.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static FirmamentV2PrimitiveRecord? ParseCanonicalPrimitive(string type, string body, List<string> diagnostics)
    {
        double Number(string field)
        {
            var match = Regex.Match(body, $@"\b{field}\s*:\s*(?<value>[-+0-9.eE]+)mm\b", RegexOptions.CultureInvariant);
            return match.Success && double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) && value > 0d ? value : double.NaN;
        }
        IReadOnlyList<double>? Size()
        {
            var match = Regex.Match(body, @"\bSize\s*:\s*\[(?<values>[^\]]+)\]", RegexOptions.CultureInvariant);
            if (!match.Success) return null;
            var values = match.Groups["values"].Value.Split(',', StringSplitOptions.TrimEntries);
            var parsed = values.Select(ParsePhase3Length).ToArray();
            return parsed.Length == 3 && parsed.All(v => double.IsFinite(v) && v > 0d) ? parsed : null;
        }
        return type switch
        {
            "Box" when Size() is { } size => new FirmamentV2BoxRecord(size, []),
            "Cylinder" when Number("Radius") is var radius && Number("Height") is var height && double.IsFinite(radius) && double.IsFinite(height) => new FirmamentV2CylinderRecord(radius, height),
            "RoundedBox" when Size() is { } roundedSize && Number("CornerRadius") is var corner && double.IsFinite(corner) && corner < double.Min(roundedSize[0], roundedSize[1]) / 2d => new FirmamentV2RoundedBoxRecord(roundedSize, corner),
            "Frustum" when Number("BottomRadius") is var bottom && Number("TopRadius") is var top && Number("Height") is var frustumHeight && double.IsFinite(bottom) && double.IsFinite(top) && double.IsFinite(frustumHeight) && double.Abs(bottom - top) > 1e-12d => new FirmamentV2FrustumRecord(bottom, top, frustumHeight),
            _ => null
        };
    }

    private static IReadOnlyList<FirmamentV2SemanticHoleDecl> ParseCanonicalHoles(string body, FirmamentV2SolidBinding solid, int bodyOffset, List<string> diagnostics)
    {
        var holes = new List<FirmamentV2SemanticHoleDecl>();
        var headers = CanonicalHoleHeaderRegex.Matches(body);
        if (Regex.Matches(body, @"\bHole\s*<", RegexOptions.CultureInvariant).Count != headers.Count)
        {
            diagnostics.Add(CanonicalModifyMalformed);
            return holes;
        }
        foreach (Match hole in headers)
        {
            var open = body.IndexOf('{', hole.Index); var close = FindMatchingBrace(body, open);
            if (close < 0) { diagnostics.Add(CanonicalModifyMalformed); continue; }
            var holeBody = body[(open + 1)..close];
            if (!Enum.TryParse<FirmamentV2SemanticHoleVariant>(hole.Groups["variant"].Value, ignoreCase: false, out var variant))
            {
                diagnostics.Add(HoleVariantUnknown);
                continue;
            }

            var knownFields = variant switch
            {
                FirmamentV2SemanticHoleVariant.Counterbore => new[] { "On", "Center", "Diameter", "CounterboreDiameter", "CounterboreDepth", "End" },
                FirmamentV2SemanticHoleVariant.Countersink => new[] { "On", "Center", "Diameter", "CountersinkDiameter", "CountersinkAngle", "End" },
                _ => new[] { "On", "Center", "Diameter", "End" }
            };
            if (CanonicalBodyHasUnknownField(holeBody, knownFields))
            {
                diagnostics.Add(CanonicalFieldUnknown);
                continue;
            }
            var on = Regex.Match(holeBody, @"\bOn\s*:\s*(?<face>[+-][XYZ])\b", RegexOptions.CultureInvariant);
            var point = Regex.Match(holeBody, @"\bCenter\s*:\s*Point2\(\s*(?<u>[-+0-9.eE]+)mm\s*,\s*(?<v>[-+0-9.eE]+)mm\s*\)", RegexOptions.CultureInvariant);
            var diameter = Regex.Match(holeBody, @"\bDiameter\s*:\s*(?<value>[-+0-9.eE]+)mm\b", RegexOptions.CultureInvariant);
            var end = Regex.Match(holeBody, @"\bEnd\s*:\s*(?<kind>ThroughAll|Blind)\b(?:\s+(?<depth>[-+0-9.eE]+)mm)?", RegexOptions.CultureInvariant);
            if (!on.Success || !diameter.Success || !end.Success) { diagnostics.Add(CanonicalModifyMalformed); continue; }
            if (!point.Success || !double.TryParse(point.Groups["u"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var u) || !double.TryParse(point.Groups["v"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || !double.IsFinite(u) || !double.IsFinite(v)) { diagnostics.Add(CanonicalPoint2Invalid); continue; }
            if (!double.TryParse(diameter.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var shaft) || !double.IsFinite(shaft) || shaft <= 0d) { diagnostics.Add(HoleDiameterInvalid); continue; }
            FirmamentV2SemanticHoleEnd endValue;
            if (end.Groups["kind"].Value == "ThroughAll") endValue = new(FirmamentV2SemanticHoleEndKind.ThroughAll);
            else if (double.TryParse(end.Groups["depth"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var depth) && depth > 0d && double.IsFinite(depth)) endValue = new(FirmamentV2SemanticHoleEndKind.Depth, depth);
            else { diagnostics.Add(HoleBlindDepthInvalid); continue; }
            double? counterboreDiameter = null, counterboreDepth = null, countersinkDiameter = null, countersinkAngle = null;
            if (variant == FirmamentV2SemanticHoleVariant.Counterbore)
            {
                var cbDiameter = Regex.Match(holeBody, @"\bCounterboreDiameter\s*:\s*(?<value>[-+0-9.eE]+)mm\b", RegexOptions.CultureInvariant);
                var cbDepth = Regex.Match(holeBody, @"\bCounterboreDepth\s*:\s*(?<value>[-+0-9.eE]+)mm\b", RegexOptions.CultureInvariant);
                if (!cbDiameter.Success || !cbDepth.Success ||
                    !double.TryParse(cbDiameter.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var diameterValue) ||
                    !double.TryParse(cbDepth.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var depthValue) ||
                    !double.IsFinite(diameterValue) || !double.IsFinite(depthValue) || diameterValue <= shaft || depthValue <= 0d)
                {
                    diagnostics.Add(HoleCounterboreInvalid);
                    continue;
                }
                counterboreDiameter = diameterValue;
                counterboreDepth = depthValue;
            }
            if (variant == FirmamentV2SemanticHoleVariant.Countersink)
            {
                var csDiameter = Regex.Match(holeBody, @"\bCountersinkDiameter\s*:\s*(?<value>[-+0-9.eE]+)mm\b", RegexOptions.CultureInvariant);
                var csAngle = Regex.Match(holeBody, @"\bCountersinkAngle\s*:\s*(?<value>[-+0-9.eE]+)deg\b", RegexOptions.CultureInvariant);
                if (!csDiameter.Success || !csAngle.Success ||
                    !double.TryParse(csDiameter.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var diameterValue) ||
                    !double.TryParse(csAngle.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var angleValue) ||
                    !double.IsFinite(diameterValue) || !double.IsFinite(angleValue) || diameterValue <= shaft || angleValue <= 0d || angleValue >= 180d)
                {
                    diagnostics.Add(HoleCountersinkInvalid);
                    continue;
                }
                countersinkDiameter = diameterValue;
                countersinkAngle = angleValue;
            }
            var face = FirmamentV2FaceTarget.Direct(on.Groups["face"].Value);
            holes.Add(new(hole.Groups["name"].Value, variant, face, new FirmamentV2FaceLocalPoint2D(u, v, FirmamentV2FaceLocalPoint2D.ConventionFor(face.Axis)), shaft, endValue,
                counterboreDiameter, counterboreDepth, countersinkDiameter, countersinkAngle,
                SourceSpan: new FirmamentV2SourceSpan(bodyOffset + hole.Index, close - hole.Index + 1)));
        }
        return holes;
    }

    private static bool CanonicalBodyHasUnknownField(string body, IReadOnlyCollection<string> knownFields) =>
        Regex.Matches(body, @"(?m)^\s*(?<field>[A-Za-z_][A-Za-z0-9_]*)\s*:", RegexOptions.CultureInvariant)
            .Cast<Match>()
            .Any(match => !knownFields.Contains(match.Groups["field"].Value, StringComparer.Ordinal));

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

    private static IReadOnlyList<FirmamentV2EdgeFinishDecl> ParseCanonicalEdgeFinishes(string body, int bodyOffset, List<string> diagnostics)
    {
        var finishes = new List<FirmamentV2EdgeFinishDecl>();
        foreach (Match edge in CanonicalEdgeHeaderRegex.Matches(body))
        {
            var open = body.IndexOf('{', edge.Index); var close = FindMatchingBrace(body, open);
            if (close < 0) { diagnostics.Add(CanonicalModifyMalformed); continue; }
            var edgeBody = body[(open + 1)..close];
            var face = Regex.Match(edgeBody, @"\bFace\s*:\s*(?<value>[+-][XYZ])\b", RegexOptions.CultureInvariant);
            var target = Regex.Match(edgeBody, @"\bTarget\s*:\s*(?<value>[A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.CultureInvariant);
            var kind = Regex.Match(edgeBody, @"\bKind\s*:\s*(?<value>[A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.CultureInvariant);
            var distance = Regex.Match(edgeBody, @"\b(?:Distance|Radius)\s*:\s*(?<value>[-+0-9.eE]+)mm\b", RegexOptions.CultureInvariant);
            if (!face.Success || !target.Success || !kind.Success || !distance.Success || !double.TryParse(distance.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value) || value <= 0d) { diagnostics.Add(CanonicalModifyMalformed); continue; }
            finishes.Add(new FirmamentV2EdgeFinishDecl(edge.Groups["name"].Value, face.Groups["value"].Value, target.Groups["value"].Value, kind.Groups["value"].Value, value, new FirmamentV2SourceSpan(bodyOffset + edge.Index, close - edge.Index + 1)));
        }
        return finishes;
    }

    private static FirmamentV2ParseResult CanonicalFailure(List<string> diagnostics, string diagnostic)
    {
        diagnostics.Add(diagnostic);
        diagnostics.Add("firmament-v2-parse-failed");
        return FirmamentV2ParseResult.Failure(diagnostics.Distinct(StringComparer.Ordinal).Order().ToArray());
    }

    private static FirmamentV2ParseResult ParsePhase3ModelingDocument(string source, List<string> diagnostics)
    {
        var model = Regex.Match(source, @"^\s*Model\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s+(?<units>[A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.CultureInvariant);
        var primitive = Regex.Match(source, @"\b(?<type>Box|Cylinder)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
        var modify = Regex.Match(source, @"\bModify\s+(?<target>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
        var edges = Regex.Matches(source, @"\bEdgeFinish\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant).Cast<Match>().ToArray();
        var edge = edges.FirstOrDefault() ?? Match.Empty;
        if (!model.Success || !primitive.Success || !modify.Success || !edge.Success || model.Groups["units"].Value != "mm"
            || Regex.Matches(source, @"\b(?:Box|Cylinder)\s+[A-Za-z_]", RegexOptions.CultureInvariant).Count != 1
            || Regex.Matches(source, @"\bModify\s+[A-Za-z_]", RegexOptions.CultureInvariant).Count != 1
            || edges.Length is < 1 or > 3)
            diagnostics.Add(Phase3EdgeFinishSyntaxInvalid);

        if (diagnostics.Contains(Phase3EdgeFinishSyntaxInvalid))
            return FirmamentV2ParseResult.Failure(diagnostics.Append("firmament-v2-parse-failed").Order().ToArray());

        var primitiveOpen = source.IndexOf('{', primitive.Index);
        var primitiveClose = FindMatchingBrace(source, primitiveOpen);
        var modifyOpen = source.IndexOf('{', modify.Index);
        var modifyClose = FindMatchingBrace(source, modifyOpen);
        var edgeOpen = source.IndexOf('{', edge.Index);
        var edgeClose = FindMatchingBrace(source, edgeOpen);
        if (primitiveClose < 0 || modifyClose < 0 || edgeClose < 0 || edge.Index < modifyOpen || edgeClose > modifyClose)
        {
            diagnostics.Add(Phase3EdgeFinishSyntaxInvalid);
            return FirmamentV2ParseResult.Failure(diagnostics.Append("firmament-v2-parse-failed").Order().ToArray());
        }

        var primitiveBody = source[(primitiveOpen + 1)..primitiveClose];
        var sizeMatch = Regex.Match(primitiveBody, @"\bSize\s*:\s*\[(?<values>[^\]]+)\]", RegexOptions.CultureInvariant);
        var values = sizeMatch.Success ? sizeMatch.Groups["values"].Value.Split(',').Select(ParsePhase3Length).ToArray() : [];
        var radiusMatch = Regex.Match(primitiveBody, @"\bRadius\s*:\s*(?<value>[-+0-9.eE]+mm)\b", RegexOptions.CultureInvariant);
        var heightMatch = Regex.Match(primitiveBody, @"\bHeight\s*:\s*(?<value>[-+0-9.eE]+mm)\b", RegexOptions.CultureInvariant);
        var radiusValue = radiusMatch.Success ? ParsePhase3Length(radiusMatch.Groups["value"].Value) : double.NaN;
        var heightValue = heightMatch.Success ? ParsePhase3Length(heightMatch.Groups["value"].Value) : double.NaN;
        var edgeBody = source[(edgeOpen + 1)..edgeClose];
        var face = Regex.Match(edgeBody, @"\bFace\s*:\s*(?<value>[+-][XYZ])", RegexOptions.CultureInvariant);
        var target = Regex.Match(edgeBody, @"\bTarget\s*:\s*(?<value>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant);
        var kind = Regex.Match(edgeBody, @"\bKind\s*:\s*(?<value>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant);
        var distance = Regex.Match(edgeBody, @"\bDistance\s*:\s*(?<value>[-+0-9.eE]+)mm\b", RegexOptions.CultureInvariant);
        var primitiveValid = primitive.Groups["type"].Value == "Box"
            ? values.Length == 3 && values.All(v => double.IsFinite(v) && v > 0d)
            : double.IsFinite(radiusValue) && radiusValue > 0d && double.IsFinite(heightValue) && heightValue > 0d;
        if (!primitiveValid || !face.Success || !target.Success || !kind.Success || !distance.Success
            || !double.TryParse(distance.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var distanceValue) || !double.IsFinite(distanceValue)
            || primitive.Groups["name"].Value != modify.Groups["target"].Value)
        {
            diagnostics.Add(Phase3EdgeFinishSyntaxInvalid);
            return FirmamentV2ParseResult.Failure(diagnostics.Append("firmament-v2-parse-failed").Order().ToArray());
        }

        FirmamentV2PrimitiveRecord primitiveRecord = primitive.Groups["type"].Value == "Box"
            ? new FirmamentV2BoxRecord(values, [])
            : new FirmamentV2CylinderRecord(radiusValue, heightValue);
        var solid = new FirmamentV2SolidBinding(primitive.Groups["name"].Value, primitive.Groups["type"].Value, primitiveRecord);
        var finishes = new List<FirmamentV2EdgeFinishDecl> { new(edge.Groups["name"].Value, face.Groups["value"].Value, target.Groups["value"].Value, kind.Groups["value"].Value, distanceValue, new FirmamentV2SourceSpan(edge.Index, edgeClose - edge.Index + 1)) };
        foreach (var additionalEdge in edges.Skip(1))
        {
            var additionalOpen = source.IndexOf('{', additionalEdge.Index);
            var additionalClose = FindMatchingBrace(source, additionalOpen);
            var additionalBody = additionalClose < 0 ? string.Empty : source[(additionalOpen + 1)..additionalClose];
            var additionalFace = Regex.Match(additionalBody, @"\bFace\s*:\s*(?<value>[+-][XYZ])", RegexOptions.CultureInvariant);
            var additionalTarget = Regex.Match(additionalBody, @"\bTarget\s*:\s*(?<value>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant);
            var additionalKind = Regex.Match(additionalBody, @"\bKind\s*:\s*(?<value>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant);
            var additionalDistance = Regex.Match(additionalBody, @"\bDistance\s*:\s*(?<value>[-+0-9.eE]+)mm\b", RegexOptions.CultureInvariant);
            if (additionalClose < 0 || !additionalFace.Success || !additionalTarget.Success || !additionalKind.Success
                || !additionalDistance.Success || !double.TryParse(additionalDistance.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var additionalDistanceValue) || !double.IsFinite(additionalDistanceValue))
            {
                diagnostics.Add(Phase3EdgeFinishSyntaxInvalid);
                return FirmamentV2ParseResult.Failure(diagnostics.Append("firmament-v2-parse-failed").Order().ToArray());
            }
            finishes.Add(new(additionalEdge.Groups["name"].Value, additionalFace.Groups["value"].Value, additionalTarget.Groups["value"].Value, additionalKind.Groups["value"].Value, additionalDistanceValue, new FirmamentV2SourceSpan(additionalEdge.Index, additionalClose - additionalEdge.Index + 1)));
        }
        var document = new FirmamentV2Document(model.Groups["name"].Value, "mm", [solid], [new FirmamentV2ModifyBlock(solid.Name, [], [], finishes)]);
        diagnostics.Add("firmament-v2-phase3-edge-finish-parsed");
        diagnostics.Add("firmament-v2-parse-succeeded");
        diagnostics.Sort(StringComparer.Ordinal);
        return FirmamentV2ParseResult.Success(document, diagnostics);
    }

    /// <summary>Bounded direct syntax for AIR-ROUNDED-BOX-M6.  It intentionally
    /// accepts only one history-known rounded box and its single complete +Z boundary finish.</summary>
    private static FirmamentV2ParseResult ParseRoundedBoxModelingDocument(string source, List<string> diagnostics)
    {
        var model = Regex.Match(source, @"^\s*(?:Model|Struct)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)(?:\s+(?<units>[A-Za-z_][A-Za-z0-9_]*))?\s*\{?", RegexOptions.CultureInvariant);
        var primitive = Regex.Match(source, @"\bRoundedBox\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
        if (!model.Success || !primitive.Success || Regex.Matches(source, @"\bRoundedBox\s+[A-Za-z_]", RegexOptions.CultureInvariant).Count != 1)
            return RoundedBoxParseFailure(diagnostics);
        if (model.Groups["units"].Success && model.Groups["units"].Value != "mm") return RoundedBoxParseFailure(diagnostics);
        var open = source.IndexOf('{', primitive.Index); var close = FindMatchingBrace(source, open);
        if (close < 0) return RoundedBoxParseFailure(diagnostics);
        var body = source[(open + 1)..close];
        var size = Regex.Match(body, @"\bSize\s*:\s*\[(?<values>[^\]]+)\]", RegexOptions.CultureInvariant);
        var corner = Regex.Match(body, @"\bCornerRadius\s*:\s*(?<value>[-+0-9.eE]+)mm\b", RegexOptions.CultureInvariant);
        var values = size.Success ? size.Groups["values"].Value.Split(',').Select(ParsePhase3Length).ToArray() : [];
        if (values.Length != 3 || values.Any(v => !double.IsFinite(v) || v <= 0d) || !corner.Success || !double.TryParse(corner.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var rc) || !double.IsFinite(rc) || rc <= 0d || rc >= double.Min(values[0], values[1]) / 2d)
            return RoundedBoxParseFailure(diagnostics);

        var solid = new FirmamentV2SolidBinding(primitive.Groups["name"].Value, "RoundedBox", new FirmamentV2RoundedBoxRecord(values, rc));
        var modifies = Regex.Matches(source, @"\bModify\s+(?<target>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant).Cast<Match>().ToArray();
        if (modifies.Length > 1 || (modifies.Length == 1 && modifies[0].Groups["target"].Value != solid.Name)) return RoundedBoxParseFailure(diagnostics);
        var blocks = new List<FirmamentV2ModifyBlock>();
        if (modifies.Length == 1)
        {
            var modify = modifies[0]; var modifyOpen = source.IndexOf('{', modify.Index); var modifyClose = FindMatchingBrace(source, modifyOpen);
            var finishes = Regex.Matches(source, @"\bEdgeFinish\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant).Cast<Match>().ToArray();
            if (modifyClose < 0 || finishes.Length != 1) return RoundedBoxParseFailure(diagnostics);
            var edge = finishes[0]; var edgeOpen = source.IndexOf('{', edge.Index); var edgeClose = FindMatchingBrace(source, edgeOpen);
            if (edgeClose < 0 || edge.Index < modifyOpen || edgeClose > modifyClose) return RoundedBoxParseFailure(diagnostics);
            var edgeBody = source[(edgeOpen + 1)..edgeClose];
            var face = Regex.Match(edgeBody, @"\bFace\s*:\s*(?<value>[+-][XYZ])", RegexOptions.CultureInvariant);
            var target = Regex.Match(edgeBody, @"\bTarget\s*:\s*(?<value>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant);
            var kind = Regex.Match(edgeBody, @"\bKind\s*:\s*(?<value>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant);
            var radius = Regex.Match(edgeBody, @"\bRadius\s*:\s*(?<value>[-+0-9.eE]+)mm\b", RegexOptions.CultureInvariant);
            if (!face.Success || !target.Success || !kind.Success || !radius.Success || !double.TryParse(radius.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var rf) || !double.IsFinite(rf)) return RoundedBoxParseFailure(diagnostics);
            blocks.Add(new FirmamentV2ModifyBlock(solid.Name, [], [], [new FirmamentV2EdgeFinishDecl(edge.Groups["name"].Value, face.Groups["value"].Value, target.Groups["value"].Value, kind.Groups["value"].Value, rf, new FirmamentV2SourceSpan(edge.Index, edgeClose - edge.Index + 1))]));
        }
        diagnostics.Add("firmament-v2-rounded-box-parsed"); diagnostics.Add("firmament-v2-parse-succeeded"); diagnostics.Sort(StringComparer.Ordinal);
        return FirmamentV2ParseResult.Success(new FirmamentV2Document(model.Groups["name"].Value, "mm", [solid], blocks), diagnostics.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static FirmamentV2ParseResult RoundedBoxParseFailure(List<string> diagnostics)
    {
        diagnostics.Add(Phase3EdgeFinishSyntaxInvalid); diagnostics.Add("firmament-v2-parse-failed");
        return FirmamentV2ParseResult.Failure(diagnostics.Distinct(StringComparer.Ordinal).Order().ToArray());
    }

    /// <summary>
    /// Bounded generic primitive construction syntax.  Angle brackets are not a general type-argument escape hatch:
    /// this parser accepts only the named construction policies and witnesses admitted by the production compiler.
    /// </summary>
    private static FirmamentV2ParseResult ParsePrimitiveConstructionPolicyDocument(string source, List<string> diagnostics)
    {
        var model = Regex.Match(source, @"^\s*(?:Struct|Model)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant);
        var primitive = Regex.Match(source, @"\b(?<type>[A-Za-z_][A-Za-z0-9_]*)\s*<\s*(?<policy>[A-Za-z_][A-Za-z0-9_]*)\s*>\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
        if (!model.Success || !primitive.Success || Regex.Matches(source, @"\b(?:RoundedBox|Frustum|Box)\s*<\s*[A-Za-z_][A-Za-z0-9_]*\s*>\s+[A-Za-z_]", RegexOptions.CultureInvariant).Count != 1)
            return ConstructionPolicyParseFailure(diagnostics);

        var type = primitive.Groups["type"].Value;
        var policy = primitive.Groups["policy"].Value;
        if (policy != "Hollow") { diagnostics.Add(UnknownConstructionPolicy); return ConstructionPolicyParseFailure(diagnostics); }
        if (type is not ("RoundedBox" or "Frustum")) { diagnostics.Add(PrimitiveDoesNotSatisfyHollowConstructible); return ConstructionPolicyParseFailure(diagnostics); }
        var open = source.IndexOf('{', primitive.Index); var close = FindMatchingBrace(source, open);
        if (close < 0) return ConstructionPolicyParseFailure(diagnostics);
        var body = source[(open + 1)..close];
        var thickness = Regex.Match(body, @"\bWallThickness\s*:\s*(?<value>[-+0-9.eE]+)mm\b", RegexOptions.CultureInvariant);
        if (!thickness.Success) { diagnostics.Add(WallThicknessRequired); return ConstructionPolicyParseFailure(diagnostics); }
        if (!double.TryParse(thickness.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var wall) || !double.IsFinite(wall) || wall <= 0d)
        { diagnostics.Add(WallThicknessMustBePositive); return ConstructionPolicyParseFailure(diagnostics); }
        var openings = Regex.Match(body, @"\bOpenings\s*:\s*\[(?<items>[^\]]*)\]", RegexOptions.CultureInvariant);
        var openingItems = openings.Success ? openings.Groups["items"].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) : [];
        if (openingItems.Length != 1 || openingItems[0] != "Top")
        {
            diagnostics.Add(openingItems.Length > 1 ? MultipleOpeningsNotSupported : UnsupportedOpening);
            return ConstructionPolicyParseFailure(diagnostics);
        }

        FirmamentV2PrimitiveRecord? record = null;
        if (type == "RoundedBox")
        {
            var size = Regex.Match(body, @"\bSize\s*:\s*\[(?<values>[^\]]+)\]", RegexOptions.CultureInvariant);
            var corner = Regex.Match(body, @"\bCornerRadius\s*:\s*(?<value>[-+0-9.eE]+)mm\b", RegexOptions.CultureInvariant);
            var values = size.Success ? size.Groups["values"].Value.Split(',').Select(ParsePhase3Length).ToArray() : [];
            if (values.Length != 3 || values.Any(v => !double.IsFinite(v) || v <= 0d) || !corner.Success || !double.TryParse(corner.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var radius) || !double.IsFinite(radius) || radius <= 0d)
                return ConstructionPolicyParseFailure(diagnostics);
            record = new FirmamentV2RoundedBoxRecord(values, radius);
        }
        else
        {
            double Read(string field)
            {
                var m = Regex.Match(body, $@"\b{field}\s*:\s*(?<value>[-+0-9.eE]+)mm\b", RegexOptions.CultureInvariant);
                return m.Success && double.TryParse(m.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : double.NaN;
            }
            var bottom = Read("BottomRadius"); var top = Read("TopRadius"); var height = Read("Height");
            if (!double.IsFinite(bottom) || !double.IsFinite(top) || !double.IsFinite(height) || bottom <= 0d || top <= 0d || height <= 0d || double.Abs(top - bottom) <= 1e-12d)
                return ConstructionPolicyParseFailure(diagnostics);
            record = new FirmamentV2FrustumRecord(bottom, top, height);
        }

        var hollow = new FirmamentV2HollowIntent(wall, ["Top"], new FirmamentV2SourceSpan(primitive.Index, close - primitive.Index + 1));
        var solid = new FirmamentV2SolidBinding(primitive.Groups["name"].Value, type, record, ConstructionPolicy: FirmamentV2ConstructionPolicy.Hollow, Hollow: hollow);
        diagnostics.Add("firmament-v2-hollow-policy-parsed"); diagnostics.Add("firmament-v2-parse-succeeded"); diagnostics.Sort(StringComparer.Ordinal);
        return FirmamentV2ParseResult.Success(new FirmamentV2Document(model.Groups["name"].Value, "mm", [solid]), diagnostics.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static FirmamentV2ParseResult ConstructionPolicyParseFailure(List<string> diagnostics)
    {
        diagnostics.Add("firmament-v2-parse-failed");
        return FirmamentV2ParseResult.Failure(diagnostics.Distinct(StringComparer.Ordinal).Order().ToArray());
    }

    private static FirmamentV2ParseResult ParseConceptModelingDocument(string source, List<string> diagnostics, IReadOnlyList<ConceptIrTemplateInstantiation>? templateInstantiations = null)
    {
        var resolution = ConceptIrResolver.Resolve(source, diagnostics);
        if (resolution is null || diagnostics.Any(IsConceptFatalDiagnostic))
        {
            diagnostics.Add("firmament-v2-parse-failed");
            diagnostics.Sort(StringComparer.Ordinal);
            return FirmamentV2ParseResult.Failure(diagnostics.Distinct(StringComparer.Ordinal).ToArray());
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

    internal static bool IsConceptFatalDiagnostic(string code) =>
        code is HoleConstructionPlaneNotFound or HolePlacementMixed or HolePlacementDuplicate or HoleConstructionPlaneCenterMissing or HoleConstructionPlaneExtentUnsupported or HoleConstructionPlaneHostUnsupported or HoleBlindDepthMissing or HoleBlindDepthInvalid or HoleDrillPointAngleInvalid or HoleTerminationConflictsWithExtent
        ||
        code.StartsWith(ConceptIrResolver.MissingMember, StringComparison.Ordinal)
        || code.StartsWith(ConceptIrResolver.UnknownMember, StringComparison.Ordinal)
        || code.StartsWith(ConceptIrResolver.TypeMismatch, StringComparison.Ordinal)
        || code.StartsWith(ConceptIrResolver.InvalidSpatialDerivation, StringComparison.Ordinal)
        || code.StartsWith(ConceptIrResolver.IndexOutOfRange, StringComparison.Ordinal)
        || code.StartsWith(ConceptIrResolver.CircularDependency, StringComparison.Ordinal)
        || code.StartsWith(ConceptIrResolver.MaterializedPhaseReference, StringComparison.Ordinal)
        || code.StartsWith(ConceptIrResolver.DuplicateDeclaration, StringComparison.Ordinal)
        || code.StartsWith(ConceptIrResolver.DuplicateExposedMember, StringComparison.Ordinal)
        || code.StartsWith(ConceptIrResolver.InvalidMaterializedReference, StringComparison.Ordinal)
        || code.StartsWith(ConceptIrResolver.ExposedMemberUnrepresentable, StringComparison.Ordinal)
        || code.StartsWith(ConceptIrResolver.CircularExposureDependency, StringComparison.Ordinal)
        || code.StartsWith(ConceptIrResolver.PointNotOnPlacementPlane, StringComparison.Ordinal)
        || code.StartsWith(ConceptIrResolver.PointOutsidePlacementFace, StringComparison.Ordinal)
        || code.StartsWith(ConceptIrResolver.PointProjectionUnsupported, StringComparison.Ordinal)
        || code.StartsWith("firmament-static-", StringComparison.Ordinal);

    public static bool IsFatalDiagnosticCode(string code) => IsConceptFatalDiagnostic(code) || code is UnknownConstructionPolicy or PrimitiveDoesNotSatisfyHollowConstructible or WallThicknessRequired or WallThicknessMustBePositive or UnsupportedOpening or MultipleOpeningsNotSupported or Phase3EdgeFinishSyntaxInvalid or PrimitiveFieldMissing or PrimitiveFieldUnknown or PrimitiveFieldInvalid or MissingModel or MissingUnits or MissingSolid or UnsupportedConstruct or UnknownRecordType or BoxMissingSize or BoxSizeArity or DegenerateDimension or NameUnresolved or DuplicateName or WithRequiresRecord or WithRequiresBoxRecord or WithFieldNotFound or WithFieldTypeMismatch or WithForwardReference or WithDerivedRecordInvalid or ExposeBlockUnsupported or ExposeRequiresBoxRecord or ExposeAliasDuplicate or ExposeAliasInvalid or SelectorUnsupported or SelectorAxisInvalid or SelectorSubselectorUnsupported or FatArrowOutsideExpose or RawBackendIdReferenceForbidden or ModifyTargetUnresolved or ModifyTargetNotSolid or RegionUnsupported or RegionAttachmentSelectorUnsupported or CutUnsupported or CutToolUnsupported or CylinderRadiusMissing or CylinderRadiusInvalid or CylinderRadiusNotFinite or ThroughSelectorUnsupported or AliasUnresolved or AliasRefTypeUnsupported or SideHoleAliasMustResolveToFace or SideHoleAliasResolvesToUnsupportedFace or SideHoleOnlyPlusXMinusXSupported or SideHoleRouteUnsupported or SideHoleSameFaceUnsupported or SideHoleAxisNotYetSupported or SideHoleRadiusExceedsClearance or CylinderCenterInvalid or CylinderCenterArityInvalid or CylinderCenterNotFinite or SideHoleCenterExceedsClearance or HoleVariantUnknown or HoleEntryFaceMissing or HoleCenterMissing or HoleShaftMissing or HoleEndMissing or HoleDiameterInvalid or HoleDepthInvalid or HoleCounterboreInvalid or HoleCountersinkInvalid or PmiKindUnknown or PmiTargetMissing or PmiTargetUnresolved or PmiDiameterInvalid or PmiDuplicateName or InlineStepUnknownBody or InlineStepUnknownFace or PmiImportedTargetNotFace or PmiImportedTargetRequiresCanonicalStep or PmiInvalidImportedTarget or InlineStepPathMissing or InlineStepPathInvalid or InlineStepFileMissing or InlineStepRequiresCanonical or UnknownRecognitionBody or UnknownRecognitionFace or DuplicateRegion or UnknownRecognitionRegion or InvalidRecognitionKind or InvalidRecognitionConfidence or PmiRecognizedRegionKindMismatch or UnknownReplacementBody or UnknownReplacementRegion or ReplacementKindMismatch or ReplacementFaceUnresolved or ReplacementUnsupportedKind or ReplacementVerificationFailed or ReplacementRadiusInvalid or ReplacementEndUnsupported or LetDuplicateName or LetUnknownType or LetTypeMismatch or LetInvalidLiteral or LetUnitMismatch or LetLiteralOnly or LetRecordDuplicateName or LetRecordDuplicateField or LetReferenceUnknownRecord or LetReferenceUnknownField or LetReferenceNonRecord or LetReferenceRecordUsedAsValue or ExpressionUnknownSymbol or ExpressionUnknownRecord or ExpressionUnknownField or ExpressionRecordUsedAsValue or ExpressionScalarUsedAsRecord or ExpressionTypeMismatch or ExpressionInvalidOperator or ExpressionDivisionByZero or ExpressionCycle or ExpressionUnsupported or ToleranceInvalidType or ToleranceUnitMismatch or ToleranceInvalidLiteral or ToleranceNegativeBilateral or ToleranceMissingMinus or ToleranceMissingPlus or ToleranceUnsupported or RecognitionEvidenceRadiusInvalid or RecognitionEvidenceSurfaceFamilyUnknown or RecognitionEvidenceAxisInvalid or SemanticProposalKindMismatch or SemanticProposalRadiusInvalid or SemanticProposalTargetUnresolved or SemanticProposalEndUnsupported or ConceptUnknownFamily or ConceptUnknownConcept or ConceptMissingRequiredField or ConceptUnknownField or ConceptDuplicateField or ConceptFieldTypeMismatch or ConceptInvalidTarget or ConceptDescriptorUnavailable or PmiDuplicateBlock or PmiDuplicateRecord or PmiDuplicateDatum or PmiUnknownRecordKind or PmiMissingRequiredField or PmiUnknownField or PmiDuplicateField or PmiInvalidTarget or PmiUnknownDatum or PmiDimensionTypeMismatch or PmiDimensionMissingTolerance or PmiToleranceTypeMismatch or StandaloneFillRegionRequired or UnsupportedStandaloneFillPattern or InvalidCellCount or CellSizeMustBePositive or StrutRadiusMustBePositive or NodeRadiusMustBePositive or MaterialBoundsMismatch or StandaloneFillMultipleUnsupported;

    private static IReadOnlyList<FirmamentV2Exposure> ParseExposures(string body, List<string> diagnostics)
    {
        var match = ExposeRegex.Match(body);
        if (!match.Success)
        {
            if (body.Contains("=>", StringComparison.Ordinal)) diagnostics.Add(FatArrowOutsideExpose);
            return [];
        }

        var exposures = new List<FirmamentV2Exposure>();
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rawLine in match.Groups["body"].Value.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;
            var binding = ExposureLineRegex.Match(line);
            if (!binding.Success) { diagnostics.Add(SelectorUnsupported); continue; }
            var alias = binding.Groups["alias"].Value;
            if (!aliases.Add(alias)) { diagnostics.Add(ExposeAliasDuplicate); continue; }
            var selector = binding.Groups["selector"].Value.Trim();
            if (selector.StartsWith("edge(", StringComparison.Ordinal) || selector.StartsWith("vertex(", StringComparison.Ordinal)) { diagnostics.Add(SelectorUnsupported); continue; }
            var face = FaceSelectorRegex.Match(selector);
            if (!face.Success)
            {
                if (selector.StartsWith("face(", StringComparison.Ordinal)) diagnostics.Add(SelectorAxisInvalid);
                else diagnostics.Add(SelectorUnsupported);
                continue;
            }
            var axis = face.Groups["axis"].Value;
            var sub = face.Groups["sub"].Success ? face.Groups["sub"].Value[1..] : null;
            if (sub is not null && !string.Equals(sub, "outerLoop", StringComparison.Ordinal)) { diagnostics.Add(SelectorSubselectorUnsupported); continue; }
            exposures.Add(new(alias, "face", selector, sub is null ? "FaceRef" : "LoopRef", axis, sub));
        }
        return exposures;
    }

    private static IReadOnlyList<double>? ParseSizeField(string body, List<string> diagnostics, string missingDiagnostic)
    {
        var sizeMatch = SizeRegex.Match(body);
        if (!sizeMatch.Success) { diagnostics.Add(missingDiagnostic); return null; }
        return ParseSizeValues(sizeMatch.Groups["values"].Value, diagnostics);
    }

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

    private static Dictionary<string, double> ParseScalarFields(string body, List<string> diagnostics)
    {
        var fields = FieldRegex.Matches(body).Select(m => m.Groups["field"].Value).Where(f => !f.StartsWith('@')).ToHashSet(StringComparer.Ordinal);
        var values = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (Match match in ScalarFieldRegex.Matches(body))
        {
            var raw = match.Groups["value"].Value;
            if (!double.TryParse(raw.EndsWith("mm", StringComparison.Ordinal) ? raw[..^2] : raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
            {
                diagnostics.Add(PrimitiveFieldInvalid);
                continue;
            }
            values[match.Groups["field"].Value] = value;
        }
        foreach (var field in fields)
            if (!values.ContainsKey(field)) diagnostics.Add(PrimitiveFieldInvalid);
        return values;
    }

    private static object? RequireFields(Dictionary<string, double> values, List<string> diagnostics, params string[] required)
    {
        foreach (var requiredField in required)
            if (!values.ContainsKey(requiredField)) diagnostics.Add(PrimitiveFieldMissing);
        foreach (var field in values.Keys)
            if (!required.Contains(field, StringComparer.Ordinal)) diagnostics.Add(PrimitiveFieldUnknown);
        return diagnostics.Contains(PrimitiveFieldMissing) || diagnostics.Contains(PrimitiveFieldUnknown) ? null : new object();
    }

    private static void ValidatePrimitive(string recordType, Dictionary<string, double> values, List<string> diagnostics)
    {
        foreach (var (field, value) in values)
        {
            if (recordType == "Cone" && field == "topRadius" && value == 0d) continue;
            if (value <= 0d) diagnostics.Add(DegenerateDimension);
        }
        if (recordType == "Cone" && values.TryGetValue("bottomRadius", out var bottom) && bottom <= 0d) diagnostics.Add(DegenerateDimension);
        if (recordType == "Torus" && values.TryGetValue("majorRadius", out var major) && values.TryGetValue("minorRadius", out var minor) && major <= minor) diagnostics.Add(PrimitiveFieldInvalid);
    }

    private static IReadOnlyList<FirmamentV2ModifyBlock> ParseModifyBlocks(string source, Dictionary<string, FirmamentV2SolidBinding> byName, List<string> diagnostics)
    {
        var blocks = new List<FirmamentV2ModifyBlock>();
        foreach (Match match in ModifyHeaderRegex.Matches(source))
        {
            var target = match.Groups["target"].Value;
            if (!byName.TryGetValue(target, out var solid)) { diagnostics.Add(ModifyTargetUnresolved); continue; }
            if (!string.Equals(solid.RecordType, "Box", StringComparison.Ordinal)) { diagnostics.Add(ModifyTargetNotSolid); continue; }
            var open = source.IndexOf('{', match.Index);
            var close = FindMatchingBrace(source, open);
            if (close < 0) { diagnostics.Add(RegionUnsupported); continue; }
            var body = source[(open + 1)..close];
            var regions = new List<FirmamentV2RegionDecl>();
            var region = ParseRegion(body, solid, diagnostics);
            if (region is not null) regions.Add(region);
            var holes = ParseSemanticHoles(body, solid, diagnostics);
            if (regions.Count > 0 || holes.Count > 0) blocks.Add(new(target, regions, holes));
        }
        return blocks;
    }

    private static FirmamentV2RegionDecl? ParseRegion(string body, FirmamentV2SolidBinding solid, List<string> diagnostics)
    {
        var regions = RegionHeaderRegex.Matches(body);
        if (regions.Count == 0) return null;
        if (regions.Count != 1) { diagnostics.Add(RegionUnsupported); return null; }
        var rm = regions[0];
        if (!string.Equals(rm.Groups["name"].Value, "sideHole", StringComparison.Ordinal)) { diagnostics.Add(RegionUnsupported); return null; }
        var attach = ResolveFaceTarget(rm.Groups["target"].Value, solid, RegionAttachmentSelectorUnsupported, diagnostics);
        var open = body.IndexOf('{', rm.Index);
        var close = FindMatchingBrace(body, open);
        if (close < 0) { diagnostics.Add(RegionUnsupported); return null; }
        var cut = ParseCut(body[(open + 1)..close], solid, diagnostics);
        if (attach is null || cut is null) return null;
        var centerU = cut.Tool.Center?.U ?? 0;
        var centerV = cut.Tool.Center?.V ?? 0;
        var route = FirmamentV2SideHoleRoutePolicy.Resolve(attach.Axis, cut.Tool.Through.Axis, solid.Box!.Size, cut.Tool.Radius, centerU, centerV);
        if (!route.IsSupported)
        {
            diagnostics.Add(route.Diagnostic!);
            if (route.Diagnostic is SideHoleRouteUnsupported && (attach.Kind == "Alias" || cut.Tool.Through.Kind == "Alias")) diagnostics.Add(SideHoleAliasResolvesToUnsupportedFace);
            diagnostics.Add(SideHoleOnlyPlusXMinusXSupported);
        }
        return diagnostics.Any(IsFatalDiagnosticCode) ? null : new(rm.Groups["name"].Value, "FaceAttachedRegion", attach, cut);
    }

    private static IReadOnlyList<FirmamentV2SemanticHoleDecl> ParseSemanticHoles(string body, FirmamentV2SolidBinding solid, List<string> diagnostics)
    {
        var holes = new List<FirmamentV2SemanticHoleDecl>();
        foreach (Match hm in SemanticHoleHeaderRegex.Matches(body))
        {
            if (!Enum.TryParse<FirmamentV2SemanticHoleVariant>(hm.Groups["variant"].Value, true, out var variant)) { diagnostics.Add(HoleVariantUnknown); continue; }
            var open = body.IndexOf('{', hm.Index);
            var close = FindMatchingBrace(body, open);
            if (close < 0) { diagnostics.Add(RegionUnsupported); continue; }
            var hb = body[(open + 1)..close];
            var on = Regex.Match(hb, @"\bon\s*:\s*(?<target>face\([^)]*\)|[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant);
            var face = on.Success ? ResolveFaceTarget(on.Groups["target"].Value, solid, HoleEntryFaceMissing, diagnostics) : null;
            if (face is null) diagnostics.Add(HoleEntryFaceMissing);
            var center = CenterRegex.IsMatch(hb) ? ParseCenter(hb, diagnostics) : null;
            if (center is null) diagnostics.Add(HoleCenterMissing);
            var hasShaftDiameter = ReadPositive(hb, ["shaftDiameter", "diameter"], out var shaftDiameter);
            double shaftRadius = 0d;
            var hasShaftRadius = !hasShaftDiameter && ReadPositive(hb, ["shaftRadius", "radius"], out shaftRadius);
            var shaft = hasShaftDiameter || hasShaftRadius;
            var shaftDia = hasShaftDiameter ? shaftDiameter : (hasShaftRadius ? shaftRadius * 2d : 0d);
            if (!shaft) diagnostics.Add(HoleShaftMissing);
            var end = ParseEnd(hb, diagnostics);
            double? cbDia = null, cbDepth = null, csDia = null, csAngle = null;
            if (variant == FirmamentV2SemanticHoleVariant.Counterbore)
            {
                if (!(ReadPositive(hb, ["counterboreDiameter"], out var d) || (ReadPositive(hb, ["counterboreRadius"], out var r) && (d = r * 2d) > 0)) || d <= shaftDia) diagnostics.Add(HoleCounterboreInvalid); else cbDia = d;
                if (!ReadPositive(hb, ["counterboreDepth"], out var depth)) diagnostics.Add(HoleCounterboreInvalid); else cbDepth = depth;
            }
            if (variant == FirmamentV2SemanticHoleVariant.Countersink)
            {
                if (!(ReadPositive(hb, ["countersinkDiameter"], out var d) || (ReadPositive(hb, ["countersinkRadius"], out var r) && (d = r * 2d) > 0)) || d <= shaftDia) diagnostics.Add(HoleCountersinkInvalid); else csDia = d;
                if (!ReadPositive(hb, ["countersinkAngle"], out var angle) || angle <= 0 || angle >= 180) diagnostics.Add(HoleCountersinkInvalid); else csAngle = angle;
            }
            if (face is not null && center is not null && shaft && end is not null && !diagnostics.Any(IsFatalDiagnosticCode))
                holes.Add(new(hm.Groups["name"].Value, variant, face, center with { Convention = FirmamentV2FaceLocalPoint2D.ConventionFor(face.Axis) }, shaftDia, end, cbDia, cbDepth, csDia, csAngle));
        }
        return holes;
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

    private static FirmamentV2CutOperation? ParseCut(string body, FirmamentV2SolidBinding solid, List<string> diagnostics)
    {
        var cuts = CutHeaderRegex.Matches(body);
        if (cuts.Count != 1) { diagnostics.Add(CutUnsupported); return null; }
        var cm = cuts[0];
        if (!string.Equals(cm.Groups["tool"].Value, "Cylinder", StringComparison.Ordinal) && !string.Equals(cm.Groups["tool"].Value, "cylinder", StringComparison.Ordinal)) { diagnostics.Add(CutToolUnsupported); return null; }
        var open = body.IndexOf('{', cm.Index);
        var close = FindMatchingBrace(body, open);
        if (close < 0) { diagnostics.Add(CutUnsupported); return null; }
        var toolBody = body[(open + 1)..close];
        var radiusMatch = RadiusRegex.Match(toolBody);
        if (!radiusMatch.Success) { diagnostics.Add(CylinderRadiusMissing); return null; }
        if (!double.TryParse(radiusMatch.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var radius)) { diagnostics.Add(CylinderRadiusInvalid); return null; }
        if (!double.IsFinite(radius)) { diagnostics.Add(CylinderRadiusNotFinite); return null; }
        if (radius <= 0) { diagnostics.Add(CylinderRadiusInvalid); return null; }
        var center = ParseCenter(toolBody, diagnostics);
        var throughMatch = ThroughRegex.Match(toolBody);
        var through = throughMatch.Success ? ResolveFaceTarget(throughMatch.Groups["target"].Value, solid, ThroughSelectorUnsupported, diagnostics) : null;
        if (through is null) { diagnostics.Add(ThroughSelectorUnsupported); return null; }
        return new("Cut", new("Cylinder", radius, center, through));
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
        List<string> diagnostics)
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
        var holeNames = modifyBlocks.SelectMany(m => m.SemanticHoles).Select(h => h.Name).ToHashSet(StringComparer.Ordinal);
        var regionByTarget = recognizedRegions.ToDictionary(r => r.TargetSource, r => r, StringComparer.Ordinal);
        var aliases = solids.Values.SelectMany(s => s.Box?.Exposures ?? []).Select(e => e.Alias).ToHashSet(StringComparer.Ordinal);

        foreach (Match em in PmiEntryHeaderRegex.Matches(body))
        {
            var kindRaw = em.Groups["kind"].Value;
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
            if (!TryBindPmiRecord(record, solids, regionByTarget, aliases, holeNames, letMap, recordMap, knownDatums, diagnostics, out var b)) continue;
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
        var fieldRegex = new Regex(@"(?<name>targetA|targetB|target|dimension|value|diameter|tolerance|datum)\s*:\s*(?<value>.*?)(?=\s+(?:targetA|targetB|target|dimension|value|diameter|tolerance|datum)\s*:|$)", RegexOptions.CultureInvariant | RegexOptions.Singleline);
        foreach (Match m in fieldRegex.Matches(body))
        {
            var n = m.Groups["name"].Value;
            var v = m.Groups["value"].Value.Trim();
            if (v.EndsWith("}", StringComparison.Ordinal)) v = v[..^1].Trim();
            if (fields.ContainsKey(n)) { diagnostics.Add(PmiDuplicateField); continue; }
            fields[n] = new(n, v, new FirmamentV2SourceSpan(baseOffset + m.Index, m.Length), null);
        }
        if (fields.Count == 0 && body.Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries).Any(l => l.Trim().Length > 0)) diagnostics.Add(PmiUnsupported);
        return fields;
    }

    private static bool TryPmiKind(string raw, out FirmamentV2PmiKind kind) { kind = raw switch { "diameter"=>FirmamentV2PmiKind.HoleDiameter, "datum"=>FirmamentV2PmiKind.DatumPlane, "distance"=>FirmamentV2PmiKind.Distance, "flatness"=>FirmamentV2PmiKind.Flatness, "parallel"=>FirmamentV2PmiKind.Parallel, "perpendicular"=>FirmamentV2PmiKind.Perpendicular, "coplanar"=>FirmamentV2PmiKind.Coplanar, _=>default }; return raw is "diameter" or "datum" or "distance" or "flatness" or "parallel" or "perpendicular" or "coplanar"; }

    private static void ValidatePmiFieldSet(string kind, string name, IReadOnlyDictionary<string,FirmamentV2PmiField> fields, List<string> diagnostics)
    {
        string[] required = kind switch { "datum"=>["target"], "diameter"=> fields.ContainsKey("dimension") ? ["target","dimension"] : fields.ContainsKey("diameter") ? ["target","diameter"] : ["target","value"], "distance"=>["targetA","targetB","dimension"], "flatness"=>["target","tolerance"], "parallel" or "perpendicular" or "coplanar"=>["target","datum","tolerance"], _=>[] };
        var allowed = required.Concat(kind=="diameter"?["tolerance","diameter","value","dimension"]:[]).ToHashSet(StringComparer.Ordinal);
        foreach (var r in required) if (!fields.ContainsKey(r)) diagnostics.Add(PmiMissingRequiredField);
        foreach (var f in fields.Keys) if (!allowed.Contains(f)) diagnostics.Add(PmiUnknownField);
    }

    private static bool TryBindPmiRecord(FirmamentV2PmiRecord r, Dictionary<string,FirmamentV2SolidBinding> solids, IReadOnlyDictionary<string,FirmamentV2RecognizedRegion> regions, HashSet<string> aliases, HashSet<string> holeNames, IReadOnlyDictionary<string,FirmamentV2BoundLet> lets, IReadOnlyDictionary<string,FirmamentV2BoundLetRecord> records, HashSet<string> datums, List<string> diagnostics, out FirmamentV2BoundPmiRecord bound)
    {
        bound=null!;
        string? target = Field(r,"target"); string? targetA=Field(r,"targetA"); string? targetB=Field(r,"targetB");
        var targets = new[]{target,targetA,targetB}.Where(t=>t is not null).Cast<string>().ToArray();
        foreach (var t in targets) ValidateTarget(t, r.Kind, solids, regions, aliases, holeNames, diagnostics);
        FirmamentV2LiteralValue? dim=null; FirmamentV2Tolerance? dimTol=null; FirmamentV2LiteralValue? ctlTol=null; var datumRefs=new List<string>();
        if (r.Kind is FirmamentV2PmiKind.HoleDiameter or FirmamentV2PmiKind.Distance)
        {
            if (Field(r,"dimension") is string d) { var b=ResolveBoundLet(d, lets, records); if (b is null) diagnostics.Add(PmiDimensionTypeMismatch); else if (b.Type!=FirmamentV2PrimitiveType.Length) diagnostics.Add(PmiDimensionTypeMismatch); else { dim=b.Value; dimTol=b.Tolerance; } }
            else if (Field(r,"value") is string v && TryParsePmiLength(NormalizePmiLengthLiteral(v), diagnostics, out var lit)) dim=lit;
            else if (Field(r,"diameter") is string legacy && TryParsePmiLength(NormalizePmiLengthLiteral(legacy), diagnostics, out var legacyLit)) dim=legacyLit;
            if (Field(r,"tolerance") is string t && TryParseToleranceLiteral(NormalizePmiLengthLiteral(t), diagnostics, out var tol)) dimTol=tol;
            if (dim is null) { diagnostics.Add(PmiDiameterInvalid); return false; }
            if (dimTol is null && Field(r,"diameter") is null && Field(r,"value") is null) { diagnostics.Add(PmiDimensionMissingTolerance); return false; }
        }
        if (r.Kind is FirmamentV2PmiKind.Flatness or FirmamentV2PmiKind.Parallel or FirmamentV2PmiKind.Perpendicular or FirmamentV2PmiKind.Coplanar)
        { if (Field(r,"tolerance") is string t && TryParsePmiLength(NormalizePmiLengthLiteral(t), diagnostics, out var lit)) ctlTol=lit; else diagnostics.Add(PmiToleranceTypeMismatch); }
        if (r.Kind is FirmamentV2PmiKind.Parallel or FirmamentV2PmiKind.Perpendicular or FirmamentV2PmiKind.Coplanar)
        { var d=Field(r,"datum"); if (d is null || !datums.Contains(d)) diagnostics.Add(PmiUnknownDatum); else datumRefs.Add(d); }
        bound=new(r.Kind,r.Name,targets,dim,dimTol,ctlTol,datumRefs,r.SourceSpan); return !diagnostics.Any(x=>x.StartsWith("firmament-v2-pmi-",StringComparison.Ordinal) && x is not PmiUnsupported);
    }
    private static string? Field(FirmamentV2PmiRecord r,string name)=>r.Fields.TryGetValue(name,out var f)?f.Source:null;
    private static FirmamentV2BoundLet? ResolveBoundLet(string s,IReadOnlyDictionary<string,FirmamentV2BoundLet> lets,IReadOnlyDictionary<string,FirmamentV2BoundLetRecord> records){var m=DottedReferenceRegex.Match(s); if(m.Success&&records.TryGetValue(m.Groups["record"].Value,out var rec)&&rec.Fields.TryGetValue(m.Groups["field"].Value,out var f))return f; return lets.TryGetValue(s,out var l)?l:null;}
    private static string NormalizePmiLengthLiteral(string s) => Regex.Replace(s.Trim(), @"(?<=\d)\s+(?=mm$)", "", RegexOptions.CultureInvariant);
    private static bool TryParsePmiLength(string s,List<string> d,out FirmamentV2LiteralValue lit){lit=null!; var v=ParseLetLiteral(FirmamentV2PrimitiveType.Length,s,d); if(v is null){d.Add(PmiToleranceTypeMismatch); return false;} if(v.NumericValue is < 0){ d.Add(PmiDiameterInvalid); return false; } lit=v; return true;}
    private static bool TryParseToleranceLiteral(string s,List<string>d,out FirmamentV2Tolerance tol){tol=null!; var t=ParseTolerance(FirmamentV2PrimitiveType.Length,s,d); if(t is null){d.Add(PmiToleranceTypeMismatch); return false;} tol=t; return true;}
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

    private sealed record SolidMatch(string Name, string Target, bool IsWith, string Body);

    private static IEnumerable<SolidMatch> FindSolids(string source)
    {
        foreach (Match match in SolidHeaderRegex.Matches(source))
        {
            var open = source.IndexOf('{', match.Index);
            var close = FindMatchingBrace(source, open);
            if (close < 0) continue;
            yield return new(match.Groups["name"].Value, match.Groups["target"].Value, match.Groups["with"].Success, source[(open + 1)..close]);
        }
    }

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

    private static string StripLineComments(string sourceText) => string.Join('\n', sourceText.Split('\n').Select(line =>
    {
        var index = line.IndexOf("//", StringComparison.Ordinal);
        return index >= 0 ? line[..index] : line;
    }));
}
