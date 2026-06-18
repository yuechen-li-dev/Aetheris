using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class AirTraceCommandTests
{
    [Fact]
    public void TraceHelp_IsDiscoverable()
    {
        var stdout = new StringWriter(); var stderr = new StringWriter();
        Assert.Equal(0, Aetheris.CLI.CliRunner.Run(["--help"], stdout, stderr));
        Assert.Contains("trace", stdout.ToString(), StringComparison.Ordinal);

        stdout = new StringWriter(); stderr = new StringWriter();
        Assert.Equal(0, Aetheris.CLI.CliRunner.Run(["trace", "--help"], stdout, stderr));
        var text = stdout.ToString();
        Assert.Contains("default output is human-readable text", text, StringComparison.Ordinal);
        Assert.Contains("--json", text, StringComparison.Ordinal);
        Assert.Contains("analyze", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TracePrismatic_DefaultsToText()
    {
        var (exitCode, output, error) = Run("trace", "--case", "prismatic-section-transition");
        Assert.Equal(0, exitCode); Assert.True(string.IsNullOrWhiteSpace(error));
        Assert.False(output.TrimStart().StartsWith("{", StringComparison.Ordinal));
        Assert.Contains("Aetheris trace", output); Assert.Contains("Case: prismatic-section-transition", output);
        Assert.Contains("Trace kind: lowering", output); Assert.Contains("Route decision", output);
        Assert.Contains("BRepPlan", output); Assert.Contains("CIR mirror", output); Assert.Contains("STEP smoke", output);
        Assert.Contains("no production route replacement", output);
    }

    [Fact]
    public void TraceTopFaceLoopChamfer_DefaultsToText()
    {
        var (exitCode, output, _) = Run("trace", "--case", "top-face-loop-chamfer");
        Assert.Equal(0, exitCode); Assert.False(output.TrimStart().StartsWith("{", StringComparison.Ordinal));
        Assert.Contains("TopFaceLoopChamfer", output); Assert.Contains("FaceBoundaryLoop", output);
        Assert.Contains("UniformChamfer", output); Assert.Contains("SwitchMatch", output);
        Assert.Contains("Chamfer faces: 4", output); Assert.Contains("not four independent single-edge chamfers", output);
        Assert.Contains("no AirEdgeSweep", output); Assert.Contains("no BrepBoundedChamfer", output);
    }

    [Fact]
    public void TracePrismatic_JsonOutput_IsDeterministicAndParses()
    {
        var (exitCode, output, _) = Run("trace", "--case", "prismatic-section-transition", "--json");
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output); var root = doc.RootElement;
        Assert.Equal("trace", root.GetProperty("command").GetString());
        Assert.Equal("lowering", root.GetProperty("traceKind").GetString());
        Assert.Equal("built-in-case", root.GetProperty("inputKind").GetString());
        Assert.Equal("prismatic-section-transition", root.GetProperty("caseName").GetString());
        Assert.Equal("Direct", root.GetProperty("routeDecision").GetProperty("mode").GetString());
        var plan = root.GetProperty("brepPlan");
        Assert.Equal(12, plan.GetProperty("vertices").GetInt32()); Assert.Equal(20, plan.GetProperty("edges").GetInt32());
        Assert.Equal(10, plan.GetProperty("faces").GetInt32()); Assert.Equal(40, plan.GetProperty("coedges").GetInt32());
        Assert.True(root.TryGetProperty("cirMirror", out _));
        Assert.Contains("topology-parity", root.GetProperty("knownLosses").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public void TraceTopFaceLoopChamfer_JsonOutput_IsDeterministicAndParses()
    {
        var (exitCode, output, _) = Run("trace", "--case", "top-face-loop-chamfer", "--json");
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output); var root = doc.RootElement;
        Assert.Equal("top-face-loop-chamfer", root.GetProperty("caseName").GetString());
        Assert.Equal("FaceBoundaryLoop", root.GetProperty("air").GetProperty("selectionClass").GetString());
        Assert.Equal("UniformChamfer", root.GetProperty("air").GetProperty("rule").GetString());
        Assert.Equal("SwitchMatch", root.GetProperty("routeDecision").GetProperty("mode").GetString());
        Assert.Equal(4, root.GetProperty("brepPlan").GetProperty("chamferFaces").GetInt32());
        var losses = root.GetProperty("cirMirror").GetProperty("knownLosses").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains("chamfer-face-identity", losses); Assert.Contains("brep-plan-role-parity", losses);
        Assert.Contains("not four independent single-edge chamfers", root.GetProperty("guarantees").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public void TraceOutDir_WritesTextOrJsonReport()
    {
        var dir = Path.Combine(Path.GetTempPath(), "aetheris-trace-tests", Guid.NewGuid().ToString("N"));
        var (textExit, textOut, _) = Run("trace", "--case", "top-face-loop-chamfer", "--out-dir", dir);
        Assert.Equal(0, textExit); Assert.Contains("Trace report written:", textOut);
        var textPath = Path.Combine(dir, "air-x6-top-face-loop-chamfer-trace.txt"); Assert.True(File.Exists(textPath));
        Assert.Contains("Aetheris trace", File.ReadAllText(textPath));
        var (jsonExit, jsonOut, _) = Run("trace", "--case", "top-face-loop-chamfer", "--out-dir", dir, "--json");
        Assert.Equal(0, jsonExit); JsonDocument.Parse(jsonOut).Dispose();
        Assert.True(File.Exists(Path.Combine(dir, "air-x6-top-face-loop-chamfer-trace.json")));
    }

    [Fact]
    public void TraceInvalidUsage_IsHelpful()
    {
        var missing = Run("trace"); Assert.NotEqual(0, missing.ExitCode); Assert.Contains("--case", missing.Stderr); Assert.Contains("Supported cases", missing.Stderr);
        var unknown = Run("trace", "--case", "nope"); Assert.NotEqual(0, unknown.ExitCode); Assert.Contains("Supported cases", unknown.Stderr);
        var step = Run("trace", "some.step"); Assert.NotEqual(0, step.ExitCode); Assert.Contains("trace does not analyze STEP files", step.Stderr); Assert.Contains("aetheris analyze", step.Stderr);
    }

    [Fact]
    public void TraceOutput_IsDeterministic()
    {
        Assert.Equal(Run("trace", "--case", "prismatic-section-transition", "--json").Stdout, Run("trace", "--case", "prismatic-section-transition", "--json").Stdout);
        Assert.Equal(Run("trace", "--case", "top-face-loop-chamfer", "--json").Stdout, Run("trace", "--case", "top-face-loop-chamfer", "--json").Stdout);
    }



    [Fact]
    public void TraceParserBackedBoxFixture_ReachesProfileEmission_DefaultText()
    {
        var (exitCode, output, error) = Run("trace", "--fixture", PrimitiveFixture("valid/box.valid.firmfixture"));
        Assert.Equal(0, exitCode); Assert.True(string.IsNullOrWhiteSpace(error));
        Assert.False(output.TrimStart().StartsWith("{", StringComparison.Ordinal));
        Assert.Contains("Fixture", output); Assert.Contains("Frontend", output);
        Assert.Contains("Parser-backed: true", output); Assert.Contains("Parse succeeded: true", output);
        Assert.Contains("Expected stage: emitted-brep", output); Assert.Contains("Actual stage: emitted-brep", output);
        Assert.Contains("Feature AIR", output); Assert.Contains("Source op: box", output); Assert.Contains("Node: CreateBox", output);
        Assert.Contains("Constructive AIR", output); Assert.Contains("Node: AirProfileExtrude", output); Assert.Contains("Canonical form: rectangle-profile-extrude", output);
        Assert.Contains("Profile: Rectangle(width=10, depth=8)", output); Assert.Contains("Extrusion: height=6", output);
        Assert.Contains("Profile extrusion emission", output); Assert.Contains("Wrapper invoked: true", output);
        Assert.Contains("Emitter: LineArcProfileExtrudeEmitter", output); Assert.Contains("Succeeded: true", output);
        Assert.Contains("Expectation satisfied: true", output);
    }

    [Fact]
    public void TraceParserBackedBoxFixture_ReachesProfileEmission_Json()
    {
        var (exitCode, output, _) = Run("trace", "--fixture", PrimitiveFixture("valid/box.valid.firmfixture"), "--json");
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output); var root = doc.RootElement;
        Assert.Equal("firmfixture", root.GetProperty("inputKind").GetString());
        Assert.Equal("valid", root.GetProperty("fixture").GetProperty("expectation").GetString());
        Assert.True(root.GetProperty("fixture").GetProperty("parserBacked").GetBoolean());
        Assert.True(root.GetProperty("fixture").GetProperty("expectationSatisfied").GetBoolean());
        Assert.True(root.GetProperty("frontend").GetProperty("parserBacked").GetBoolean());
        Assert.True(root.GetProperty("frontend").GetProperty("parseSucceeded").GetBoolean());
        Assert.Equal("emitted-brep", root.GetProperty("frontend").GetProperty("frontendStageReached").GetString());
        Assert.Equal("emitted-brep", root.GetProperty("actualStageReached").GetString());
        Assert.Equal("box", root.GetProperty("featureAir").GetProperty("sourceOpKind").GetString());
        Assert.Equal("CreateBox", root.GetProperty("featureAir").GetProperty("nodeKind").GetString());
        Assert.Equal("AirProfileExtrude", root.GetProperty("constructiveAir").GetProperty("nodeKind").GetString());
        Assert.Equal("rectangle-profile-extrude", root.GetProperty("constructiveAir").GetProperty("canonicalForm").GetString());
        Assert.True(root.TryGetProperty("profileEmission", out var profileEmission));
        Assert.True(profileEmission.GetProperty("wrapperInvoked").GetBoolean());
        Assert.Equal("LineArcProfileExtrudeEmitter", profileEmission.GetProperty("emitterName").GetString());
        Assert.True(profileEmission.GetProperty("succeeded").GetBoolean());
        Assert.Contains("air-x11-profile-extrude-wrapper-invoked", output);
    }


    [Fact]
    public void TraceParserBackedBoxFixture_ProfileEmissionUsesConstructiveDimensions()
    {
        var (exitCode, output, _) = Run("trace", "--fixture", PrimitiveFixture("valid/box.valid.firmfixture"), "--json");
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output);
        var dimensions = doc.RootElement.GetProperty("featureAir").GetProperty("dimensions");
        Assert.Equal(10, dimensions.GetProperty("width").GetDouble());
        Assert.Equal(8, dimensions.GetProperty("depth").GetDouble());
        Assert.Equal(6, dimensions.GetProperty("height").GetDouble());
        var constructive = doc.RootElement.GetProperty("constructiveAir");
        Assert.Equal(10, constructive.GetProperty("width").GetDouble());
        Assert.Equal(8, constructive.GetProperty("depth").GetDouble());
        Assert.Equal(6, constructive.GetProperty("height").GetDouble());
        Assert.Equal("Rectangle", constructive.GetProperty("profileKind").GetString());
        var emission = doc.RootElement.GetProperty("profileEmission");
        Assert.Equal(10, emission.GetProperty("width").GetDouble());
        Assert.Equal(8, emission.GetProperty("depth").GetDouble());
        Assert.Equal(6, emission.GetProperty("height").GetDouble());
        Assert.Contains("air-x11-profile-emission-summary-created", output);
    }

    [Fact]
    public void TraceParserBackedBoxFixture_DoesNotPretendBRepPlanOrCIR()
    {
        var (exitCode, output, _) = Run("trace", "--fixture", PrimitiveFixture("valid/box.valid.firmfixture"), "--json");
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output); var root = doc.RootElement;
        Assert.Equal("emitted-brep", root.GetProperty("actualStageReached").GetString());
        Assert.Equal("none", root.GetProperty("brepPlan").GetProperty("planKind").GetString());
        Assert.Equal("not-requested", root.GetProperty("cirMirror").GetProperty("status").GetString());
        Assert.Equal("AirProfileExtrude", root.GetProperty("constructiveAir").GetProperty("nodeKind").GetString());
        Assert.Contains("air-x11-brepplan-deferred", output);
        Assert.Contains("air-x11-cir-mirror-deferred", output);
    }

    [Fact]
    public void TraceParserBackedBoxFixture_StepSmokeTruthful()
    {
        var (exitCode, output, _) = Run("trace", "--fixture", PrimitiveFixture("valid/box.valid.firmfixture"), "--json");
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output);
        var stepSmoke = doc.RootElement.GetProperty("profileEmission").GetProperty("stepSmoke");
        Assert.False(stepSmoke.GetProperty("wasChecked").GetBoolean());
        Assert.False(stepSmoke.GetProperty("succeeded").GetBoolean());
        Assert.Contains("air-x11-step-smoke-unavailable", output);
    }

    [Fact]
    public void MetadataDrivenChamferFixtures_StillWork()
    {
        Assert.Equal(0, Run("trace", "--fixture", Fixture("valid/top-face-loop-chamfer.valid.firmfixture"), "--json").ExitCode);
        Assert.Equal(0, Run("trace", "--fixture", Fixture("invalid/arbitrary-graph-chamfer.invalid.firmfixture"), "--json").ExitCode);
        Assert.Equal(0, Run("trace", "--fixture", Fixture("invalid/non-uniform-loop-chamfer.invalid.firmfixture"), "--json").ExitCode);
        Assert.Equal(0, Run("trace", "--fixture", Fixture("invalid/loop-fillet-deferred.invalid.firmfixture"), "--json").ExitCode);
    }

    [Fact]
    public void ParserBackedBoxExpectationFailure_ReturnsNonZeroWithReport()
    {
        var source = File.ReadAllText(PrimitiveFixture("valid/box.valid.firmfixture")).Replace("// expected-stage: emitted-brep", "// expected-stage: cir-mirror", StringComparison.Ordinal);
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".valid.firmfixture");
        File.WriteAllText(path, source);
        var (exitCode, output, error) = Run("trace", "--fixture", path, "--json");
        Assert.NotEqual(0, exitCode); Assert.Contains("expectation", error, StringComparison.OrdinalIgnoreCase);
        using var doc = JsonDocument.Parse(output); var root = doc.RootElement;
        Assert.False(root.GetProperty("fixture").GetProperty("expectationSatisfied").GetBoolean());
        Assert.True(root.GetProperty("frontend").GetProperty("parseSucceeded").GetBoolean());
        Assert.Equal("emitted-brep", root.GetProperty("actualStageReached").GetString());
        Assert.Contains("air-x10-firmament-parse-succeeded", output);
    }

    [Fact]
    public void ParserBackedBoxJsonDeterministic()
    {
        var valid = PrimitiveFixture("valid/box.valid.firmfixture");
        Assert.Equal(Run("trace", "--fixture", valid, "--json").Stdout, Run("trace", "--fixture", valid, "--json").Stdout);
    }

    [Fact]
    public void TraceFixtureValidTopFaceLoopChamfer_DefaultsToText()
    {
        var (exitCode, output, error) = Run("trace", "--fixture", Fixture("valid/top-face-loop-chamfer.valid.firmfixture"));
        Assert.Equal(0, exitCode); Assert.True(string.IsNullOrWhiteSpace(error));
        Assert.False(output.TrimStart().StartsWith("{", StringComparison.Ordinal));
        Assert.Contains("Fixture", output); Assert.Contains("Expectation: valid", output);
        Assert.Contains("Actual stage: cir-mirror", output); Assert.Contains("Expectation satisfied: true", output);
        Assert.Contains("TopFaceLoopChamfer", output); Assert.Contains("FaceBoundaryLoop", output);
        Assert.Contains("UniformChamfer", output); Assert.Contains("Chamfer faces: 4", output);
    }

    [Fact]
    public void TraceFixtureValidTopFaceLoopChamfer_JsonParses()
    {
        var (exitCode, output, _) = Run("trace", "--fixture", Fixture("valid/top-face-loop-chamfer.valid.firmfixture"), "--json");
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output); var root = doc.RootElement;
        Assert.Equal("firmfixture", root.GetProperty("inputKind").GetString());
        Assert.Equal("valid", root.GetProperty("fixtureExpectation").GetString());
        Assert.True(root.GetProperty("expectationSatisfied").GetBoolean());
        Assert.Equal("top-face-loop-chamfer", root.GetProperty("fixtureCaseName").GetString());
        Assert.True(root.TryGetProperty("air", out _)); Assert.True(root.TryGetProperty("routeDecision", out _));
        Assert.True(root.TryGetProperty("brepPlan", out _)); Assert.True(root.TryGetProperty("cirMirror", out _));
    }

    [Theory]
    [InlineData("invalid/arbitrary-graph-chamfer.invalid.firmfixture", "arbitrary-graph-unsupported")]
    [InlineData("invalid/non-uniform-loop-chamfer.invalid.firmfixture", "non-uniform-rule-unsupported")]
    [InlineData("invalid/loop-fillet-deferred.invalid.firmfixture", "loop-fillet-deferred")]
    public void TraceFixtureInvalid_StopsBeforeGeometry(string path, string reason)
    {
        var (exitCode, output, error) = Run("trace", "--fixture", Fixture(path));
        Assert.Equal(0, exitCode); Assert.True(string.IsNullOrWhiteSpace(error));
        Assert.Contains("Expectation: invalid", output); Assert.Contains("Expectation satisfied: true", output);
        Assert.Contains(reason, output); Assert.Contains("Faces: 0", output);
        Assert.Contains("STEP smoke: unavailable", output); Assert.Contains("Status: not-requested", output);
    }

    [Fact]
    public void TraceFixtureUsageErrors_AreHelpful()
    {
        var both = Run("trace", "--case", "top-face-loop-chamfer", "--fixture", Fixture("valid/top-face-loop-chamfer.valid.firmfixture"));
        Assert.NotEqual(0, both.ExitCode); Assert.Contains("mutually exclusive", both.Stderr);
        var wrongPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt"); File.WriteAllText(wrongPath, "// case: nope");
        var wrong = Run("trace", "--fixture", wrongPath); Assert.NotEqual(0, wrong.ExitCode); Assert.Contains(".valid.firmfixture", wrong.Stderr);
        var missing = Run("trace", "--fixture", "fixtures/Firmament/Chamfer/missing.valid.firmfixture"); Assert.NotEqual(0, missing.ExitCode); Assert.Contains("not found", missing.Stderr);
        var unknownPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".valid.firmfixture"); File.WriteAllText(unknownPath, "// case: nope\n// expected: valid\n");
        var unknown = Run("trace", "--fixture", unknownPath); Assert.NotEqual(0, unknown.ExitCode); Assert.Contains("Supported fixture cases", unknown.Stderr);
    }

    [Fact]
    public void TraceFixtureOutput_IsDeterministic()
    {
        var valid = Fixture("valid/top-face-loop-chamfer.valid.firmfixture");
        var invalid = Fixture("invalid/arbitrary-graph-chamfer.invalid.firmfixture");
        Assert.Equal(Run("trace", "--fixture", valid, "--json").Stdout, Run("trace", "--fixture", valid, "--json").Stdout);
        Assert.Equal(Run("trace", "--fixture", invalid, "--json").Stdout, Run("trace", "--fixture", invalid, "--json").Stdout);
    }


    [Fact]
    public void TraceParserBackedBoxFixture_IncludesRootRegion()
    {
        var (exitCode, output, _) = Run("trace", "--fixture", PrimitiveFixture("valid/box.valid.firmfixture"), "--json");
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output); var regions = doc.RootElement.GetProperty("regions");
        Assert.Equal(1, regions.GetProperty("regionCount").GetInt32());
        Assert.False(regions.GetProperty("hasNestedRegions").GetBoolean());
        var rootRegion = regions.GetProperty("regions")[0];
        Assert.Equal("RootRegion", rootRegion.GetProperty("regionKind").GetString());
        Assert.Equal("WorldRoot", rootRegion.GetProperty("localFrame").GetProperty("frameKind").GetString());
        Assert.Equal("PureConstruction", rootRegion.GetProperty("effectKind").GetString());
        Assert.Equal("NotRequired", rootRegion.GetProperty("integrationStatus").GetString());
    }

    [Fact]
    public void TraceSideHoleRegion_BRepBoundary_DefaultText()
    {
        var (exitCode, output, error) = Run("trace", "--fixture", RegionFixture("valid/side-hole-face-attached-region.valid.firmfixture"));
        Assert.Equal(0, exitCode); Assert.True(string.IsNullOrWhiteSpace(error));
        Assert.Contains("Regions", output); Assert.Contains("FaceAttachedRegion", output);
        Assert.Contains("Subtractive", output); Assert.Contains("YieldSubtractiveVolume", output);
        Assert.Contains("YieldsCutVolume", output); Assert.Contains("Integration: Deferred", output);
        Assert.Contains("Region yield", output); Assert.Contains("SideHole", output); Assert.Contains("Circle", output);
        Assert.Contains("radius=1", output); Assert.Contains("+X", output); Assert.Contains("through inward", output); Assert.Contains("ThroughCut", output);
        Assert.Contains("Region CIR mirror", output); Assert.Contains("mirror-admitted-conservative", output); Assert.Contains("cir-region-parent-minus-cylinder", output);
        Assert.Contains("Parent field: Box", output); Assert.Contains("Subtract field: Cylinder", output);
        Assert.Contains("no topology authority", output); Assert.Contains("no face identity", output);
        Assert.Contains("no Boolean", output); Assert.Contains("no BRep emission", output);

        Assert.Contains("Region BRepPlan boundary", output); Assert.Contains("PlannedContractOnly", output);
        Assert.Contains("Affected face: +X", output); Assert.Contains("circular entry loop intent", output);
        Assert.Contains("opposite-side exit deferred", output); Assert.Contains("cylindrical cut wall intent deferred", output);
        Assert.Contains("CutEntryLoop", output); Assert.Contains("CutWallFace", output);
        Assert.Contains("no parent topology mutation", output); Assert.Contains("no BRepPlan elements materialized", output);
        Assert.Contains("Region integration decision", output);
        Assert.Contains("Selected: ControlledSideHolePatchMaterialization", output);
        Assert.Contains("Status: Deferred", output);
        Assert.Contains("FaceAttachedConstructiveInsertion: Deferred", output);
        Assert.Contains("LocalBRepPlanPatch: Deferred", output);
        Assert.Contains("BRepBooleanFallback: Rejected", output);
        Assert.Contains("CirAnalysisMirrorOnly: AvailableForAnalysis", output);
        Assert.Contains("no BRep emission", output);
    }

    [Fact]
    public void TraceSideHoleRegion_BRepBoundary_Json()
    {
        var (exitCode, output, _) = Run("trace", "--fixture", RegionFixture("valid/side-hole-face-attached-region.valid.firmfixture"), "--json");
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output); var root = doc.RootElement;
        Assert.Equal("valid", root.GetProperty("fixture").GetProperty("expectation").GetString());
        Assert.True(root.GetProperty("fixture").GetProperty("expectationSatisfied").GetBoolean());
        Assert.Equal("region-materialization-partial", root.GetProperty("actualStageReached").GetString());
        var regions = root.GetProperty("regions");
        Assert.Equal(2, regions.GetProperty("regionCount").GetInt32());
        Assert.True(regions.GetProperty("hasNestedRegions").GetBoolean());
        var side = regions.GetProperty("regions").EnumerateArray().Single(r => r.GetProperty("regionKind").GetString() == "FaceAttachedRegion");
        Assert.Equal("region:root", side.GetProperty("parentRegionId").GetString());
        Assert.Equal("Subtractive", side.GetProperty("effectKind").GetString());
        Assert.Equal("YieldSubtractiveVolume", side.GetProperty("yieldKind").GetString());
        Assert.Equal("YieldsCutVolume", side.GetProperty("boundaryContractKind").GetString());
        Assert.Equal("Deferred", side.GetProperty("integrationStatus").GetString());
        var yield = side.GetProperty("yield");
        Assert.Equal("SideHole", yield.GetProperty("featureKind").GetString());
        Assert.Equal("YieldSubtractiveVolume", yield.GetProperty("yieldKind").GetString());
        Assert.Equal("Subtractive", yield.GetProperty("effectKind").GetString());
        Assert.Equal("+X", yield.GetProperty("attachment").GetProperty("faceSelector").GetString());
        Assert.Equal("Circle", yield.GetProperty("profile").GetProperty("profileKind").GetString());
        Assert.Equal(1, yield.GetProperty("profile").GetProperty("radius").GetDouble());
        Assert.Equal("FaceNormal", yield.GetProperty("direction").GetProperty("directionKind").GetString());
        Assert.Equal("Inward", yield.GetProperty("direction").GetProperty("sense").GetString());
        Assert.True(yield.GetProperty("direction").GetProperty("isThrough").GetBoolean());
        Assert.Equal("ThroughCut", yield.GetProperty("boundaryIntent").GetProperty("boundaryKind").GetString());
        Assert.True(yield.GetProperty("affectedScope").GetProperty("parentBodyOnly").GetBoolean());
        Assert.True(yield.GetProperty("affectedScope").GetProperty("escapesOnlyThroughYield").GetBoolean());
        Assert.Equal("Deferred", yield.GetProperty("integrationStatus").GetString());
        var mirror = side.GetProperty("cirMirror");
        Assert.Equal("SideHole", mirror.GetProperty("yieldFeatureKind").GetString());
        Assert.Equal("mirror-admitted-conservative", mirror.GetProperty("status").GetString());
        Assert.Equal("cir-region-parent-minus-cylinder", mirror.GetProperty("backend").GetString());
        Assert.Equal("Subtractive", mirror.GetProperty("effect").GetString());
        Assert.Equal("Box", mirror.GetProperty("parentField").GetString());
        Assert.Equal("Cylinder", mirror.GetProperty("subtractField").GetString());
        var capabilities = mirror.GetProperty("capabilities").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains("occupancy", capabilities); Assert.Contains("containment", capabilities); Assert.Contains("bounds", capabilities);
        var losses = mirror.GetProperty("knownLosses").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains("no-topology-authority", losses); Assert.Contains("no-face-identity", losses);
        Assert.Contains("no-brep-plan-role-parity", losses); Assert.Contains("no-step-export-authority", losses);

        var brepBoundary = side.GetProperty("brepBoundary");
        Assert.Equal("PlannedContractOnly", brepBoundary.GetProperty("status").GetString());
        Assert.Equal("+X", brepBoundary.GetProperty("affectedParent").GetProperty("affectedFaceSelector").GetString());
        Assert.Equal("ParentBodyLocalFeature", brepBoundary.GetProperty("affectedParent").GetProperty("affectedScope").GetString());
        Assert.Equal("CircularEntry", brepBoundary.GetProperty("entryBoundary").GetProperty("boundaryKind").GetString());
        Assert.Equal("CircularEntryLoop", brepBoundary.GetProperty("entryBoundary").GetProperty("loopIntent").GetString());
        Assert.Equal("OppositeSideExit", brepBoundary.GetProperty("exitBoundary").GetProperty("exitKind").GetString());
        Assert.Equal("Deferred", brepBoundary.GetProperty("exitBoundary").GetProperty("status").GetString());
        Assert.Equal("CylindricalCutWallIntent", brepBoundary.GetProperty("cutWallIntent").GetProperty("wallKind").GetString());
        Assert.Equal("Deferred", brepBoundary.GetProperty("cutWallIntent").GetProperty("status").GetString());
        var plannedRoles = brepBoundary.GetProperty("plannedRoles").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains("CutEntryLoop", plannedRoles); Assert.Contains("CutWallFace", plannedRoles);
        var boundaryLosses = brepBoundary.GetProperty("knownLosses").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains("no-emitted-entry-loop-identity", boundaryLosses);
        Assert.Contains("no-emitted-exit-loop-identity", boundaryLosses);
        Assert.Contains("no-emitted-cut-wall-face-identity", boundaryLosses);
        Assert.Contains("no-brep-plan-element-materialization", boundaryLosses);
        Assert.Equal("Deferred", brepBoundary.GetProperty("integrationStatus").GetString());
        var decision = side.GetProperty("integrationDecision");
        Assert.Equal("ControlledSideHolePatchMaterialization", decision.GetProperty("selectedRouteKind").GetString());
        Assert.Equal("Deferred", decision.GetProperty("selectedStatus").GetString());
        Assert.Equal("SwitchMatch", decision.GetProperty("selectionMode").GetString());
        var candidates = decision.GetProperty("candidates").EnumerateArray().ToDictionary(c => c.GetProperty("routeKind").GetString()!, c => c);
        Assert.Equal("Deferred", candidates["FaceAttachedConstructiveInsertion"].GetProperty("status").GetString());
        Assert.Equal("side-hole-constructive-insertion-not-implemented", candidates["FaceAttachedConstructiveInsertion"].GetProperty("reasonCode").GetString());
        Assert.Equal("Deferred", candidates["LocalBRepPlanPatch"].GetProperty("status").GetString());
        Assert.Equal("local-brep-plan-patch-not-implemented", candidates["LocalBRepPlanPatch"].GetProperty("reasonCode").GetString());
        Assert.Equal("Rejected", candidates["BRepBooleanFallback"].GetProperty("status").GetString());
        Assert.Equal("boolean-fallback-not-admitted", candidates["BRepBooleanFallback"].GetProperty("reasonCode").GetString());
        Assert.Equal("AvailableForAnalysis", candidates["CirAnalysisMirrorOnly"].GetProperty("status").GetString());
        Assert.Equal("cir-mirror-analysis-only", candidates["CirAnalysisMirrorOnly"].GetProperty("reasonCode").GetString());
        Assert.Equal("Selected", candidates["DeferredIntegration"].GetProperty("status").GetString());
        Assert.Equal("no-topology-integration-route-admitted", candidates["DeferredIntegration"].GetProperty("reasonCode").GetString());
        Assert.True(root.GetProperty("emission").GetProperty("succeeded").GetBoolean());
        Assert.False(root.GetProperty("stepSmoke").GetProperty("succeeded").GetBoolean());
        Assert.Equal("none", root.GetProperty("brepPlan").GetProperty("planKind").GetString());
    }


    [Fact]
    public void TraceSideHoleRegion_BRepPlaceholders_DefaultText()
    {
        var (exitCode, output, error) = Run("trace", "--fixture", RegionFixture("valid/side-hole-face-attached-region.valid.firmfixture"));
        Assert.Equal(0, exitCode); Assert.True(string.IsNullOrWhiteSpace(error));
        Assert.Contains("Region BRepPlan placeholders", output);
        Assert.Contains("Status: PlaceholderOnly", output);
        Assert.Contains("Elements: 5", output);
        Assert.Contains("Materialized: 0", output);
        Assert.Contains("Region materialization", output);
        Assert.Contains("ControlledSideHolePatchMaterialization", output);
        Assert.Contains("CutWallFace -> Materialized", output);
        Assert.Contains("Cylindrical faces: 1", output);
        Assert.Contains("no general side-hole support", output);
        Assert.Contains("region:side-hole:+x:entry-loop", output);
        Assert.Contains("CutEntryLoop", output);
        Assert.Contains("CutExitLoop", output);
        Assert.Contains("CutWallFace", output);
        Assert.Contains("AffectedParentFace", output);
        Assert.Contains("RegionIntegrationPatch", output);
        Assert.Contains("no parent topology mutation", output);
        Assert.Contains("no BRep emission", output);
        Assert.Contains("no Boolean", output);
    }

    [Fact]
    public void TraceSideHoleRegion_BRepPlaceholders_Json()
    {
        var (exitCode, output, _) = Run("trace", "--fixture", RegionFixture("valid/side-hole-face-attached-region.valid.firmfixture"), "--json");
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output); var root = doc.RootElement;
        var side = root.GetProperty("regions").GetProperty("regions").EnumerateArray().Single(r => r.GetProperty("regionKind").GetString() == "FaceAttachedRegion");
        var placeholders = side.GetProperty("brepPlaceholders");
        Assert.Equal("PlaceholderOnly", placeholders.GetProperty("placeholderStatus").GetString());
        var summary = placeholders.GetProperty("summary");
        Assert.Equal(5, summary.GetProperty("placeholderElementCount").GetInt32());
        Assert.Equal(0, summary.GetProperty("materializedElementCount").GetInt32());
        Assert.Equal(5, summary.GetProperty("notMaterializedElementCount").GetInt32());
        var elements = placeholders.GetProperty("elements").EnumerateArray().ToArray();
        Assert.Equal(5, elements.Length);
        var ids = elements.Select(e => e.GetProperty("id").GetString()).ToArray();
        Assert.Contains("region:side-hole:+x:parent-face:+x", ids);
        Assert.Contains("region:side-hole:+x:entry-loop", ids);
        Assert.Contains("region:side-hole:+x:exit-loop", ids);
        Assert.Contains("region:side-hole:+x:cut-wall", ids);
        Assert.Contains("region:side-hole:+x:integration-patch", ids);
        var roles = elements.Select(e => e.GetProperty("role").GetString()).ToArray();
        Assert.Contains("CutEntryLoop", roles); Assert.Contains("CutExitLoop", roles); Assert.Contains("CutWallFace", roles);
        Assert.Contains("AffectedParentFace", roles); Assert.Contains("RegionIntegrationPatch", roles);
        Assert.All(elements, e => Assert.True(e.GetProperty("materializationStatus").GetString() is "NotMaterialized" or "ReferenceOnly"));
        Assert.Equal("ControlledSideHolePatchMaterialization", side.GetProperty("integrationDecision").GetProperty("selectedRouteKind").GetString());
        Assert.Equal("Rejected", side.GetProperty("integrationDecision").GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("routeKind").GetString() == "BRepBooleanFallback").GetProperty("status").GetString());
        Assert.True(root.GetProperty("emission").GetProperty("succeeded").GetBoolean());
        Assert.False(root.GetProperty("stepSmoke").GetProperty("succeeded").GetBoolean());
        var materialization = side.GetProperty("materialization");
        Assert.Equal("PartiallyMaterialized", materialization.GetProperty("status").GetString());
        Assert.Equal("ControlledSideHolePatchMaterialization", materialization.GetProperty("route").GetString());
        Assert.Equal(1, materialization.GetProperty("topologySummary").GetProperty("cylindricalFaceCount").GetInt32());
        Assert.Contains(materialization.GetProperty("placeholderMappings").EnumerateArray(), m => m.GetProperty("placeholderRole").GetString() == "CutWallFace" && m.GetProperty("materializationStatus").GetString() == "Materialized");
    }

    [Fact]
    public void TraceSideHoleRegion_BRepPlaceholders_StableIds()
    {
        var first = Run("trace", "--fixture", RegionFixture("valid/side-hole-face-attached-region.valid.firmfixture"), "--json").Stdout;
        var second = Run("trace", "--fixture", RegionFixture("valid/side-hole-face-attached-region.valid.firmfixture"), "--json").Stdout;
        using var d1 = JsonDocument.Parse(first); using var d2 = JsonDocument.Parse(second);
        static string[] P(JsonDocument d) => d.RootElement.GetProperty("regions").GetProperty("regions").EnumerateArray().Single(r => r.GetProperty("regionKind").GetString() == "FaceAttachedRegion").GetProperty("brepPlaceholders").GetProperty("elements").EnumerateArray().Select(e => e.GetProperty("id").GetString() + ":" + e.GetProperty("role").GetString()).ToArray();
        Assert.Equal(P(d1), P(d2));
    }

    [Fact]
    public void TraceImplicitParentMutationRegion_RejectedWithYieldContractReason()
    {
        var (exitCode, output, _) = Run("trace", "--fixture", RegionFixture("invalid/implicit-parent-mutation.invalid.firmfixture"), "--json");
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output); var root = doc.RootElement;
        Assert.Equal("invalid", root.GetProperty("fixture").GetProperty("expectation").GetString());
        Assert.True(root.GetProperty("fixture").GetProperty("expectationSatisfied").GetBoolean());
        Assert.Equal("region-rejected", root.GetProperty("actualStageReached").GetString());
        Assert.Contains("implicit parent mutation rejected", root.GetProperty("recommendation").GetString());
        var diagnostics = root.GetProperty("diagnostics").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains("air-region-x2-implicit-parent-mutation-rejected", diagnostics);
        Assert.Contains("air-region-x2-missing-explicit-yield-rejected", diagnostics);
        Assert.Contains("air-region-x2-boundary-contract-required", diagnostics);
        Assert.False(root.GetProperty("emission").GetProperty("succeeded").GetBoolean());
        var side = root.GetProperty("regions").GetProperty("regions").EnumerateArray().Single(r => r.GetProperty("regionKind").GetString() == "FaceAttachedRegion");
        Assert.False(side.TryGetProperty("brepBoundary", out _));
        Assert.False(side.TryGetProperty("integrationDecision", out _));
        Assert.False(side.TryGetProperty("brepPlaceholders", out _));
    }

    [Fact]
    public void RegionIntegrationDecisionSummaries_AreDeterministic()
    {
        var valid = RegionFixture("valid/side-hole-face-attached-region.valid.firmfixture");
        var invalid = RegionFixture("invalid/implicit-parent-mutation.invalid.firmfixture");
        Assert.Equal(Run("trace", "--fixture", valid, "--json").Stdout, Run("trace", "--fixture", valid, "--json").Stdout);
        Assert.Equal(Run("trace", "--fixture", invalid, "--json").Stdout, Run("trace", "--fixture", invalid, "--json").Stdout);
    }

    [Fact]
    public void TraceSideHoleRegion_EnforcesLocality_NoParentBRepOrBoolean()
    {
        var (exitCode, output, _) = Run("trace", "--fixture", RegionFixture("valid/side-hole-face-attached-region.valid.firmfixture"), "--json");
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output); var root = doc.RootElement;
        Assert.True(root.GetProperty("emission").GetProperty("succeeded").GetBoolean());
        Assert.False(root.GetProperty("stepSmoke").GetProperty("succeeded").GetBoolean());
        Assert.Equal("none", root.GetProperty("brepPlan").GetProperty("planKind").GetString());
        var side = root.GetProperty("regions").GetProperty("regions").EnumerateArray().Single(r => r.GetProperty("regionKind").GetString() == "FaceAttachedRegion");
        var mirror = side.GetProperty("cirMirror");
        var capabilities = mirror.GetProperty("capabilities").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.DoesNotContain("topology-authority", capabilities);
        Assert.DoesNotContain("face-identity", capabilities);
        Assert.DoesNotContain("entry-loop-identity", capabilities);
        Assert.DoesNotContain("boundary-patch-identity", capabilities);
        var losses = mirror.GetProperty("knownLosses").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains("no-topology-authority", losses);
        Assert.Contains("no-face-identity", losses);
        var guarantees = root.GetProperty("guarantees").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains("no Boolean", guarantees);
        Assert.Contains("escapes only through explicit yield", guarantees);
        Assert.Contains("no BRep emission", guarantees);
    }

    [Fact]
    public void TraceParserBackedBox_RootRegionStillWorks()
    {
        var (exitCode, output, _) = Run("trace", "--fixture", PrimitiveFixture("valid/box.valid.firmfixture"), "--json");
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output); var root = doc.RootElement;
        Assert.Equal("emitted-brep", root.GetProperty("actualStageReached").GetString());
        var regions = root.GetProperty("regions");
        Assert.Equal(1, regions.GetProperty("regionCount").GetInt32());
        Assert.DoesNotContain("SideHole", output, StringComparison.Ordinal);
        Assert.DoesNotContain("brepBoundary", output, StringComparison.Ordinal);
        Assert.DoesNotContain("integrationDecision", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingMetadataDrivenChamferFixtures_StillWork()
    {
        Assert.Equal(0, Run("trace", "--fixture", Fixture("valid/top-face-loop-chamfer.valid.firmfixture"), "--json").ExitCode);
        Assert.Equal(0, Run("trace", "--fixture", Fixture("invalid/arbitrary-graph-chamfer.invalid.firmfixture"), "--json").ExitCode);
        Assert.Equal(0, Run("trace", "--fixture", Fixture("invalid/non-uniform-loop-chamfer.invalid.firmfixture"), "--json").ExitCode);
        Assert.Equal(0, Run("trace", "--fixture", Fixture("invalid/loop-fillet-deferred.invalid.firmfixture"), "--json").ExitCode);
    }

    private static string Fixture(string relative) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/Firmament/Chamfer", relative));
    private static string PrimitiveFixture(string relative) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/Firmament/Primitive", relative));
    private static string RegionFixture(string relative) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/Firmament/Region", relative));

    private static (int ExitCode, string Stdout, string Stderr) Run(params string[] args)
    {
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(args, stdout, stderr);
        return (exitCode, stdout.ToString(), stderr.ToString());
    }
}
