using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Air;

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
    public const string PmiKindUnknown = "firmament-v2-pmi-kind-unknown";
    public const string PmiTargetMissing = "firmament-v2-pmi-target-missing";
    public const string PmiTargetUnresolved = "firmament-v2-pmi-target-unresolved";
    public const string PmiDiameterInvalid = "firmament-v2-pmi-diameter-invalid";
    public const string PmiDuplicateName = "firmament-v2-pmi-duplicate-name";
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
    private static readonly HashSet<string> ReservedWords = new(StringComparer.Ordinal) { "let", "model", "units", "solid", "modify", "region", "cut", "template", "concept", "pmi", "recognize", "replace", "with", "true", "false", "int", "float", "length", "angle", "string", "bool" };
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
    private static readonly Regex TemplateHeaderRegex = new(@"\btemplate\s*<\s*(?<process>[A-Za-z_][A-Za-z0-9_]*)\s*>\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex ConceptRegex = new(@"\bconcept\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<value>[-+0-9.eE]+)\s*(?<unit>[A-Za-z_][A-Za-z0-9_]*)?", RegexOptions.CultureInvariant);
    private static readonly Regex PathRegex = new("\\bpath\\s*:\\s*\"(?<path>[^\"]+)\"", RegexOptions.CultureInvariant);

    public static FirmamentV2ParseResult Parse(string sourceText) => Parse(sourceText, null);

    public static FirmamentV2ParseResult Parse(string sourceText, string? sourceDirectory)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        var diagnostics = new List<string> { "firmament-v2-parser-invoked" };
        var source = StripLineComments(sourceText);

        if (ContainsRawBackendId(source)) diagnostics.Add(RawBackendIdReferenceForbidden);
        if (ContainsUnsupportedConstruct(source)) diagnostics.Add(UnsupportedConstruct);

        var modelMatch = ModelRegex.Match(source);
        if (!modelMatch.Success) diagnostics.Add(MissingModel);

        var unitsMatch = UnitsRegex.Match(source);
        if (!unitsMatch.Success) diagnostics.Add(MissingUnits);
        else if (!string.Equals(unitsMatch.Groups["units"].Value, "mm", StringComparison.Ordinal)) diagnostics.Add(UnsupportedConstruct);

        var solidMatches = FindSolids(source).ToArray();
        if (solidMatches.Length == 0 && LegacyEqualsSolidRegex.IsMatch(source)) diagnostics.Add(UnsupportedConstruct);
        if (solidMatches.Length == 0) diagnostics.Add(MissingSolid);

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
        var recognizedRegions = ParseRecognizedRegions(source, byName, diagnostics);
        var replacements = ParseReplacements(source, byName, recognizedRegions, diagnostics);
        var pmi = ParsePmi(source, byName, modifyBlocks, recognizedRegions, diagnostics);

        FirmamentV2Document? document = null;
        if (modelMatch.Success && unitsMatch.Success && solids.Count > 0 && !diagnostics.Any(IsFatalDiagnostic))
            document = new FirmamentV2Document(modelMatch.Groups["name"].Value, unitsMatch.Groups["units"].Value, solids, modifyBlocks, templates, pmi, recognizedRegions, replacements, lets, boundLets, letRecords, boundLetRecords);

        diagnostics.Add(document is null ? "firmament-v2-parse-failed" : "firmament-v2-parse-succeeded");
        diagnostics.Sort(StringComparer.Ordinal);
        return document is null ? FirmamentV2ParseResult.Failure(diagnostics) : FirmamentV2ParseResult.Success(document, diagnostics);
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
                var expr = ParseLetValueExpression(type, fm.Groups["literal"].Value.Trim(), diagnostics);
                if (expr is FirmamentV2LiteralExpression) fields.Add(new(fieldName, type, expr, new FirmamentV2SourceSpan(open + 1 + body.IndexOf(rawLine, StringComparison.Ordinal), rawLine.Length)));
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
            var value = ParseLetValueExpression(type, literal, diagnostics);
            if (value is not null) lets.Add(new FirmamentV2LetDeclaration(name, type, value, new FirmamentV2SourceSpan(match.Index, match.Length)));
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
        { diagnostics.Add(literal.Contains("tol", StringComparison.Ordinal) ? LetInvalidLiteral : ExpressionUnsupported); return null; }
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
        records.Select(r => new FirmamentV2BoundLetRecord(r.Name, r.Fields.Where(f => f.ValueExpression is FirmamentV2LiteralExpression).Select(f => new FirmamentV2BoundLet(f.Name, f.DeclaredType, f.LiteralValue, f.SourceSpan, new FirmamentV2BoundExpression(f.DeclaredType, f.LiteralValue, new HashSet<string>(StringComparer.Ordinal), f.SourceSpan), new HashSet<string>(StringComparer.Ordinal))).ToDictionary(f => f.Name, StringComparer.Ordinal), r.SourceSpan)).ToArray();

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
            return _bound[name] = new FirmamentV2BoundLet(let.Name, let.DeclaredType, expr.Value, let.SourceSpan, expr, expr.Dependencies);
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
            return new(b.Type, b.Value, new HashSet<string>((b.Dependencies ?? new HashSet<string>()).Append(r.Name), StringComparer.Ordinal), b.SourceSpan);
        }
        private FirmamentV2BoundExpression? EvalDotted(FirmamentV2DottedReferenceExpression r)
        {
            if (_solidNames.Contains(r.RecordName) || _letMap.ContainsKey(r.RecordName)) { _diagnostics.Add(ExpressionScalarUsedAsRecord); _diagnostics.Add(LetReferenceNonRecord); return null; }
            if (!_records.TryGetValue(r.RecordName, out var record)) { _diagnostics.Add(ExpressionUnknownRecord); _diagnostics.Add(LetReferenceUnknownRecord); return null; }
            if (!record.Fields.TryGetValue(r.FieldName, out var f)) { _diagnostics.Add(ExpressionUnknownField); _diagnostics.Add(LetReferenceUnknownField); return null; }
            return new(f.Type, f.Value, new HashSet<string> { $"{r.RecordName}.{r.FieldName}" }, f.SourceSpan);
        }
        private FirmamentV2BoundExpression? EvalBinary(FirmamentV2BinaryExpression b)
        {
            var l = Eval(b.Left); var r = Eval(b.Right); if (l is null || r is null) return null;
            var type = ResultType(l.InferredType, b.Operator, r.InferredType);
            if (type is null) { _diagnostics.Add(ExpressionInvalidOperator); return null; }
            if (b.Operator == "/" && IsZero(r.Value)) { _diagnostics.Add(ExpressionDivisionByZero); return null; }
            var val = Compute(type.Value, l.Value, b.Operator, r.Value, b.Source);
            return new(type.Value, val, new HashSet<string>(l.Dependencies.Concat(r.Dependencies), StringComparer.Ordinal), new(0, b.Source.Length));
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
        var match = PathRegex.Match(body);
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

    private static bool IsFatalDiagnostic(string code) => code is PrimitiveFieldMissing or PrimitiveFieldUnknown or PrimitiveFieldInvalid or MissingModel or MissingUnits or MissingSolid or UnsupportedConstruct or UnknownRecordType or BoxMissingSize or BoxSizeArity or DegenerateDimension or NameUnresolved or DuplicateName or WithRequiresRecord or WithRequiresBoxRecord or WithFieldNotFound or WithFieldTypeMismatch or WithForwardReference or WithDerivedRecordInvalid or ExposeBlockUnsupported or ExposeRequiresBoxRecord or ExposeAliasDuplicate or ExposeAliasInvalid or SelectorUnsupported or SelectorAxisInvalid or SelectorSubselectorUnsupported or FatArrowOutsideExpose or RawBackendIdReferenceForbidden or ModifyTargetUnresolved or ModifyTargetNotSolid or RegionUnsupported or RegionAttachmentSelectorUnsupported or CutUnsupported or CutToolUnsupported or CylinderRadiusMissing or CylinderRadiusInvalid or CylinderRadiusNotFinite or ThroughSelectorUnsupported or AliasUnresolved or AliasRefTypeUnsupported or SideHoleAliasMustResolveToFace or SideHoleAliasResolvesToUnsupportedFace or SideHoleOnlyPlusXMinusXSupported or SideHoleRouteUnsupported or SideHoleSameFaceUnsupported or SideHoleAxisNotYetSupported or SideHoleRadiusExceedsClearance or CylinderCenterInvalid or CylinderCenterArityInvalid or CylinderCenterNotFinite or SideHoleCenterExceedsClearance or HoleVariantUnknown or HoleEntryFaceMissing or HoleCenterMissing or HoleShaftMissing or HoleEndMissing or HoleDiameterInvalid or HoleDepthInvalid or HoleCounterboreInvalid or HoleCountersinkInvalid or PmiKindUnknown or PmiTargetMissing or PmiTargetUnresolved or PmiDiameterInvalid or PmiDuplicateName or InlineStepUnknownBody or InlineStepUnknownFace or PmiImportedTargetNotFace or PmiImportedTargetRequiresCanonicalStep or PmiInvalidImportedTarget or InlineStepPathMissing or InlineStepPathInvalid or InlineStepFileMissing or InlineStepRequiresCanonical or UnknownRecognitionBody or UnknownRecognitionFace or DuplicateRegion or UnknownRecognitionRegion or InvalidRecognitionKind or InvalidRecognitionConfidence or PmiRecognizedRegionKindMismatch or UnknownReplacementBody or UnknownReplacementRegion or ReplacementKindMismatch or ReplacementFaceUnresolved or ReplacementUnsupportedKind or ReplacementVerificationFailed or ReplacementRadiusInvalid or ReplacementEndUnsupported or LetDuplicateName or LetUnknownType or LetTypeMismatch or LetInvalidLiteral or LetUnitMismatch or LetLiteralOnly or LetRecordDuplicateName or LetRecordDuplicateField or LetReferenceUnknownRecord or LetReferenceUnknownField or LetReferenceNonRecord or LetReferenceRecordUsedAsValue or ExpressionUnknownSymbol or ExpressionUnknownRecord or ExpressionUnknownField or ExpressionRecordUsedAsValue or ExpressionScalarUsedAsRecord or ExpressionTypeMismatch or ExpressionInvalidOperator or ExpressionDivisionByZero or ExpressionCycle or ExpressionUnsupported or RecognitionEvidenceRadiusInvalid or RecognitionEvidenceSurfaceFamilyUnknown or RecognitionEvidenceAxisInvalid or SemanticProposalKindMismatch or SemanticProposalRadiusInvalid or SemanticProposalTargetUnresolved or SemanticProposalEndUnsupported;

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
        return diagnostics.Any(IsFatalDiagnostic) ? null : new(rm.Groups["name"].Value, "FaceAttachedRegion", attach, cut);
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
            if (face is not null && center is not null && shaft && end is not null && !diagnostics.Any(IsFatalDiagnostic))
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
        return string.Equals(raw, "hole<shaft>", StringComparison.Ordinal) ? "holeShaft" : raw;
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

    private static IReadOnlyList<FirmamentV2PmiDecl> ParsePmi(string source, Dictionary<string, FirmamentV2SolidBinding> solids, IReadOnlyList<FirmamentV2ModifyBlock> modifyBlocks, IReadOnlyList<FirmamentV2RecognizedRegion> recognizedRegions, List<string> diagnostics)
    {
        var match = PmiHeaderRegex.Match(source);
        if (!match.Success) return [];
        var open = source.IndexOf('{', match.Index);
        var close = FindMatchingBrace(source, open);
        if (close < 0) { diagnostics.Add(UnsupportedConstruct); return []; }
        var body = source[(open + 1)..close];
        var entries = new List<FirmamentV2PmiDecl>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var holeNames = modifyBlocks.SelectMany(m => m.SemanticHoles).Select(h => h.Name).ToHashSet(StringComparer.Ordinal);
        var regionByTarget = recognizedRegions.ToDictionary(r => r.TargetSource, r => r, StringComparer.Ordinal);
        var aliases = solids.Values.SelectMany(s => s.Box?.Exposures ?? []).Select(e => e.Alias).ToHashSet(StringComparer.Ordinal);
        foreach (Match em in PmiEntryHeaderRegex.Matches(body))
        {
            var kindRaw = em.Groups["kind"].Value;
            var name = em.Groups["name"].Value;
            if (!names.Add(name)) { diagnostics.Add(PmiDuplicateName); continue; }
            var entryOpen = body.IndexOf('{', em.Index);
            var entryClose = FindMatchingBrace(body, entryOpen);
            if (entryClose < 0) { diagnostics.Add(UnsupportedConstruct); continue; }
            var eb = body[(entryOpen + 1)..entryClose];
            var targetMatch = TargetRegex.Match(eb);
            if (!targetMatch.Success) { diagnostics.Add(PmiTargetMissing); continue; }
            var target = targetMatch.Groups["target"].Value.Trim();
            if (string.Equals(kindRaw, "diameter", StringComparison.Ordinal))
            {
                var valueMatch = ValueRegex.Match(eb);
                if (!valueMatch.Success || !TryParsePositiveNumberWithOptionalMm(valueMatch.Groups["value"].Value, out var value)) { diagnostics.Add(PmiDiameterInvalid); continue; }
                if (TryValidateImportedFaceTarget(target, solids, diagnostics))
                {
                    entries.Add(new FirmamentV2PmiDecl(name, FirmamentV2PmiKind.HoleDiameter, target, value));
                    continue;
                }
                if (RecognizedRegionTargetRegex.IsMatch(target))
                {
                    if (!regionByTarget.TryGetValue(target, out var region)) { diagnostics.Add(UnknownRecognitionRegion); continue; }
                    if (region.Kind != "holeShaft") { diagnostics.Add(PmiRecognizedRegionKindMismatch); continue; }
                    entries.Add(new FirmamentV2PmiDecl(name, FirmamentV2PmiKind.HoleDiameter, target, value));
                    continue;
                }
                if (!holeNames.Contains(target)) { diagnostics.Add(PmiTargetUnresolved); continue; }
                entries.Add(new FirmamentV2PmiDecl(name, FirmamentV2PmiKind.HoleDiameter, target, value));
            }
            else if (string.Equals(kindRaw, "datum", StringComparison.Ordinal))
            {
                if (TryValidateImportedFaceTarget(target, solids, diagnostics))
                {
                    entries.Add(new FirmamentV2PmiDecl(name, FirmamentV2PmiKind.DatumPlane, target));
                    continue;
                }
                if (RecognizedRegionTargetRegex.IsMatch(target))
                {
                    if (!regionByTarget.TryGetValue(target, out var region)) { diagnostics.Add(UnknownRecognitionRegion); continue; }
                    if (region.Kind != "datumPlane") { diagnostics.Add(PmiRecognizedRegionKindMismatch); continue; }
                    entries.Add(new FirmamentV2PmiDecl(name, FirmamentV2PmiKind.DatumPlane, target));
                    continue;
                }
                if (!aliases.Contains(target) && !FaceSelectorRegex.IsMatch(target)) { diagnostics.Add(PmiTargetUnresolved); continue; }
                entries.Add(new FirmamentV2PmiDecl(name, FirmamentV2PmiKind.DatumPlane, target));
            }
            else
            {
                diagnostics.Add(PmiKindUnknown);
            }
        }
        return entries;
    }

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
        Regex.IsMatch(source, @"\b(PMI|where|add|shell|fillet|chamfer|regions|profile|material|pattern)\b|<\s*Process\s*>", RegexOptions.CultureInvariant);

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
