using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class AssemblyKinematicsX0Tests
{
    [Fact]
    public void TwoLinkArm_ComposesPureRevolutePosesAndReusesDefinitions()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/two-link-arm.firmament");
        var compiled = new AssemblyM0Pipeline().CompileFile(path);
        Assert.True(compiled.IsSuccess, Evidence(compiled));
        var ir = compiled.Ir!;
        Assert.Equal(2, ir.Joints!.Count);
        Assert.All(ir.Joints, joint => Assert.Equal(MechanicalInterfaceFamily.Revolute, joint.Family));
        Assert.All(ir.Joints, joint => Assert.Equal(1, joint.DegreesOfFreedom));
        var zero = AssemblyKinematics.Evaluate(ir, new Dictionary<string, double>());
        Assert.True(zero.IsSuccess, Evidence(zero));
        Assert.Equal(0, zero.State["Shoulder"]);
        Assert.Equal(0, zero.State["Elbow"]);
        Assert.Equal(20, X(zero, "TwoLinkArm.Link2"), 6);
        Assert.Equal(0, Y(zero, "TwoLinkArm.Link2"), 6);
        var fortyFive = AssemblyKinematics.Evaluate(ir, new Dictionary<string, double> { ["Shoulder"] = 45 });
        Assert.Equal(20 / Math.Sqrt(2), X(fortyFive, "TwoLinkArm.Link2"), 5);
        Assert.Equal(20 / Math.Sqrt(2), Y(fortyFive, "TwoLinkArm.Link2"), 5);
        var plus = AssemblyKinematics.Evaluate(ir, new Dictionary<string, double> { ["Shoulder"] = 90, ["Elbow"] = 45 });
        Assert.True(plus.IsSuccess, Evidence(plus));
        Assert.Equal(0, X(plus, "TwoLinkArm.Link2"), 5);
        Assert.Equal(20, Y(plus, "TwoLinkArm.Link2"), 5);
        Assert.Equal(Math.Cos(135 * Math.PI / 180),
            plus.Instances.Single(instance => instance.Path.ToString() == "TwoLinkArm.Link2").ResolvedTransform!.Matrix[0], 5);
        var minus = AssemblyKinematics.Evaluate(ir, new Dictionary<string, double> { ["Shoulder"] = -45 });
        Assert.Equal(20 / Math.Sqrt(2), X(minus, "TwoLinkArm.Link2"), 5);
        Assert.Equal(-20 / Math.Sqrt(2), Y(minus, "TwoLinkArm.Link2"), 5);
        Assert.Equal(plus.Instances.Select(instance => instance.ResolvedTransform?.Matrix),
            AssemblyKinematics.Evaluate(ir, new Dictionary<string, double> { ["Elbow"] = 45, ["Shoulder"] = 90 })
                .Instances.Select(instance => instance.ResolvedTransform?.Matrix), new MatrixComparer());
        Assert.Equal(ir.Instances.Where(instance => instance.DefinitionIdentity == "ArmLink").Select(instance => instance.DefinitionIdentity),
            plus.Instances.Where(instance => instance.DefinitionIdentity == "ArmLink").Select(instance => instance.DefinitionIdentity));
        Assert.Same(ir.Instances.Single(instance => instance.Path.ToString() == "TwoLinkArm.Link1").SemanticRoot,
            plus.Instances.Single(instance => instance.Path.ToString() == "TwoLinkArm.Link1").SemanticRoot);
    }

    [Theory]
    [InlineData("Fixed", 0, 10)]
    [InlineData("Prismatic", 15, 25)]
    public void FrameContract_UsesExactZeroAndDirectState(string family, double state, double expectedZ)
    {
        var source = $$"""
            Assembly Pair {
              <Assembly Pair>
                <Part Base = Block> Semantic Port { DatumFrame Frame = [0,0,10] x [1,0,0] y [0,1,0] z [0,0,1]; } </Part>
                <Part Child = Block> Semantic Port { DatumFrame Frame = [0,0,0] x [1,0,0] y [0,1,0] z [0,0,1]; } </Part>
              </Assembly>
              Anchor: Pair.Base.Port;
              Interface<{{family}}> Joint { A: Pair.Base.Port; B: Pair.Child.Port; }
            }
            """;
        var parsed = new AssemblyM0Parser().Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var compiled = new AssemblyM0Compiler().Compile(parsed.Source!);
        Assert.True(compiled.IsSuccess, Evidence(compiled));
        var joint = Assert.Single(compiled.Ir!.Joints!);
        Assert.Equal(family == "Fixed" ? 0 : 1, joint.DegreesOfFreedom);
        var zero = AssemblyKinematics.Evaluate(compiled.Ir, new Dictionary<string, double>());
        Assert.Equal(10, Z(zero, "Pair.Child"), 6);
        var pose = AssemblyKinematics.Evaluate(compiled.Ir, family == "Fixed" ? new Dictionary<string, double>() : new Dictionary<string, double> { ["Joint"] = state });
        Assert.True(pose.IsSuccess, Evidence(pose));
        Assert.Equal(expectedZ, Z(pose, "Pair.Child"), 6);
    }

    [Fact]
    public void OpposedFixedFramePoseMatchesCompiledZeroTransform()
    {
        const string source = """
            Assembly Pair {
              <Assembly Pair>
                <Part Base = Block> Semantic Port { DatumFrame Frame = [0,0,0] x [1,0,0] y [0,1,0] z [0,0,1]; } </Part>
                <Part Child = Block> Semantic Port { DatumFrame Frame = [0,0,0] x [1,0,0] y [0,1,0] z [0,0,1]; } </Part>
              </Assembly>
              Anchor: Pair.Base.Port;
              Interface<Fixed> Mount { A: Pair.Base.Port; B: Pair.Child.Port; Lower FrameCoincident A B OpposedDirection; }
            }
            """;
        var compiled = new AssemblyM0Compiler().Compile(new AssemblyM0Parser().Parse(source).Source!);
        Assert.True(compiled.IsSuccess, Evidence(compiled));
        var child = compiled.Ir!.Instances.Single(instance => instance.Path.ToString() == "Pair.Child");
        var poseChild = AssemblyKinematics.Evaluate(compiled.Ir, new Dictionary<string, double>()).Instances
            .Single(instance => instance.StableId == child.StableId);
        Assert.Equal(child.ResolvedTransform!.Matrix, poseChild.ResolvedTransform!.Matrix);
    }

    [Fact]
    public void AuthoredRevoluteFrameMemberSelectsSourceAddressableDatum()
    {
        const string source = """
            Assembly Pair {
              <Assembly Pair>
                <Part Base = Block> Semantic Port {
                  DatumFrame Other = [0,0,0] x [1,0,0] y [0,1,0] z [0,0,1];
                  DatumFrame Pivot = [0,0,5] x [1,0,0] y [0,1,0] z [0,0,1];
                } </Part>
                <Part Child = Block> Semantic Port {
                  DatumFrame Other = [0,0,0] x [1,0,0] y [0,1,0] z [0,0,1];
                  DatumFrame Pivot = [0,0,0] x [1,0,0] y [0,1,0] z [0,0,1];
                } </Part>
              </Assembly>
              Anchor: Pair.Base.Port;
              Interface<Revolute> Joint { A: Pair.Base.Port; B: Pair.Child.Port; Lower FrameCoincident A.Pivot B.Pivot; }
            }
            """;
        var compiled = new AssemblyM0Compiler().Compile(new AssemblyM0Parser().Parse(source).Source!);
        Assert.True(compiled.IsSuccess, Evidence(compiled));
        Assert.Equal(5, Z(AssemblyKinematics.Evaluate(compiled.Ir!, new Dictionary<string, double>()), "Pair.Child"), 6);
        Assert.EndsWith(".Pivot", Assert.Single(compiled.Ir!.Joints!).ParentFrameSemanticId, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingFrameAndInvalidState_AreDiagnosed()
    {
        const string source = """
            Assembly Pair {
              <Assembly Pair>
                <Part Base = Block> Semantic Port { DatumFrame Frame = [0,0,0] x [1,0,0] y [0,1,0] z [0,0,1]; } </Part>
                <Part Child = Block> Semantic Port { Point Origin = [0,0,0]; } </Part>
              </Assembly>
              Anchor: Pair.Base.Port;
              Interface<Prismatic> Joint { A: Pair.Base.Port; B: Pair.Child.Port; }
            }
            """;
        var compiled = new AssemblyM0Compiler().Compile(new AssemblyM0Parser().Parse(source).Source!);
        Assert.False(compiled.IsSuccess);
        Assert.Contains(compiled.Diagnostics, diagnostic => diagnostic.Code == AssemblyM0Compiler.CapabilityMismatch);
        Assert.Contains(compiled.Diagnostics, diagnostic => diagnostic.Code == "assembly-interface-missing-frame");
        var invalidLower = source.Replace("B: Pair.Child.Port; }", "B: Pair.Child.Port; Lower AxisCoincident A.Axis B.Axis; }", StringComparison.Ordinal);
        Assert.Contains(new AssemblyM0Parser().Parse(invalidLower).Diagnostics,
            diagnostic => diagnostic.Code == "assembly-interface-invalid-contract-lowering");
        var valid = new AssemblyM0Pipeline().CompileFile(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/two-link-arm.firmament"));
        var invalidState = AssemblyKinematics.Evaluate(valid.Ir!, new Dictionary<string, double> { ["Shoulder"] = double.NaN });
        Assert.False(invalidState.IsSuccess);
        Assert.Contains(invalidState.Diagnostics, diagnostic => diagnostic.Code == "assembly-kinematic-invalid-state");
    }

    [Fact]
    public void ExternalStepDefinitionsStaySharedWhenPoseChanges()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/external-step-slider.firmament");
        var compiled = new AssemblyM1Pipeline().CompileFile(path);
        Assert.True(compiled.IsSuccess, string.Join("\n", compiled.Diagnostics.Select(diagnostic => diagnostic.Code + ": " + diagnostic.Message)));
        Assert.Single(compiled.Geometry!.Artifact.Definitions);
        var pose = AssemblyKinematics.Evaluate(compiled.Ir!, new Dictionary<string, double> { ["Travel"] = 12 });
        Assert.True(pose.IsSuccess, Evidence(pose));
        Assert.Equal(12, Z(pose, "ExternalSlider.Carriage"), 6);
        Assert.Equal(0, Z(pose, "ExternalSlider.Rail"), 6);
        var restDisplay = AssemblyDisplayMeshExporter.Export(compiled);
        var display = AssemblyDisplayMeshExporter.WithPose(restDisplay, pose);
        Assert.Same(restDisplay.Definitions, display.Definitions);
        Assert.Single(display.Definitions);
        Assert.Equal(2, display.Occurrences.Count(occurrence => occurrence.DefinitionId == display.Definitions[0].Id));
        Assert.Equal(12, display.Occurrences.Single(occurrence => occurrence.Path == "ExternalSlider.Carriage").Transform[14], 6);
    }

    [Fact]
    public void DuplicateDriverAndKinematicLoop_FailAtCompileTime()
    {
        var source = File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/two-link-arm.firmament"));
        var insertion = source.LastIndexOf('}');
        var duplicate = source.Insert(insertion, "  Interface<Revolute> Other { A: TwoLinkArm.Base.Hinge; B: TwoLinkArm.Link1.Input; }\n");
        var duplicateResult = new AssemblyM0Compiler().Compile(new AssemblyM0Parser().Parse(duplicate).Source!);
        Assert.Contains(duplicateResult.Diagnostics, diagnostic => diagnostic.Code == "assembly-kinematic-duplicate-child");
        var loop = source.Insert(insertion, "  Interface<Revolute> Return { A: TwoLinkArm.Link2.Input; B: TwoLinkArm.Base.Hinge; }\n");
        var loopResult = new AssemblyM0Compiler().Compile(new AssemblyM0Parser().Parse(loop).Source!);
        Assert.Contains(loopResult.Diagnostics, diagnostic => diagnostic.Code == "assembly-kinematic-loop-unsupported");
    }

    [Fact]
    public void SchemaListsThreeFrameContractsAndRequiredParticipants()
    {
        foreach (var family in new[] { "Fixed", "Revolute", "Prismatic" })
        {
            var schema = FirmamentSemanticSchemas.Get($"Interface<{family}>");
            Assert.NotNull(schema);
            Assert.Equal(["A", "B"], schema.Fields.Select(field => field.Name));
            Assert.All(schema.Fields, field => Assert.True(field.Required));
        }
        const string source = "Assembly A { Interface<Revolute> Joint { ";
        var completion = FirmamentLanguageService.Complete(source, "arm.firmament", "1", source.Length);
        Assert.Equal("Interface<Revolute>", completion.Context);
        Assert.Equal(["A", "B"], completion.MissingRequiredFields);
    }

    private static double X(AssemblyPoseResult pose, string path) => Component(pose, path, 12);
    private static double Y(AssemblyPoseResult pose, string path) => Component(pose, path, 13);
    private static double Z(AssemblyPoseResult pose, string path) => Component(pose, path, 14);
    private static double Component(AssemblyPoseResult pose, string path, int index) =>
        pose.Instances.Single(instance => instance.Path.ToString() == path).ResolvedTransform!.Matrix[index];
    private static string Evidence(AssemblyCompilationResult result) => string.Join("\n", result.Diagnostics.Select(diagnostic => diagnostic.Code + ": " + diagnostic.Message));
    private static string Evidence(AssemblyPoseResult result) => string.Join("\n", result.Diagnostics.Select(diagnostic => diagnostic.Code + ": " + diagnostic.Message));
    private sealed class MatrixComparer : IEqualityComparer<double[]?>
    {
        public bool Equals(double[]? x, double[]? y) => x is null ? y is null : y is not null && x.SequenceEqual(y);
        public int GetHashCode(double[]? obj) => 0;
    }
}
