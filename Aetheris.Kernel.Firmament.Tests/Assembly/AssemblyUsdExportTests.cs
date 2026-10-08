using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class AssemblyUsdExportTests
{
    [Fact]
    public void IndustrialSplineHousingsHaveClosedMeshesAndOutwardNormals()
    {
        var compiled = Compile("IndustrialAtlas/atlas-industrial.firmament");
        var mesh = AssemblyDisplayMeshExporter.Export(compiled);
        var housings = mesh.Definitions.Where(d => d.Identity.StartsWith("SectionChainFile", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, housings.Length);
        foreach (var housing in housings)
        {
            Assert.Equal("StructuredSpline", housing.MeshPipeline);
            var edges = new Dictionary<(string, string), int>();
            string Point(int i) => string.Join(",", housing.Positions.Skip(i * 3).Take(3).Select(v => Math.Round(v, 7).ToString("G17", System.Globalization.CultureInfo.InvariantCulture)));
            Aetheris.Kernel.Core.Math.Point3D Position(int i) => new(housing.Positions[i*3],housing.Positions[i*3+1],housing.Positions[i*3+2]);
            var volume = 0d;
            for (var t = 0; t < housing.Indices.Length; t += 3)
            {
                var ids = housing.Indices.Skip(t).Take(3).ToArray();
                for (var i = 0; i < 3; i++)
                {
                    var a = Point(ids[i]); var b = Point(ids[(i+1)%3]);
                    var edge = string.CompareOrdinal(a,b) < 0 ? (a,b) : (b,a);
                    edges[edge] = edges.GetValueOrDefault(edge) + 1;
                }
                var pa = Position(ids[0]); var pb = Position(ids[1]); var pc = Position(ids[2]);
                var cross = (pb-pa).Cross(pc-pa);
                var normal = new Aetheris.Kernel.Core.Math.Vector3D(housing.Normals[ids[0]*3],housing.Normals[ids[0]*3+1],housing.Normals[ids[0]*3+2]);
                Assert.True(cross.Dot(normal) > 0, "Triangle winding must agree with the outward face normal.");
                volume += (pa - new Aetheris.Kernel.Core.Math.Point3D(0,0,0)).Dot((pb-new Aetheris.Kernel.Core.Math.Point3D(0,0,0)).Cross(pc-new Aetheris.Kernel.Core.Math.Point3D(0,0,0))) / 6;
            }
            Assert.All(edges.Values, uses => Assert.Equal(2, uses));
            Assert.True(volume > 0, "Closed product mesh must enclose positive volume.");
        }
        Assert.Equal(24, mesh.Definitions.Count);
        Assert.Equal(60, compiled.Geometry!.InstanceBodies.Count);
        Assert.Equal(5, compiled.Ir!.Joints!.Count);
    }

    [Fact]
    public void PosedArmReusesGeometryAndSerializesDeterministicallyAcrossCultures()
    {
        var compiled = Compile("two-link-arm-usd.firmament");
        var state = new Dictionary<string, double> { ["Shoulder"] = 35, ["Elbow"] = 65 };
        var pose = AssemblyKinematics.Evaluate(compiled.Ir!, state);
        var mesh = AssemblyDisplayMeshExporter.Export(compiled, pose: pose);
        var options = new AssemblyUsdOptions(state, [new(0, new Dictionary<string, double>()), new(48, state)]);
        var source = AssemblyUsdExporter.Serialize(compiled.Ir!, mesh, options, pose.State);
        var originalCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(source, AssemblyUsdExporter.Export(compiled, options));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = originalCulture; }
        Assert.Equal(2, mesh.Definitions.Count);
        Assert.Contains("metersPerUnit = 0.001", source);
        Assert.Contains("upAxis = \"Z\"", source);
        Assert.Contains("PhysicsRevoluteJoint", source);
        Assert.Contains("quatd xformOp:orient.timeSamples", source);
        Assert.DoesNotContain("matrix4d xformOp:transform.timeSamples", source);
        Assert.Equal(3, source.Split("instanceable = true").Length - 1);
    }

    [Fact]
    public void ImportedSliderAndNestedDemoKeepEveryAcceptedJointFamily()
    {
        var slider = Compile("external-step-slider.firmament");
        Assert.Single(slider.Geometry!.DefinitionBodies);
        Assert.Contains("PhysicsPrismaticJoint", AssemblyUsdExporter.Export(slider, new(new Dictionary<string, double> { ["Travel"] = 24 })));
        var occtSlider = Compile("external-occt-slider-usd.firmament");
        Assert.Single(occtSlider.Geometry!.DefinitionBodies);
        Assert.Contains("PhysicsPrismaticJoint", AssemblyUsdExporter.Export(occtSlider));
        var fixedNested = Compile("fixed-nested-usd.firmament");
        Assert.Single(fixedNested.Geometry!.DefinitionBodies);
        Assert.Contains("PhysicsFixedJoint", AssemblyUsdExporter.Export(fixedNested));
        var demo = Compile("physical-ai-demo.firmament");
        var usd = AssemblyUsdExporter.Export(demo);
        Assert.Contains("PhysicsFixedJoint", usd);
        Assert.Contains("PhysicsRevoluteJoint", usd);
        Assert.Contains("PhysicsPrismaticJoint", usd);
        Assert.Contains("aetheris:sourcePath = \"Atlas.UpperArm.Housing\"", usd);
        Assert.Equal(6, demo.Geometry!.DefinitionBodies.Count);
        Assert.Equal(8, demo.Ir!.Joints!.Count);
    }

    [Fact]
    public void InvalidStateSamplesUnitsAndReflectionFailBeforeSerialization()
    {
        var compiled = Compile("two-link-arm-usd.firmament");
        Assert.Throws<InvalidOperationException>(() => AssemblyUsdExporter.Export(compiled, new(new Dictionary<string, double> { ["Typo"] = 1 })));
        Assert.Throws<InvalidOperationException>(() => AssemblyUsdExporter.Export(compiled, new(Samples: [new(0, new Dictionary<string, double>()), new(0, new Dictionary<string, double>())])));
        Assert.Throws<InvalidOperationException>(() => AssemblyUsdExporter.Export(compiled, new(Samples: [new(0, new Dictionary<string, double> { ["Shoulder"] = double.NaN })])));
        var mesh = AssemblyDisplayMeshExporter.Export(compiled);
        Assert.Throws<InvalidOperationException>(() => AssemblyUsdExporter.Serialize(compiled.Ir!, mesh with { Units = "m" }));
        Assert.Throws<InvalidOperationException>(() => AssemblyUsdExporter.Serialize(compiled.Ir!, mesh, new(new Dictionary<string, double> { ["Shoulder"] = 45 })));
        Assert.Throws<InvalidOperationException>(() => AssemblyUsdExporter.Export(compiled,
            new(DefinitionMaterials: new Dictionary<string, AssemblyUsdMaterial> { ["Typo"] = new() })));
        var reflected = mesh.Occurrences.Select((o, i) => i != 0 ? o : o with { Transform = [-1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1] }).ToArray();
        Assert.Throws<InvalidOperationException>(() => AssemblyUsdExporter.Serialize(compiled.Ir!, mesh with { Occurrences = reflected }));
    }

    private static AssemblyM1CompilationResult Compile(string fixture)
    {
        var result = new AssemblyM1Pipeline().CompileFile(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/" + fixture));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        return result;
    }
}
