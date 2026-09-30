using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class GuitarSurfacingWitnessTests
{
    [Fact]
    public void SingleCutCarveAndBackExportClosedOrientedMeshesThroughOrdinaryAssembly()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath(
            "fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar-x0.firmament");
        var compiled = new AssemblyM1Pipeline().CompileFile(path);
        Assert.True(compiled.IsSuccess, string.Join("\n", compiled.Diagnostics.Select(d => d.Message)));
        var mesh = AssemblyDisplayMeshExporter.Export(compiled);
        Assert.Equal(107, mesh.Occurrences.Count(o => o.DefinitionId is not null));
        Assert.Equal(7, compiled.Ir!.Patterns!.Count);
        Assert.Single(compiled.Ir.Joints!);
        Assert.DoesNotContain(File.ReadAllText(path), "LegacyExplicit");
        Assert.Contains(compiled.Ir.Instances, i => i.Path.ToString() == "GuitarX0.TunerPosts.TunerPost00.TunerPost"
            && i.Provenance.Any(p => p.Stage == "assembly-pattern"));
        var head = compiled.Ir.Instances.Single(i => i.Path.ToString() == "GuitarX0.Headstock");
        Assert.Equal(660, head.ResolvedTransform!.Matrix[13], 6);
        Assert.Equal(48, head.ResolvedTransform.Matrix[14], 6);
        Assert.Equal(6, mesh.Definitions.Count(d => d.Identity.StartsWith("GuitarString<", StringComparison.Ordinal)));
        var surfaced = mesh.Definitions.Where(d => d.Identity.StartsWith("SectionChainFile", StringComparison.Ordinal)).ToArray();
        Assert.Equal(4, surfaced.Length);
        var strings = mesh.Definitions.Where(d => d.Identity.StartsWith("GuitarString<", StringComparison.Ordinal)).ToArray();
        Assert.All(strings, d => Assert.Equal("SurfaceMeshIr", d.MeshPipeline));
        foreach (var definition in surfaced.Concat(strings))
        {
            if (definition.Identity.StartsWith("SectionChainFile", StringComparison.Ordinal))
                Assert.Equal("StructuredSpline", definition.MeshPipeline);
            Point3D Position(int i) => new(definition.Positions[i * 3], definition.Positions[i * 3 + 1], definition.Positions[i * 3 + 2]);
            string Key(int i)
            {
                var p = Position(i);
                return FormattableString.Invariant($"{p.X:F6},{p.Y:F6},{p.Z:F6}");
            }
            var edges = new Dictionary<(string, string), int>();
            double volume = 0;
            for (var i = 0; i < definition.Indices.Length; i += 3)
            {
                var ids = definition.Indices.Skip(i).Take(3).ToArray();
                var a = Position(ids[0]); var b = Position(ids[1]); var c = Position(ids[2]);
                var cross = (b - a).Cross(c - a);
                var normal = new Vector3D(definition.Normals[ids[0] * 3], definition.Normals[ids[0] * 3 + 1], definition.Normals[ids[0] * 3 + 2]);
                Assert.True(cross.Dot(normal) > 0, $"{definition.Identity}: triangle {i / 3} has normal dot {cross.Dot(normal):R}; every triangle must agree with its outward face normal.");
                volume += (a - new Point3D(0, 0, 0)).Dot((b - new Point3D(0, 0, 0)).Cross(c - new Point3D(0, 0, 0))) / 6;
                for (var j = 0; j < 3; j++)
                {
                    var from = Key(ids[j]); var to = Key(ids[(j + 1) % 3]);
                    var edge = string.CompareOrdinal(from, to) < 0 ? (from, to) : (to, from);
                    edges[edge] = edges.GetValueOrDefault(edge) + 1;
                }
            }
            Assert.All(edges.Values, uses => Assert.Equal(2, uses));
            Assert.True(volume > 0, "Each exact part must retain a closed positive-volume display witness.");
        }
        var top = Assert.Single(surfaced.Where(d => d.Identity.Contains("CarvedMaple", StringComparison.Ordinal)));
        var heights = top.Positions.Where((_, i) => i % 3 == 2).ToArray();
        Assert.Equal(40, heights.Min(), 6);
        Assert.Equal(53, heights.Max(), 6);
        Assert.True(heights.Any(z => z > 44 && z < 50), "The top must have real intermediate surface height.");
        var neck = Assert.Single(surfaced.Where(d => d.Identity.Contains("Neck.firmament", StringComparison.Ordinal)));
        var neckPoints = Enumerable.Range(0, neck.Positions.Length / 3)
            .Select(i => new Point3D(neck.Positions[i * 3], neck.Positions[i * 3 + 1], neck.Positions[i * 3 + 2])).ToArray();
        Assert.All(neckPoints, p => Assert.True(p.Z <= 55.000001, "Neck must stay below the flat fretboard seat."));
        var nutPoints = neckPoints.Where(p => System.Math.Abs(p.Y - 660) < 0.000001).ToArray();
        Assert.NotEmpty(nutPoints);
        Assert.Equal(48, nutPoints.Min(p => p.Z), 6);
        Assert.Equal(55, nutPoints.Max(p => p.Z), 6);
        Assert.True(neckPoints.Any(p => p.Y < 200 && p.Z < 20), "The rounded heel must extend into the body back.");
        foreach (var p in neckPoints.Where(p => p.Y >= 240))
        {
            var straightBack = 42 + (p.Y - 240) / 70;
            Assert.True(p.Z >= straightBack - 0.002, "The main neck must not bulge behind its straight tapered back.");
            // The nut cap legitimately contains interior points between back and seat.
            if (p.Y < 659.999999 && System.Math.Abs(p.X) < 0.000001)
                Assert.True(System.Math.Abs(p.Z - straightBack) < 0.002 || System.Math.Abs(p.Z - 55) < 0.002,
                    "Centerline samples must lie on the straight back or flat seat.");
        }
        Assert.Contains("subdivisionScheme = \"none\"", AssemblyUsdExporter.Serialize(compiled.Ir!, mesh));
    }
}
