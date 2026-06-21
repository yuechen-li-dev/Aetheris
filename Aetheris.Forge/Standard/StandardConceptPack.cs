using Aetheris.Forge.Abstractions;

namespace Aetheris.Forge.Standard;

public static class StandardConceptPack
{
    public const string PackageId = "Aetheris.Standard";

    public static ForgePackageDescriptor CreatePackage() => new(
        PackageId: PackageId,
        DisplayName: "Aetheris Standard Concept Pack",
        SemanticVersion: "0.2.0",
        Vendor: "Aetheris",
        Description: "Built-in descriptor-only semantic concept pack for blessed Standard CAD authoring concepts.",
        RequestedTrustTier: ForgeTrustTier.SemanticDocsOnly,
        HostRequirements: ["Aetheris.Forge.Abstractions", "MetadataOnly", "NoPluginExecution"],
        Concepts: [CreateCnc(), CreateHole(), CreateShaftHole(), CreateCounterboreHole(), CreateCountersinkHole(), CreateEdgeFinish()],
        Templates: [],
        Capabilities:
        [
            new ForgeCapabilityDescriptor("Standard.MetadataOnly", ForgeTrustTier.SemanticDocsOnly, "Descriptors document semantic contracts only; they do not execute plugins, lowerers, materializers, or BRep helpers.", []),
            new ForgeCapabilityDescriptor("Standard.Hole.AirHoleFeatureMetadata", ForgeTrustTier.SemanticDocsOnly, "Metadata naming AirHoleFeature as the future semantic-hole AIR feature family without executing lowering.", ["AirHoleFeature"]),
            new ForgeCapabilityDescriptor("Standard.StackedHole.Metadata", ForgeTrustTier.SemanticDocsOnly, "Metadata distinguishing counterbore and countersink stacked-hole concepts from a base shaft hole.", []),
            new ForgeCapabilityDescriptor("Standard.Process.CncMetadata", ForgeTrustTier.SemanticDocsOnly, "Metadata for CNC/prismatic process constraints that are not BRep feature lowering contracts.", [])
        ],
        Examples: [new ForgeExampleDescriptor("standard-concept-pack-minimal", "package Aetheris.Standard concepts: Standard.CNC, Standard.Hole, Standard.EdgeFinish", "Descriptor-only package inventory example; not parser syntax.")],
        Fixtures: [],
        LlmGuidance: [new ForgeLlmGuidanceDescriptor("standard-concept-pack-implementation-note", "docs/implementation/forge-x2-standard-concept-pack-scaffold.md", "FORGE-X2 implementation note for the built-in Standard concept pack scaffold.")]);

    public static ForgeConceptDescriptor CreateCnc() => new(
        "Standard.CNC", "Standard CNC", "ManufacturingProcess", "Descriptor-only CNC/prismatic process concept for manufacturing assumptions and constraints; it is not a geometry feature.",
        [
            new ForgeFieldDescriptor("processFamily", ForgeFieldType.String, false, null, "Manufacturing process family metadata.", "CNC/prismatic", ["CNC/prismatic"]),
            new ForgeFieldDescriptor("minimumToolRadius", ForgeFieldType.Length, false, "Length", "Optional lower bound for modeled internal tool radius assumptions."),
            new ForgeFieldDescriptor("minimumWallThickness", ForgeFieldType.Length, false, "Length", "Optional lower bound for wall-thickness manufacturability assumptions."),
            new ForgeFieldDescriptor("preferredInsideCorner", ForgeFieldType.String, false, null, "Optional policy hint for preferred CNC inside corner treatment.", null, ["fillet", "relief", "defer"])
        ],
        [
            new ForgeDiagnosticDescriptor("standard-cnc-invalid-minimum-tool-radius", ForgeDiagnosticSeverity.Error, "Standard.CNC minimumToolRadius must be positive when present.", "Descriptor-level CNC constraint diagnostic."),
            new ForgeDiagnosticDescriptor("standard-cnc-invalid-minimum-wall-thickness", ForgeDiagnosticSeverity.Error, "Standard.CNC minimumWallThickness must be positive when present.", "Descriptor-level CNC constraint diagnostic.")
        ],
        ["Standard.MetadataOnly", "Standard.Process.CncMetadata"], [], [], [], [],
        ManufacturingAssumptions: ["processFamily=CNC/prismatic", "process-concept-not-geometry-feature", "no-brep-lowering-contract"],
        DerivedFields: [], ValidationRuleIds: ["standard-cnc-invalid-minimum-tool-radius", "standard-cnc-invalid-minimum-wall-thickness"]);

    public static ForgeConceptDescriptor CreateHole() => HoleLike("Standard.Hole", "Standard Hole", "Base descriptor-only semantic hole concept.", null, "shaft");
    public static ForgeConceptDescriptor CreateShaftHole() => HoleLike("Standard.ShaftHole", "Standard Shaft Hole", "Descriptor-only simple shaft-hole refinement of Standard.Hole.", "Standard.Hole", "shaft");
    public static ForgeConceptDescriptor CreateCounterboreHole() => HoleLike("Standard.CounterboreHole", "Standard Counterbore Hole", "Descriptor-only counterbore stacked-hole refinement of Standard.Hole.", "Standard.Hole", "counterbore");
    public static ForgeConceptDescriptor CreateCountersinkHole() => HoleLike("Standard.CountersinkHole", "Standard Countersink Hole", "Descriptor-only countersink stacked-hole refinement of Standard.Hole.", "Standard.Hole", "countersink");

    public static ForgeConceptDescriptor CreateEdgeFinish() => new(
        "Standard.EdgeFinish", "Standard Edge Finish", "EdgeFinish", "Descriptor-only concept for fillet/chamfer/round edge-finish intent. Lowering is deferred until a suitable AIR feature exists.",
        [new ForgeFieldDescriptor("target", ForgeFieldType.SemanticReference, true, null, "Semantic reference to edge intent."), new ForgeFieldDescriptor("kind", ForgeFieldType.String, true, null, "Edge-finish kind metadata.", null, ["fillet", "chamfer", "round"]), new ForgeFieldDescriptor("radiusOrDistance", ForgeFieldType.Length, true, "Length", "Nominal radius or chamfer distance metadata."), new ForgeFieldDescriptor("scope", ForgeFieldType.String, false, null, "Optional scope metadata.", "singleEdge", ["singleEdge", "loop", "partialLoop", "featureEdgeRole", "simpleBodyAllEdges"])],
        [new ForgeDiagnosticDescriptor("standard-edge-finish-invalid-target", ForgeDiagnosticSeverity.Error, "Standard.EdgeFinish target must resolve to supported semantic edge intent.", "Descriptor-level target validation diagnostic."), new ForgeDiagnosticDescriptor("standard-edge-finish-invalid-size", ForgeDiagnosticSeverity.Error, "Standard.EdgeFinish radiusOrDistance must be positive.", "Descriptor-level size validation diagnostic."), new ForgeDiagnosticDescriptor("standard-edge-finish-unsupported-scope", ForgeDiagnosticSeverity.Error, "Standard.EdgeFinish scope is unsupported.", "Descriptor-level scope validation diagnostic.")],
        ["Standard.MetadataOnly"], [], [], [], [],
        ManufacturingAssumptions: ["lowering-deferred", "descriptor-only", "no-current-air-feature-claim"], DerivedFields: ["scope defaults to singleEdge when omitted"], ValidationRuleIds: ["standard-edge-finish-invalid-target", "standard-edge-finish-invalid-size", "standard-edge-finish-unsupported-scope"]);

    private static ForgeConceptDescriptor HoleLike(string id, string name, string description, string? baseConceptId, string stackKind)
    {
        var fields = new List<ForgeFieldDescriptor> { new("entryFace", ForgeFieldType.FaceSelector, true, null, "Face receiving the hole."), new("center", ForgeFieldType.FaceLocalPoint2D, true, null, "Face-local hole center."), new("shaftDiameter", ForgeFieldType.Length, true, "Length", "Nominal shaft diameter."), new("endCondition", ForgeFieldType.HoleEndCondition, true, null, "Descriptor-level through/blind/end-condition metadata.") };
        if (stackKind == "counterbore") { fields.Add(new("counterboreDiameter", ForgeFieldType.Length, true, "Length", "Counterbore diameter metadata.")); fields.Add(new("counterboreDepth", ForgeFieldType.Length, true, "Length", "Counterbore depth metadata.")); }
        if (stackKind == "countersink") { fields.Add(new("countersinkDiameter", ForgeFieldType.Length, true, "Length", "Countersink diameter metadata.")); fields.Add(new("countersinkAngle", ForgeFieldType.Angle, true, "Angle", "Countersink included angle metadata.")); }
        var diagnostics = new List<ForgeDiagnosticDescriptor> { new("standard-hole-missing-entry-face", ForgeDiagnosticSeverity.Error, "Standard.Hole requires an entry face.", "Raised when entryFace metadata is absent."), new("standard-hole-invalid-center", ForgeDiagnosticSeverity.Error, "Standard.Hole center must be valid on the entry face.", "Raised when center metadata is invalid."), new("standard-hole-invalid-diameter", ForgeDiagnosticSeverity.Error, "Standard.Hole shaftDiameter must be positive.", "Raised when shaftDiameter metadata is invalid."), new("standard-hole-invalid-end-condition", ForgeDiagnosticSeverity.Error, "Standard.Hole endCondition is unsupported.", "Raised when endCondition metadata is invalid.") };
        if (stackKind == "counterbore") { diagnostics.Add(new("standard-counterbore-invalid-diameter", ForgeDiagnosticSeverity.Error, "Standard.CounterboreHole counterboreDiameter must be positive.", "Counterbore descriptor diagnostic.")); diagnostics.Add(new("standard-counterbore-invalid-depth", ForgeDiagnosticSeverity.Error, "Standard.CounterboreHole counterboreDepth must be positive.", "Counterbore descriptor diagnostic.")); diagnostics.Add(new("standard-counterbore-diameter-not-greater-than-shaft", ForgeDiagnosticSeverity.Error, "Counterbore diameter must be greater than shaft diameter.", "Counterbore stack relationship diagnostic.")); }
        if (stackKind == "countersink") { diagnostics.Add(new("standard-countersink-invalid-diameter", ForgeDiagnosticSeverity.Error, "Standard.CountersinkHole countersinkDiameter must be positive.", "Countersink descriptor diagnostic.")); diagnostics.Add(new("standard-countersink-invalid-angle", ForgeDiagnosticSeverity.Error, "Standard.CountersinkHole countersinkAngle must be valid.", "Countersink descriptor diagnostic.")); diagnostics.Add(new("standard-countersink-diameter-not-greater-than-shaft", ForgeDiagnosticSeverity.Error, "Countersink diameter must be greater than shaft diameter.", "Countersink stack relationship diagnostic.")); }
        var capabilities = stackKind is "counterbore" or "countersink" ? ["Standard.MetadataOnly", "Standard.Hole.AirHoleFeatureMetadata", "Standard.StackedHole.Metadata"] : new[] { "Standard.MetadataOnly", "Standard.Hole.AirHoleFeatureMetadata" };
        return new ForgeConceptDescriptor(id, name, "Hole", description, fields, diagnostics, capabilities,
            [new ForgeLoweringContractDescriptor(id + ".ToAirHoleFeature", id, "AirHoleFeature", ["Standard.Hole.AirHoleFeatureMetadata"], $"Metadata contract naming AirHoleFeature as the target family for {id}; no lowerer is executed.")], [], [], [],
            ManufacturingAssumptions: [.. (baseConceptId is null ? [] : new[] { "baseConceptId=" + baseConceptId }), "targetAirFeatureFamily=AirHoleFeature", "stack=" + stackKind, "descriptor-only-no-lowering-execution"],
            DerivedFields: stackKind is "counterbore" or "countersink" ? ["stackKind=" + stackKind] : [], ValidationRuleIds: diagnostics.Select(d => d.DiagnosticId).ToArray());
    }
}
