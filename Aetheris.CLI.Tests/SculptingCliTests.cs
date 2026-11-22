using System.Text.Json;
using Aetheris.CLI;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Surfacing;

namespace Aetheris.CLI.Tests;

public sealed class SculptingCliTests
{
    [Theory]
    [InlineData("surf-x3b-add-section-chain-grip")]
    [InlineData("surf-x4-remove-section-chain-g1-duct")]
    public void EmbeddedStationChainsStayWithSculptingOwner(string name)
    {
        var fixture = Path.Combine(Root(), "fixtures/Canonical/BodyState", name + ".firmament");
        var directory = Path.Combine(Root(), "artifacts/local/tests/sculpting-owner", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var output = Path.Combine(directory, "part.step");
        var mismatched = Path.Combine(directory, "mismatched.firmament");
        try
        {
            foreach (var operation in new[] { "validate", "inspect", "build" })
            {
                var stdout = new StringWriter(); var stderr = new StringWriter();
                string[] args = operation == "build" ? [operation, fixture, "--output", output, "--json"] : [operation, fixture, "--json"];
                Assert.Equal(0, CliRunner.Run(args, stdout, stderr));
                using var json = JsonDocument.Parse(stdout.ToString());
                var result = operation == "validate" ? json.RootElement.GetProperty("firmamentV2Validation") : json.RootElement;
                Assert.Equal("Sculpting", result.GetProperty("domain").GetString());
            }
            Assert.True(File.Exists(output));
            File.WriteAllText(mismatched, "schema SectionChain\n" + File.ReadAllText(fixture));
            var rejected = Path.Combine(directory, "rejected.step");
            foreach (var operation in new[] { "validate", "build" })
            {
                var stdout = new StringWriter();
                string[] args = operation == "build" ? [operation, mismatched, "--output", rejected, "--json"] : [operation, mismatched, "--json"];
                Assert.NotEqual(0, CliRunner.Run(args, stdout, new StringWriter()));
                Assert.Contains("firmament-schema-mismatch", stdout.ToString());
                Assert.False(File.Exists(rejected));
            }
        }
        finally
        {
            foreach (var path in Directory.GetFiles(directory)) File.Delete(path);
            Directory.Delete(directory);
        }
    }

    [Fact]
    public void BuildInspectAndValidateExposeStructuredSculptStateEvidence()
    {
        var fixture = Path.Combine(Root(), "fixtures", "Canonical", "Sculpting", "sculpted-housing.firmament");
        var output = Path.Combine(Path.GetTempPath(), "aetheris-surf-x0-" + Guid.NewGuid().ToString("N") + ".step");
        try
        {
            var buildOut = new StringWriter(); var buildErr = new StringWriter();
            Assert.Equal(0, CliRunner.Run(["build", fixture, "--output", output, "--json"], buildOut, buildErr));
            using var build = JsonDocument.Parse(buildOut.ToString()); Assert.True(build.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal("Sculpting", build.RootElement.GetProperty("domain").GetString()); Assert.Equal(0, build.RootElement.GetProperty("rationalNurbs").GetInt32());
            Assert.Equal("state-8960030e57e7b7d897d9", build.RootElement.GetProperty("geometricDelta").GetProperty("inputState").GetProperty("value").GetString());
            Assert.True(File.Exists(output)); Assert.True(File.Exists(Path.ChangeExtension(output, ".delta.json")));
            var inspectOut = new StringWriter(); Assert.Equal(0, CliRunner.Run(["inspect", fixture, "--json"], inspectOut, new StringWriter()));
            using var inspect = JsonDocument.Parse(inspectOut.ToString()); Assert.Equal(2, inspect.RootElement.GetProperty("states").GetArrayLength());
            var validateOut = new StringWriter(); Assert.Equal(0, CliRunner.Run(["validate", fixture, "--json"], validateOut, new StringWriter()));
            Assert.Contains("\"domain\": \"Sculpting\"", validateOut.ToString(), StringComparison.Ordinal);
        }
        finally { if (File.Exists(output)) File.Delete(output); var delta = Path.ChangeExtension(output, ".delta.json"); if (File.Exists(delta)) File.Delete(delta); }
    }

    [Fact]
    public void PatchReplacementBuildAndReinspectionExposeNonRationalSurfaceAndInteriorBounds()
    {
        var fixture = Path.Combine(Root(), "fixtures", "Canonical", "Sculpting", "surf-x1-freeform-housing.firmament");
        var output = Path.Combine(Path.GetTempPath(), "aetheris-surf-x1-" + Guid.NewGuid().ToString("N") + ".step");
        try
        {
            var buildOut = new StringWriter(); Assert.Equal(0, CliRunner.Run(["build", fixture, "--output", output, "--json"], buildOut, new StringWriter()));
            using var build = JsonDocument.Parse(buildOut.ToString()); var root = build.RootElement;
            Assert.Equal(1, root.GetProperty("surfaceInventory").GetProperty("nonRationalBSpline").GetInt32()); Assert.Equal(0, root.GetProperty("rationalNurbs").GetInt32());
            var patch = root.GetProperty("surfacePatches")[0]; Assert.Equal("CrownPatch", patch.GetProperty("patchId").GetString()); Assert.Equal("NonRationalBSpline", patch.GetProperty("exportClass").GetString());
            Assert.Equal(6, patch.GetProperty("controlCountU").GetInt32()); Assert.Equal(4, patch.GetProperty("continuityContracts").GetArrayLength());
            var analyzeOut = new StringWriter(); Assert.Equal(0, CliRunner.Run(["analyze", output, "--json"], analyzeOut, new StringWriter()));
            using var analyze = JsonDocument.Parse(analyzeOut.ToString()); var summary = analyze.RootElement.GetProperty("summary");
            Assert.Equal("enclosed-manifold", summary.GetProperty("structuralAssessment").GetString()); Assert.Equal(1, summary.GetProperty("surfaceFamilies").GetProperty("bspline").GetInt32());
            Assert.InRange(summary.GetProperty("boundingBox").GetProperty("max").GetProperty("z").GetDouble(), 26.269, 26.270);
        }
        finally { if (File.Exists(output)) File.Delete(output); var delta = Path.ChangeExtension(output, ".delta.json"); if (File.Exists(delta)) File.Delete(delta); }
    }

    [Fact]
    public void JudgedBlendBuildExposesCompactCandidateTraceAndProvenance()
    {
        var fixture = Path.Combine(Root(), "fixtures", "Canonical", "Sculpting", "surf-x2-judged-housing.firmament");
        var output = Path.Combine(Path.GetTempPath(), "aetheris-surf-x2-" + Guid.NewGuid().ToString("N") + ".step");
        try
        {
            var buildOut = new StringWriter(); var buildErr = new StringWriter();
            Assert.Equal(0, CliRunner.Run(["build", fixture, "--output", output, "--json"], buildOut, buildErr));
            using var build = JsonDocument.Parse(buildOut.ToString()); var root = build.RootElement;
            var judgment = root.GetProperty("blendJudgment");
            Assert.Equal("PowerM3Degree6", judgment.GetProperty("selectedCandidateId").GetString());
            Assert.Equal(4, judgment.GetProperty("candidates").GetArrayLength());
            Assert.Contains(judgment.GetProperty("candidates").EnumerateArray(), item => item.GetProperty("disposition").GetString() == "Rejected");
            Assert.Equal(0, root.GetProperty("rationalNurbs").GetInt32());
            using var delta = JsonDocument.Parse(File.ReadAllText(Path.ChangeExtension(output, ".delta.json")));
            Assert.Equal("PowerM3Degree6", delta.RootElement.GetProperty("delta").GetProperty("blendJudgment").GetProperty("selectedCandidateId").GetString());
        }
        finally { if (File.Exists(output)) File.Delete(output); var delta = Path.ChangeExtension(output, ".delta.json"); if (File.Exists(delta)) File.Delete(delta); }
    }

    [Trait("Category", "Slow")]
    [Fact]
    public void SectionChainCageFitsTheExactBodyAndReplaysTwoEditsWithTheSameRim()
    {
        var source = Path.Combine(Root(), "fixtures/Canonical/Sculpting/compact-mouse.firmament");
        var folder = Path.Combine(Root(), "artifacts/local/tests/section-chain-cage", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using var inspection = Run("inspect", source);
            Assert.Equal("Sculpting", inspection.RootElement.GetProperty("domain").GetString());
            var joins = inspection.RootElement.GetProperty("states")[0].GetProperty("state").GetProperty("endCapJoins");
            Assert.Equal(4, joins.GetArrayLength());
            Assert.All(joins.EnumerateArray(), join =>
            {
                Assert.Equal("G2WithinSampledTolerance", join.GetProperty("status").GetString());
                Assert.InRange(join.GetProperty("maximumNormalAngleDegrees").GetDouble(), 0, 1e-3);
                Assert.InRange(join.GetProperty("maximumShapeOperatorResidual").GetDouble(), 0, 1e-6);
            });
            var states = inspection.RootElement.GetProperty("meshStates");
            var seed = states.GetProperty("Seed");
            var anchor = Assert.Single(seed.GetProperty("surfaceAnchors").EnumerateArray(),
                candidate => candidate.GetProperty("name").GetString() == "Nose");
            Assert.Equal("Shoulder", anchor.GetProperty("section").GetString());
            var brush = states.GetProperty("LowFront").GetProperty("brushEffect");
            Assert.Equal("NoseDip", brush.GetProperty("feature").GetString());
            Assert.Equal("Nose", brush.GetProperty("anchor").GetString());
            Assert.Equal(-0.6, brush.GetProperty("requestedOffsetMm").GetProperty("z").GetDouble());
            Assert.InRange(brush.GetProperty("anchorDisplacementMm").GetProperty("z").GetDouble(), -0.6, -0.5);
            Assert.InRange(brush.GetProperty("maximumSurfaceDisplacementMm").GetDouble(), 0.5, 0.6);
            Assert.Equal(128, brush.GetProperty("rimSampleCount").GetInt32());
            Assert.Equal(0, brush.GetProperty("maximumRimMovementMm").GetDouble());
            var fit = seed.GetProperty("sectionFit");
            Assert.True(fit.GetProperty("maximumSampledTransitionDeviationMm").GetDouble() <
                fit.GetProperty("initialMaximumSampledTransitionDeviationMm").GetDouble());

            var stepPath = Path.Combine(folder, "blockout.step");
            using var blockout = Run("build", source, "--state", "Exact", "--out", stepPath);
            var imported = Step242Importer.ImportBody(File.ReadAllText(stepPath));
            Assert.True(imported.IsSuccess);
            Assert.Equal(22, imported.Value.Topology.Faces.Count());
            Assert.Equal(0, blockout.RootElement.GetProperty("surfaceInventory").GetProperty("rationalNurbs").GetInt32());
            var sourceStateId = blockout.RootElement.GetProperty("state").GetProperty("stateId").GetProperty("value").GetString();

            var raised = ExportAndReplay("PalmComfort");
            var lowered = ExportAndReplay("LowFront");
            Assert.NotEqual(raised.StateId, lowered.StateId);
            var operation = Assert.IsType<DisplaceCageBrush>(Assert.Single(lowered.Construction.Operations));
            Assert.Equal("Nose", operation.Anchor);
            Assert.Equal(brush.GetProperty("anchorDisplacementMm").GetProperty("z").GetDouble(), lowered.BrushEffect!.AnchorDisplacementMm.Z);
            Assert.Equal(raised.SectionFit!.Rim, lowered.SectionFit!.Rim);
            Assert.InRange(raised.SectionFit.Rim.MaximumSampledDeviationMm, 0, 0.15);
            Assert.Equal(raised.Cage.Faces.Select(f => f.Id), lowered.Cage.Faces.Select(f => f.Id));

            var raisedMesh = SubdivisionSculptor.PrepareExport(raised).NativeMesh;
            var loweredMesh = SubdivisionSculptor.PrepareExport(lowered).NativeMesh;
            Assert.True(raisedMesh.Positions.Count > raised.Cage.Vertices.Count);
            Assert.Equal(raisedMesh.Positions.Where(p => p.Z <= 12.000001),
                loweredMesh.Positions.Where(p => p.Z <= 12.000001));
            Assert.True(raisedMesh.Positions.Max(p => p.Z) - loweredMesh.Positions.Max(p => p.Z) > 0.5);

            MeshSculptState ExportAndReplay(string name)
            {
                var path = Path.Combine(folder, name + ".glb");
                using var report = Run("build", source, "--state", name, "--out", path);
                Assert.Equal(sourceStateId, report.RootElement.GetProperty("sourceBodyStateId").GetString());
                Assert.True(report.RootElement.GetProperty("topology").GetProperty("isWatertight").GetBoolean());
                Assert.Equal(0x46546C67u, BitConverter.ToUInt32(File.ReadAllBytes(path), 0));
                using var sidecar = JsonDocument.Parse(File.ReadAllText(path + ".sculpt.json"));
                var replay = MeshConstructionReplayer.Replay(sidecar.RootElement.GetProperty("construction").GetRawText());
                Assert.Equal(report.RootElement.GetProperty("stateId").GetString(), replay.StateId);
                Assert.Equal("Rim", replay.Construction.RimSection);
                return replay;
            }
        }
        finally
        {
            foreach (var path in Directory.GetFiles(folder)) File.Delete(path);
            Directory.Delete(folder);
        }
    }

    [Trait("Category", "Slow")]
    [Fact]
    public void WheelRecessFairsWithAStableRimAndCertifiedPreviewClearance()
    {
        var fixture = Path.Combine(Root(), "fixtures/Canonical/Sculpting");
        var mouse = Path.Combine(fixture, "compact-mouse.firmament");
        var wheel = Path.Combine(fixture, "compact-mouse-wheel.firmament");
        var folder = Path.Combine(Root(), "artifacts/local/tests/wheel-recess", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var step = Path.Combine(folder, "wheel.step");
            using var wheelBuild = Run("build", wheel, "--out", step);
            var svg = Path.Combine(folder, "compare.svg");
            using var comparison = Run("compare", mouse, "--before", "WheelOpening", "--after", "WheelFaired",
                "--wheel", step, "--minimum-clearance", "1", "--out", svg);
            var report = comparison.RootElement;
            var movement = report.GetProperty("movement");
            Assert.InRange(movement.GetProperty("maximumMovementMm").GetDouble(), 0.15, 0.3);
            Assert.Equal(128, movement.GetProperty("rimSamples").GetInt32());
            Assert.Equal(0, movement.GetProperty("maximumRimMovementMm").GetDouble());
            Assert.True(report.GetProperty("beforeRecess").GetProperty("boundaryEdges").GetInt32() > 0);
            var fair = report.GetProperty("fairEffect");
            Assert.True(fair.GetProperty("afterNormalTurnRmsDegrees").GetDouble() <
                fair.GetProperty("beforeNormalTurnRmsDegrees").GetDouble());
            var clearance = report.GetProperty("clearanceAfter");
            Assert.True(clearance.GetProperty("centerOutsideShell").GetBoolean());
            Assert.InRange(clearance.GetProperty("lowerBoundMm").GetDouble(), 1.3, 1.5);
            Assert.Equal(0.025, clearance.GetProperty("intervalWidthMm").GetDouble());
            Assert.Contains("Center section", File.ReadAllText(svg), StringComparison.Ordinal);

            var glb = Path.Combine(folder, "shell.glb");
            using var exported = Run("build", mouse, "--state", "WheelFaired", "--out", glb);
            Assert.True(exported.RootElement.GetProperty("topology").GetProperty("isWatertight").GetBoolean());
            using var sidecar = JsonDocument.Parse(File.ReadAllText(glb + ".sculpt.json"));
            var replay = MeshConstructionReplayer.Replay(sidecar.RootElement.GetProperty("construction").GetRawText());
            Assert.Equal(exported.RootElement.GetProperty("stateId").GetString(), replay.StateId);
            Assert.IsType<RecessCageRegion>(replay.Construction.Operations[^2]);
            Assert.IsType<FairCageRegion>(replay.Construction.Operations[^1]);

            var rejected = Path.Combine(folder, "rejected.svg");
            var stdout = new StringWriter();
            Assert.Equal(1, CliRunner.Run(["compare", mouse, "--before", "LowFront", "--after", "WheelFaired",
                "--out", rejected, "--json"], stdout, new StringWriter()));
            Assert.Contains("mesh-compare-correspondence-unsupported", stdout.ToString(), StringComparison.Ordinal);
            Assert.False(File.Exists(rejected));
        }
        finally
        {
            foreach (var path in Directory.GetFiles(folder)) File.Delete(path);
            Directory.Delete(folder);
        }
    }

    [Trait("Category", "Slow")]
    [Fact]
    public void SurfaceMountMeasuresTheCurrentMeshAndReplaysItsFrame()
    {
        var fixture = Path.Combine(Root(), "fixtures/Canonical/Sculpting/compact-mouse.firmament");
        var folder = Path.Combine(Root(), "artifacts/local/tests/surface-mount", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        const string mount = "SurfaceMount Lens { section: Shoulder; span: FrontRight; at: 0.2 }";
        var source = Path.Combine(folder, "mount.firmament");
        var glb = Path.Combine(folder, "mount.glb");
        try
        {
            var authored = File.ReadAllText(fixture).Replace("        SurfaceAnchor Nose", "        " + mount + "\n        SurfaceAnchor Nose", StringComparison.Ordinal);
            File.WriteAllText(source, authored);
            using var report = Run("build", source, "--state", "LowFront", "--out", glb);
            var frame = Assert.Single(report.RootElement.GetProperty("surfaceMounts").EnumerateArray());
            Assert.Equal("Lens", frame.GetProperty("name").GetString());
            Assert.Equal(0.2, frame.GetProperty("at").GetDouble());
            using var inspection = Run("inspect", source);
            var seed = Assert.Single(inspection.RootElement.GetProperty("meshStates").GetProperty("Seed")
                .GetProperty("surfaceMounts").EnumerateArray());
            Assert.True(seed.GetProperty("positionMm").GetProperty("z").GetDouble() -
                frame.GetProperty("positionMm").GetProperty("z").GetDouble() > 0.01);

            using var sidecar = JsonDocument.Parse(File.ReadAllText(glb + ".sculpt.json"));
            var replay = MeshConstructionReplayer.Replay(sidecar.RootElement.GetProperty("construction").GetRawText());
            Assert.Equal(report.RootElement.GetProperty("stateId").GetString(), replay.StateId);
            var resolved = Assert.Single(replay.SurfaceMounts!);
            Assert.Equal(frame.GetProperty("positionMm").GetProperty("z").GetDouble(), resolved.PositionMm.Z);
            Assert.InRange(resolved.Longitudinal.Cross(resolved.Crosswise).Dot(resolved.Normal), 0.999999999, 1.000000001);

            File.WriteAllText(source, authored.Replace(mount, mount.Replace("at: 0.2", "at: 1", StringComparison.Ordinal), StringComparison.Ordinal));
            var rejected = new StringWriter();
            Assert.Equal(1, CliRunner.Run(["validate", source, "--json"], rejected, new StringWriter()));
            Assert.Contains("mesh-mount-parameter-invalid", rejected.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            foreach (var path in Directory.GetFiles(folder)) File.Delete(path);
            Directory.Delete(folder);
        }
    }

    [Trait("Category", "Slow")]
    [Fact]
    public void SplineCutoutRebuildsMirroredArchesAndQualifiesWheelSweep()
    {
        var fixture = Path.Combine(Root(), "fixtures/Canonical/Sculpting");
        var source = Path.Combine(fixture, "sports-coupe-front-clip.firmament");
        var tire = Path.Combine(fixture, "sports-coupe-front-tire.firmament");
        var folder = Path.Combine(Root(), "artifacts/local/tests/front-arch", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var step = Path.Combine(folder, "tire.step");
            using var tireBuild = Run("build", tire, "--out", step);
            using var compare = Run("compare", source, "--before", "Seed", "--after", "Arches", "--wheel", step,
                "--steering-lock", "32", "--suspension-bump", "65", "--minimum-clearance", "20");
            var report = compare.RootElement;
            Assert.False(report.GetProperty("clearanceBefore").GetProperty("centerOutsideShell").GetBoolean());
            var sweep = report.GetProperty("wheelSweep");
            Assert.Equal(27, sweep.GetProperty("poses").GetArrayLength());
            Assert.True(sweep.GetProperty("lowerBoundMm").GetDouble() > 20);
            Assert.True(sweep.GetProperty("maximumDistanceFromNearestPoseMm").GetDouble() > 0);
            var effect = report.GetProperty("cutoutEffect");
            Assert.Equal("LeftFrontArch", effect.GetProperty("mirroredFeature").GetString());
            var lips = effect.GetProperty("lipControls");
            Assert.True(lips.GetProperty("RightFrontArch").GetInt32() > 0);
            Assert.Equal(lips.GetProperty("RightFrontArch").GetInt32(), lips.GetProperty("LeftFrontArch").GetInt32());
            var motion = report.GetProperty("movement");
            Assert.Equal(0, motion.GetProperty("maximumRimMovementMm").GetDouble());

            var shell = Path.Combine(folder, "shell.glb");
            using var export = Run("build", source, "--state", "Arches", "--out", shell);
            Assert.True(export.RootElement.GetProperty("topology").GetProperty("isWatertight").GetBoolean());
            using var sidecar = JsonDocument.Parse(File.ReadAllText(shell + ".sculpt.json"));
            var replay = MeshConstructionReplayer.Replay(sidecar.RootElement.GetProperty("construction").GetRawText());
            Assert.Equal(export.RootElement.GetProperty("stateId").GetString(), replay.StateId);
            Assert.IsType<CutoutCageArch>(replay.Construction.Operations[^1]);

            var edited = Path.Combine(folder, "taller.firmament");
            File.WriteAllText(edited, File.ReadAllText(source).Replace("820mm", "855mm", StringComparison.Ordinal));
            using var altered = Run("compare", edited, "--before", "Seed", "--after", "Arches");
            Assert.True(altered.RootElement.GetProperty("movement").GetProperty("maximumMovementMm").GetDouble()
                > motion.GetProperty("maximumMovementMm").GetDouble() + 30);
        }
        finally
        {
            foreach (var path in Directory.GetFiles(folder)) File.Delete(path);
            Directory.Delete(folder);
        }
    }

    [Trait("Category", "Slow")]
    [Fact]
    public void MirroredStrokeReplaysBothGripsAndRejectsAnUnmatchedPlane()
    {
        var mouse = Path.Combine(Root(), "fixtures/Canonical/Sculpting/compact-mouse.firmament");
        var folder = Path.Combine(Root(), "artifacts/local/tests/mirrored-stroke", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var shell = Path.Combine(folder, "grips.glb");
            using var build = Run("build", mouse, "--state", "SideGrips", "--out", shell);
            var result = build.RootElement;
            Assert.True(result.GetProperty("topology").GetProperty("isWatertight").GetBoolean());
            var stroke = result.GetProperty("strokeEffect");
            Assert.Equal("RightGrip", stroke.GetProperty("feature").GetString());
            Assert.True(stroke.GetProperty("pathControls").GetInt32() > 2);
            var mirror = stroke.GetProperty("mirror");
            Assert.Equal("LeftGrip", mirror.GetProperty("feature").GetString());
            Assert.Equal("Stations.Centerline", mirror.GetProperty("across").GetString());
            Assert.Equal(-1.8, mirror.GetProperty("reflectedOffsetMm").GetProperty("y").GetDouble());
            Assert.InRange(mirror.GetProperty("maximumSampledSymmetryErrorMm").GetDouble(), 0, 1e-10);
            var movement = stroke.GetProperty("movement");
            Assert.Equal(movement.GetProperty("samples").GetInt32(), mirror.GetProperty("matchedSamples").GetInt32());
            Assert.InRange(movement.GetProperty("maximumMovementMm").GetDouble(), 1.3, 1.5);
            Assert.Equal(128, movement.GetProperty("rimSamples").GetInt32());
            Assert.Equal(0, movement.GetProperty("maximumRimMovementMm").GetDouble());

            using var sidecar = JsonDocument.Parse(File.ReadAllText(shell + ".sculpt.json"));
            var replay = MeshConstructionReplayer.Replay(sidecar.RootElement.GetProperty("construction").GetRawText());
            Assert.Equal(result.GetProperty("stateId").GetString(), replay.StateId);
            var replayStroke = Assert.IsType<DisplaceCageStroke>(replay.Construction.Operations[^1]);
            Assert.Equal(["GripFront", "GripRear"], replayStroke.Path);
            Assert.Equal("LeftGrip", replayStroke.Mirror!.Feature);

            var source = File.ReadAllText(mouse);
            var wrongPlane = Path.Combine(folder, "wrong-plane.firmament");
            File.WriteAllText(wrongPlane, source.Replace("across: Stations.Centerline", "across: Stations.Rim", StringComparison.Ordinal));
            var rejected = Path.Combine(folder, "rejected.glb");
            var stdout = new StringWriter();
            Assert.Equal(1, CliRunner.Run(["build", wrongPlane, "--state", "SideGrips", "--out", rejected, "--json"], stdout, new StringWriter()));
            Assert.Contains("mesh-stroke-mirror-correspondence", stdout.ToString(), StringComparison.Ordinal);
            Assert.False(File.Exists(rejected));

            var overlap = Path.Combine(folder, "overlap.firmament");
            File.WriteAllText(overlap, source
                .Replace("[WheelFaired.GripFront, WheelFaired.GripRear]", "[WheelFaired.Nose, WheelFaired.GripFront]", StringComparison.Ordinal)
                .Replace("[0mm, 1.8mm, -0.5mm]", "[0mm, 0mm, -0.2mm]", StringComparison.Ordinal));
            using var compare = Run("compare", overlap, "--before", "WheelFaired", "--after", "SideGrips");
            var overlapped = compare.RootElement.GetProperty("strokeEffect");
            Assert.True(overlapped.GetProperty("mirror").GetProperty("sharedFootprintControls").GetInt32() > 0);
            Assert.InRange(overlapped.GetProperty("movement").GetProperty("maximumMovementMm").GetDouble(), 0.15, 0.200000001);
        }
        finally
        {
            foreach (var path in Directory.GetFiles(folder)) File.Delete(path);
            Directory.Delete(folder);
        }
    }

    [Trait("Category", "Slow")]
    [Theory]
    [InlineData("maximumRimDeviation: 0.15mm", "maximumRimDeviation: 0.08mm", "mesh-section-rim-deviation-exceeded")]
    [InlineData("output: LowFront", "MeshState Uncrease { input: Seed; Crease { edges: [All]; mode: Smooth } }\n    output: Uncrease", "mesh-section-rim-crease-changed")]
    [InlineData("end: Cap", "end: Open", "section-chain-end-continuity-incompatible")]
    [InlineData("anchor: Seed.Nose", "anchor: PalmComfort.Nose", "mesh-brush-anchor-unresolved")]
    [InlineData("span: FrontRight }", "span: Unknown }", "mesh-anchor-unresolved")]
    [InlineData("preserve: [Rim]", "preserve: [Shoulder]", "mesh-brush-anchor-protected")]
    [InlineData("drop: 0.6mm", "drop: 0.6deg", "firmament-feature-argument-type")]
    public void SectionChainCageRejectsAnUnsatisfiedContract(string before, string after, string diagnostic)
    {
        var folder = Path.Combine(Root(), "artifacts/local/tests/section-chain-cage", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var source = Path.Combine(folder, "invalid.firmament");
        var output = Path.Combine(folder, "invalid.glb");
        try
        {
            var mouse = File.ReadAllText(Path.Combine(Root(), "fixtures/Canonical/Sculpting/compact-mouse.firmament"));
            File.WriteAllText(source, mouse.Replace(before, after));
            var stdout = new StringWriter();
            Assert.Equal(1, CliRunner.Run(["build", source, "--out", output, "--json"], stdout, new StringWriter()));
            Assert.Contains(diagnostic, stdout.ToString(), StringComparison.Ordinal);
            Assert.False(File.Exists(output));
        }
        finally
        {
            File.Delete(source);
            Directory.Delete(folder);
        }
    }

    private static JsonDocument Run(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        Assert.True(CliRunner.Run([.. args, "--json"], stdout, stderr) == 0, stdout + stderr.ToString());
        return JsonDocument.Parse(stdout.ToString());
    }

    private static string Root() { var d = new DirectoryInfo(AppContext.BaseDirectory); while (d is not null && !File.Exists(Path.Combine(d.FullName, "Aetheris.slnx"))) d = d.Parent; return d!.FullName; }
}
