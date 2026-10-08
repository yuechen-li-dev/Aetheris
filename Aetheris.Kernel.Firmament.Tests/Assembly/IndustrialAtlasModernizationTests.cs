using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class IndustrialAtlasModernizationTests
{
    [Fact]
    public void CoversAndJawsHaveSeparatedVolumesAndInwardFacingPads()
    {
        var compiled = Compile();
        var display = AssemblyDisplayMeshExporter.Export(compiled);
        Assert.Equal(60, compiled.Geometry!.InstanceBodies.Count);
        Assert.Equal(24, display.Definitions.Count);
        Assert.Equal(new[] { "Elbow", "GripLeft", "GripRight", "Shoulder", "WristMount" },
            compiled.Ir!.Joints!.Select(joint => joint.Name).OrderBy(name => name));
        Assert.DoesNotContain(compiled.Ir.Instances, instance => instance.Kind == AssemblyInstanceKind.Part
            && instance.PlacementAuthority == PlacementAuthority.LegacyExplicit);
        foreach (var (arm, pattern, plate) in new[] {
            ("UpperArm", "UpperCoverFasteners", "ServicePanel"),
            ("ForeArm", "ForeCoverFasteners", "MakerPlate") })
        {
            var cover = Bounds(display, $"AtlasIndustrial.{arm}.{plate}");
            foreach (var key in new[] { "RearLow", "FrontLow", "RearHigh", "FrontHigh" })
            {
                var path = $"AtlasIndustrial.{arm}.{pattern}.{key}.Fastener";
                var shaft = Bounds(display, path + ".Standoff");
                var head = Bounds(display, path + ".Head");
                Assert.Equal(cover.Max.Y, shaft.Min.Y, 7);
                Assert.Equal(cover.Min.Y, head.Max.Y, 7);
            }
        }
        var body = Bounds(display, "AtlasIndustrial.Gripper");
        var leftJaw = Bounds(display, "AtlasIndustrial.LeftJaw.Jaw");
        var rightJaw = Bounds(display, "AtlasIndustrial.RightJaw.Jaw");
        Assert.Equal(body.Max.X, leftJaw.Min.X, 7);
        Assert.Equal(body.Max.X, rightJaw.Min.X, 7);
        var leftPad = Bounds(display, "AtlasIndustrial.LeftJaw.Pad");
        var rightPad = Bounds(display, "AtlasIndustrial.RightJaw.Pad");
        Assert.Equal(leftJaw.Min.Y, leftPad.Max.Y, 7);
        Assert.Equal(rightJaw.Max.Y, rightPad.Min.Y, 7);
        Assert.Equal(21, leftPad.Min.Y - rightPad.Max.Y, 7);
    }

    [Fact]
    public void ArticulatedComponentsRetainRigidChildFramesAcrossPoses()
    {
        var compiled = Compile();
        var display = AssemblyDisplayMeshExporter.Export(compiled);
        foreach (var state in new[] {
            new Dictionary<string, double> { ["Shoulder"] = -65, ["Elbow"] = 100, ["GripLeft"] = 4, ["GripRight"] = 4 },
            new Dictionary<string, double> { ["Shoulder"] = -75, ["Elbow"] = -140, ["GripLeft"] = 10, ["GripRight"] = 10 } })
        {
            var pose = AssemblyKinematics.Evaluate(compiled.Ir!, state);
            Assert.True(pose.IsSuccess, string.Join("\n", pose.Diagnostics));
            var posed = AssemblyDisplayMeshExporter.WithPose(display, pose);
            Assert.Same(display.Definitions, posed.Definitions);
            var bodyFrame = Transform3D.FromRowMajor(posed.Occurrences.Single(o => o.Path == "AtlasIndustrial.Gripper").Transform);
            foreach (var (jaw, direction) in new[] { ("LeftJaw", 1), ("RightJaw", -1) })
            {
                var jawFrame = Transform3D.FromRowMajor(posed.Occurrences.Single(o => o.Path == $"AtlasIndustrial.{jaw}.Jaw").Transform);
                var relative = (jawFrame * bodyFrame.Inverse()).ToRowMajor();
                Assert.Equal(30, relative[12], 7);
                Assert.Equal(direction * (16 + state["Grip" + (direction == 1 ? "Left" : "Right")]), relative[13], 7);
                Assert.Equal(9, relative[14], 7);
            }
            foreach (var root in new[] { "UpperArm", "ForeArm", "LeftJaw", "RightJaw" })
            {
                var path = "AtlasIndustrial." + root;
                Transform3D Frame(AssemblyDisplayMeshDocument document, string name) =>
                    Transform3D.FromRowMajor(document.Occurrences.Single(occurrence => occurrence.Path == name).Transform);
                foreach (var child in display.Occurrences.Where(occurrence => occurrence.DefinitionId is not null && occurrence.Path.StartsWith(path + ".", StringComparison.Ordinal)))
                {
                    var before = (Frame(display, child.Path) * Frame(display, path).Inverse()).ToRowMajor();
                    var after = (Frame(posed, child.Path) * Frame(posed, path).Inverse()).ToRowMajor();
                    Assert.All(before.Zip(after), pair => Assert.Equal(pair.First, pair.Second, 8));
                }
            }
        }
    }

    private static AssemblyM1CompilationResult Compile()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/IndustrialAtlas/atlas-industrial.firmament");
        var result = new AssemblyM1Pipeline().CompileFile(path);
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        return result;
    }

    private static (Point3D Min, Point3D Max) Bounds(AssemblyDisplayMeshDocument document, string path)
    {
        var occurrence = document.Occurrences.Single(item => item.Path == path);
        var definition = document.Definitions.Single(item => item.Id == occurrence.DefinitionId);
        var transform = Transform3D.FromRowMajor(occurrence.Transform);
        var points = Enumerable.Range(0, definition.Positions.Length / 3).Select(index => transform.Apply(
            new Point3D(definition.Positions[index * 3], definition.Positions[index * 3 + 1], definition.Positions[index * 3 + 2]))).ToArray();
        return (new(points.Min(p => p.X), points.Min(p => p.Y), points.Min(p => p.Z)),
            new(points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z)));
    }
}
