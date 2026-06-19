using System.Globalization;
using System.Text.RegularExpressions;

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

    private static readonly Regex ModelRegex = new(@"\bmodel\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex UnitsRegex = new(@"\bunits\s+(?<units>[A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.CultureInvariant);
    private static readonly Regex SolidHeaderRegex = new(@"\bsolid\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<target>[A-Za-z_][A-Za-z0-9_]*)(?<with>\s+with)?\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex LegacyEqualsSolidRegex = new(@"\bsolid\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<target>[A-Za-z_][A-Za-z0-9_]*)(?<with>\s+with)?\s*\{(?<body>.*?)\}", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex SizeRegex = new(@"\bsize\s*:\s*\[(?<values>[^\]]*)\]", RegexOptions.CultureInvariant | RegexOptions.Singleline);
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

    public static FirmamentV2ParseResult Parse(string sourceText)
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
                    : ParseDirect(name, target, body, diagnostics);
                if (binding is not null)
                {
                    solids.Add(binding);
                    byName.Add(name, binding);
                }
            }
        }

        var modifyBlocks = ParseModifyBlocks(source, byName, diagnostics);

        FirmamentV2Document? document = null;
        if (modelMatch.Success && unitsMatch.Success && solids.Count > 0 && !diagnostics.Any(IsFatalDiagnostic))
            document = new FirmamentV2Document(modelMatch.Groups["name"].Value, unitsMatch.Groups["units"].Value, solids, modifyBlocks);

        diagnostics.Add(document is null ? "firmament-v2-parse-failed" : "firmament-v2-parse-succeeded");
        diagnostics.Sort(StringComparer.Ordinal);
        return document is null ? FirmamentV2ParseResult.Failure(diagnostics) : FirmamentV2ParseResult.Success(document, diagnostics);
    }

    private static FirmamentV2SolidBinding? ParseDirect(string name, string recordType, string body, List<string> diagnostics)
    {
        if (!string.Equals(recordType, "Box", StringComparison.Ordinal)) { diagnostics.Add(UnknownRecordType); return null; }
        var values = ParseSizeField(body, diagnostics, BoxMissingSize);
        var exposures = ParseExposures(body, diagnostics);
        return values is null ? null : new(name, "Box", new(values, exposures));
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
        return new(name, "Box", new(values, []), baseName, new Dictionary<string, IReadOnlyList<double>>(StringComparer.Ordinal) { ["size"] = values });
    }

    private static bool IsFatalDiagnostic(string code) => code is MissingModel or MissingUnits or MissingSolid or UnsupportedConstruct or UnknownRecordType or BoxMissingSize or BoxSizeArity or DegenerateDimension or NameUnresolved or DuplicateName or WithRequiresRecord or WithRequiresBoxRecord or WithFieldNotFound or WithFieldTypeMismatch or WithForwardReference or WithDerivedRecordInvalid or ExposeBlockUnsupported or ExposeRequiresBoxRecord or ExposeAliasDuplicate or ExposeAliasInvalid or SelectorUnsupported or SelectorAxisInvalid or SelectorSubselectorUnsupported or FatArrowOutsideExpose or RawBackendIdReferenceForbidden or ModifyTargetUnresolved or ModifyTargetNotSolid or RegionUnsupported or RegionAttachmentSelectorUnsupported or CutUnsupported or CutToolUnsupported or CylinderRadiusMissing or CylinderRadiusInvalid or CylinderRadiusNotFinite or ThroughSelectorUnsupported or AliasUnresolved or AliasRefTypeUnsupported or SideHoleAliasMustResolveToFace or SideHoleAliasResolvesToUnsupportedFace or SideHoleOnlyPlusXMinusXSupported or SideHoleRouteUnsupported or SideHoleSameFaceUnsupported or SideHoleAxisNotYetSupported or SideHoleRadiusExceedsClearance or CylinderCenterInvalid or CylinderCenterArityInvalid or CylinderCenterNotFinite or SideHoleCenterExceedsClearance;

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
            var region = ParseRegion(body, solid, diagnostics);
            if (region is not null) blocks.Add(new(target, [region]));
        }
        return blocks;
    }

    private static FirmamentV2RegionDecl? ParseRegion(string body, FirmamentV2SolidBinding solid, List<string> diagnostics)
    {
        var regions = RegionHeaderRegex.Matches(body);
        if (regions.Count != 1) { diagnostics.Add(RegionUnsupported); return null; }
        var rm = regions[0];
        if (!string.Equals(rm.Groups["name"].Value, "sideHole", StringComparison.Ordinal)) { diagnostics.Add(RegionUnsupported); return null; }
        var attach = ResolveFaceTarget(rm.Groups["target"].Value, solid, RegionAttachmentSelectorUnsupported, diagnostics);
        var open = body.IndexOf('{', rm.Index);
        var close = FindMatchingBrace(body, open);
        if (close < 0) { diagnostics.Add(RegionUnsupported); return null; }
        var cut = ParseCut(body[(open + 1)..close], solid, diagnostics);
        if (attach is null || cut is null) return null;
        var supportedCanonical = attach.Axis == "+X" && cut.Tool.Through.Axis == "-X";
        var supportedReverseX = attach.Axis == "-X" && cut.Tool.Through.Axis == "+X";
        var supportedY = (attach.Axis == "+Y" && cut.Tool.Through.Axis == "-Y") || (attach.Axis == "-Y" && cut.Tool.Through.Axis == "+Y");
        var supportedZ = (attach.Axis == "+Z" && cut.Tool.Through.Axis == "-Z") || (attach.Axis == "-Z" && cut.Tool.Through.Axis == "+Z");
        if (!supportedCanonical && !supportedReverseX && !supportedY && !supportedZ)
        {
            if (attach.Axis == cut.Tool.Through.Axis) diagnostics.Add(SideHoleSameFaceUnsupported);
            else if (AxisName(attach.Axis) != AxisName(cut.Tool.Through.Axis)) diagnostics.Add(SideHoleRouteUnsupported);
            else diagnostics.Add(SideHoleRouteUnsupported);
            if (attach.Kind == "Alias" || cut.Tool.Through.Kind == "Alias") diagnostics.Add(SideHoleAliasResolvesToUnsupportedFace);
            diagnostics.Add(SideHoleOnlyPlusXMinusXSupported);
        }
        var axis = AxisName(attach.Axis);
        var uHalfExtent = axis switch
        {
            "Y" => solid.Box.Size[0] / 2.0,
            "Z" => solid.Box.Size[0] / 2.0,
            _ => solid.Box.Size[1] / 2.0
        };
        var vHalfExtent = axis == "Z" ? solid.Box.Size[1] / 2.0 : solid.Box.Size[2] / 2.0;
        if (cut.Tool.Radius >= Math.Min(uHalfExtent, vHalfExtent)) diagnostics.Add(SideHoleRadiusExceedsClearance);
        var centerU = cut.Tool.Center?.U ?? 0;
        var centerV = cut.Tool.Center?.V ?? 0;
        if (Math.Abs(centerU) + cut.Tool.Radius >= uHalfExtent || Math.Abs(centerV) + cut.Tool.Radius >= vHalfExtent) diagnostics.Add(SideHoleCenterExceedsClearance);
        return diagnostics.Any(IsFatalDiagnostic) ? null : new(rm.Groups["name"].Value, "FaceAttachedRegion", attach, cut);
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

    private static string AxisName(string faceAxis) => faceAxis.Length == 2 ? faceAxis[1].ToString() : string.Empty;

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

        var exposure = solid.Box.Exposures.FirstOrDefault(e => string.Equals(e.Alias, source, StringComparison.Ordinal));
        if (exposure is null) { diagnostics.Add(AliasUnresolved); return null; }
        if (!string.Equals(exposure.RefType, "FaceRef", StringComparison.Ordinal))
        {
            diagnostics.Add(AliasRefTypeUnsupported);
            diagnostics.Add(SideHoleAliasMustResolveToFace);
            return null;
        }
        return FirmamentV2FaceTarget.Alias(source, exposure.Axis);
    }

    private static bool ContainsUnsupportedConstruct(string source) =>
        Regex.IsMatch(source, @"\b(concept|PMI|where|template|add|shell|fillet|chamfer|regions|profile|material|pattern)\b|<\s*Process\s*>", RegexOptions.CultureInvariant);

    private static bool ContainsRawBackendId(string source) =>
        Regex.IsMatch(source, @"\b(brep|step|backend|coedge)\s*\.|STEP\s*#|#[0-9]+", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

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
