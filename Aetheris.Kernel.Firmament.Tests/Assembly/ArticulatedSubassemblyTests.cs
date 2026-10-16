using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class ArticulatedSubassemblyTests
{
    [Fact]
    public void RepeatedArticulatedDefinitionsKeepIndependentStateAndResolveRigidAncestry()
    {
        var compiled = new AssemblyM1Pipeline().CompileFile(Fixture("articulated-subassemblies.firmament"));
        Assert.True(compiled.IsSuccess, Evidence(compiled.Diagnostics));
        var ir = compiled.Ir!;
        Assert.Equal(2, compiled.Geometry!.Artifact.Definitions.Count);
        var definition = Assert.Single(ir.AssemblyDefinitions!);
        Assert.Single(definition.LocalJoints!);
        Assert.Equal(5, ir.Joints!.Count);
        var zero = AssemblyKinematics.Evaluate(ir, new Dictionary<string, double>());
        Assert.True(zero.IsSuccess, Evidence(zero.Diagnostics));
        foreach (var instance in ir.Instances)
            AssertMatrix(instance.ResolvedTransform!.Matrix, zero.Instances.Single(i => i.StableId == instance.StableId).ResolvedTransform!.Matrix);
        var state = new Dictionary<string, double> { ["Shoulder"] = 90, ["Articulated.First.Elbow"] = 45, ["Articulated.Second.Elbow"] = -30 };
        var pose = AssemblyKinematics.Evaluate(ir, state);
        Assert.True(pose.IsSuccess, Evidence(pose.Diagnostics));
        var tip = pose.Instances.Single(i => i.Path.ToString() == "Articulated.First.Tip");
        Assert.Equal(0, tip.ResolvedTransform!.Matrix[12], 9);
        Assert.Equal(20, tip.ResolvedTransform.Matrix[13], 9);
        Assert.Equal(Math.Cos(135 * Math.PI / 180), tip.ResolvedTransform.Matrix[0], 9);
        var follower = pose.Instances.Single(i => i.Path.ToString() == "Articulated.Follower");
        AssertMatrix(tip.ResolvedTransform.Matrix, follower.ResolvedTransform!.Matrix);
        var port = ir.Instances.Single(i => i.Path.ToString() == "Articulated.First").SemanticRoot.ExposedMembers["Tool"];
        var tool = Assert.IsType<ExactDatumFrameBinding>(AssemblyWorldQuery.Resolve(ir with { Instances = pose.Instances }, port.StableIdentity));
        Assert.Equal(tip.ResolvedTransform.Matrix[0], tool.XAxisX, 9);
        Assert.Equal(tip.ResolvedTransform.Matrix[13], tool.OriginY, 9);
        var second = pose.Instances.Single(i => i.Path.ToString() == "Articulated.Second.Tip");
        Assert.Equal(120, second.ResolvedTransform!.Matrix[12], 9);
        Assert.Equal(-0.5, second.ResolvedTransform.Matrix[1], 9);
        var again = AssemblyKinematics.Evaluate(ir, state.Reverse().ToDictionary(p => p.Key, p => p.Value));
        foreach (var instance in pose.Instances)
        {
            AssertMatrix(instance.ResolvedTransform!.Matrix, again.Instances.Single(i => i.StableId == instance.StableId).ResolvedTransform!.Matrix);
            Assert.Same(ir.Instances.Single(i => i.StableId == instance.StableId).SemanticRoot, instance.SemanticRoot);
        }
        Assert.True(AssemblyIrAp242Exporter.Export(compiled).IsSuccess);
    }

    [Fact]
    public void SharedDerivedFramesComposeBeforeSeating()
    {
        var compiled = new AssemblyM1Pipeline().CompileFile(Fixture("derived-datum-frames.firmament"));
        Assert.True(compiled.IsSuccess, Evidence(compiled.Diagnostics));
        var cam = Assert.Single(compiled.Ir!.Instances, i => i.Kind == AssemblyInstanceKind.Part);
        var matrix = cam.ResolvedTransform!.Matrix;
        Assert.Equal(3 + 100 / Math.Sqrt(2), matrix[12], 9);
        Assert.Equal(4 + 100 / Math.Sqrt(2), matrix[13], 9);
        Assert.Equal(25, matrix[14], 9);
        Assert.Equal(1, matrix[0], 9);
        Assert.Equal(0, matrix[1], 9);
    }

    [Fact]
    public void PlainPlacementFollowsAMovingTargetWithoutCreatingAPhysicalJoint()
    {
        var compiled = new AssemblyM1Pipeline().CompileFile(Fixture("relative-moving-frames.firmament"));
        Assert.True(compiled.IsSuccess, Evidence(compiled.Diagnostics));
        var ir = compiled.Ir!;
        Assert.Equal(2, ir.Joints!.Count);
        Assert.Single(ir.FrameDependencies);
        var pose = AssemblyKinematics.Evaluate(ir, new Dictionary<string, double> { ["Base"] = 90, ["Elbow"] = 45 });
        Assert.True(pose.IsSuccess, Evidence(pose.Diagnostics));
        foreach (var name in new[] { "Rigid", "Tip" })
        {
            var item = pose.Instances.Single(i => i.Path.ToString() == "RelativeFrames." + name);
            Assert.Equal(0, item.ResolvedTransform!.Matrix[12], 9);
            Assert.Equal(20, item.ResolvedTransform.Matrix[13], 9);
        }
        var tip = pose.Instances.Single(i => i.Path.ToString() == "RelativeFrames.Tip");
        Assert.Equal(Math.Cos(135 * Math.PI / 180), tip.ResolvedTransform!.Matrix[0], 9);
        var usd = AssemblyUsdExporter.Export(compiled);
        Assert.DoesNotContain("PhysicsFixedJoint", usd);
        Assert.Contains("PhysicsRevoluteJoint", usd);
    }

    [Theory]
    [InlineData("B: Articulated.First.Mount", "B: Articulated.First.Tool")]
    [InlineData("from: Mount;", "from: Tool;")]
    public void MovingInternalPortsCannotSilentlyBecomeRigidInstallationMounts(string from, string to)
    {
        var source = File.ReadAllText(Fixture("articulated-subassemblies.firmament")).Replace(from, to);
        var parsed = new AssemblyM0Parser().Parse(source);
        Assert.True(parsed.IsSuccess, Evidence(parsed.Diagnostics));
        var compiled = new AssemblyM0Compiler().Compile(parsed.Source!);
        Assert.False(compiled.IsSuccess);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "assembly-kinematic-moving-mount-unsupported");
    }

    [Fact]
    public void ExposedMovingParentRetainsAuthoredGapAndClocking()
    {
        var source = File.ReadAllText(Fixture("articulated-subassemblies.firmament"))
            .Replace("Interface<Fixed> Follow {", "Interface<Fixed> Follow { gap: 2mm; clocking: 30deg;");
        var compiled = new AssemblyM1Pipeline().Compile(source);
        Assert.True(compiled.IsSuccess, Evidence(compiled.Diagnostics));
        var ir = compiled.Ir!;
        var zero = AssemblyKinematics.Evaluate(ir, new Dictionary<string, double>());
        foreach (var instance in ir.Instances)
            AssertMatrix(instance.ResolvedTransform!.Matrix, zero.Instances.Single(i => i.StableId == instance.StableId).ResolvedTransform!.Matrix);
        var pose = AssemblyKinematics.Evaluate(ir, new Dictionary<string, double> { ["Shoulder"] = 90, ["Articulated.First.Elbow"] = 45 });
        Assert.True(pose.IsSuccess, Evidence(pose.Diagnostics));
        var follower = pose.Instances.Single(i => i.Path.ToString() == "Articulated.Follower");
        Assert.Equal(2, follower.ResolvedTransform!.Matrix[14], 9);
        Assert.Equal(Math.Cos(165 * Math.PI / 180), follower.ResolvedTransform.Matrix[0], 9);
    }

    [Theory]
        [InlineData("Interface Drive = Elbow;")]
    public void UnimplementedPublicInterfaceContractsDoNotDisappear(string exposure)
    {
        var source = File.ReadAllText(Fixture("articulated-subassemblies.firmament"))
            .Replace("DatumFrame Tool = Tip.Pivot.Frame;", "DatumFrame Tool = Tip.Pivot.Frame; " + exposure);
        var parsed = new AssemblyM0Parser().Parse(source);
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, d => d.Code == "assembly-expose-kind-unsupported");
    }

    [Fact]
    public void UnresolvedPublicMateAliasRejectsRatherThanDisappearing()
    {
        var source = File.ReadAllText(Fixture("articulated-subassemblies.firmament")).Replace("Mate Bend = Elbow;", "Mate Bend = Missing;");
        var parsed = new AssemblyM0Parser().Parse(source);
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, d => d.Code == "assembly-expose-mate-unresolved");
    }

    [Fact]
    public void PublicMateChannelCanBeReexportedThroughANestedReusableDefinition()
    {
        var source = File.ReadAllText(Fixture("articulated-subassemblies.firmament"));
        var wrapper = """
            Subassembly Sleeve {
              <Assembly Sleeve><Assembly Unit = Arm>
                Placement { from: Mount; to: World; }
              </Assembly></Assembly>
              anchor: Sleeve;
              expose { DatumFrame Mount = Unit.Mount; DatumFrame Tool = Unit.Tool; Mate Bend = Unit.Bend; }
            }
            """;
        source = source.Replace("Assembly Articulated {", wrapper + "\nAssembly Articulated {")
            .Replace("<Assembly First = Arm>", "<Assembly First = Sleeve>")
            .Replace("<Assembly Second = Arm>", "<Assembly Second = Sleeve>");
        source += "\n" + string.Join('\n', File.ReadAllLines(Fixture("articulated-motion.firmament")).Where(l => !l.StartsWith("include ", StringComparison.Ordinal)));
        var compiled = new AssemblyM1Pipeline().Compile(source);
        Assert.True(compiled.IsSuccess, Evidence(compiled.Diagnostics));
        var joint = Assert.Single(compiled.Ir!.Joints!, j => j.PublicNames.Contains("Articulated.First.Bend"));
        Assert.Equal("Articulated.First.Unit.Elbow", joint.Name);
        var pose = AssemblyMotion.Evaluate(compiled.Ir, "Cycle", 1.25);
        Assert.True(pose.IsSuccess, Evidence(pose.Diagnostics));
        Assert.Equal(225, pose.State[joint.Name], 9);
    }

    [Fact]
    public void InventoryImportsShareOneArticulatedDefinitionWithIndependentPublicChannels()
    {
        var compiled = new AssemblyM1Pipeline().CompileFile(Fixture("articulated-module-motion.firmament"));
        Assert.True(compiled.IsSuccess, Evidence(compiled.Diagnostics));
        var ir = compiled.Ir!;
        Assert.Single(ir.AssemblyDefinitions!);
        Assert.Equal(2, compiled.Geometry!.Artifact.Definitions.Count);
        var pose = AssemblyMotion.Evaluate(ir, "Cycle", .25);
        Assert.True(pose.IsSuccess, Evidence(pose.Diagnostics));
        Assert.Equal(90, pose.State["ModularMotion.First.Elbow"], 9);
        Assert.Equal(-180, pose.State["ModularMotion.Second.Elbow"], 9);
    }

    [Theory]
    [InlineData("from: Shaft", "from: Missing", "assembly-datum-source-unresolved")]
    [InlineData("from: Shaft", "from: Cam", "assembly-datum-dependency-cycle")]
    [InlineData("angle: -45deg", "angle: -45mm", "firmament-v2-field-value-invalid")]
    [InlineData("translateLocal:", "translateLocla:", "firmament-v2-canonical-field-unknown")]
    public void InvalidDerivedFramesAreRejected(string from, string to, string code)
    {
        var source = File.ReadAllText(Fixture("derived-datum-frames.firmament")).Replace(from, to);
        var parsed = new AssemblyM0Parser().Parse(source, "derived-datum-frames.firmament");
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, d => d.Code == code);
    }

    [Fact]
    public void UnusedDerivedDatumStillRequiresAResolvedDeclaredSource()
    {
        var source = File.ReadAllText(Fixture("derived-datum-frames.firmament"))
            .Replace("Concept Struct Layout {", "Concept Struct Layout { DatumFrame Unused { from: Layout.Missing }");
        var parsed = new AssemblyM0Parser().Parse(source);
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, d => d.Code == "assembly-datum-source-unresolved");
    }

    private static string Fixture(string name) => FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/" + name);
    private static string Evidence(IReadOnlyList<AssemblyDiagnostic> diagnostics) => string.Join("\n", diagnostics.Select(d => d.Code + ": " + d.Message));
    private static void AssertMatrix(double[] expected, double[] actual)
    {
        for (var i = 0; i < 16; i++) Assert.Equal(expected[i], actual[i], 9);
    }
}
