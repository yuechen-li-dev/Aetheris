using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Core.Brep.Tessellation;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class AssemblyDisplayMeshTests
{
    [Fact]
    public void AnnularDefinitionsRetainSeamsCapsIdentityAndDeterministicMeshes()
    {
        var fixture = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/Assembly/annular-pair.firmament");
        var compilation = new AssemblyM1Pipeline().CompileFile(fixture);
        Assert.True(compilation.IsSuccess, string.Join("\n", compilation.Diagnostics.Select(d => d.Message)));
        var body = Assert.Single(compilation.Geometry!.DefinitionBodies).Value;
        Assert.Equal(4, body.Topology.Vertices.Count());
        var tessellation = BrepDisplayTessellator.Tessellate(body);
        Assert.True(tessellation.IsSuccess);
        Assert.All(tessellation.Value.FacePatches, face => Assert.NotEmpty(face.TriangleIndices));
        var document = AssemblyDisplayMeshExporter.Export(compilation);
        var definition = Assert.Single(document.Definitions);
        double volume = 0;
        for (var i = 0; i < definition.Indices.Length; i += 3)
        {
            Aetheris.Kernel.Core.Math.Vector3D Position(int index) => new(definition.Positions[index * 3], definition.Positions[index * 3 + 1], definition.Positions[index * 3 + 2]);
            var a = Position(definition.Indices[i]); var b = Position(definition.Indices[i + 1]); var c = Position(definition.Indices[i + 2]);
            volume += a.Dot(b.Cross(c)) / 6;
        }
        var expectedVolume = double.Pi * (20 * 20 - 15 * 15) * 20;
        Assert.InRange(volume / expectedVolume, .98, 1.02);
        Assert.Equal(2, document.Occurrences.Count(o => o.DefinitionId == definition.Id));
        Assert.Equal(60, document.Occurrences.Single(o => o.Path == "AnnularPair.Second").Transform[12]);
        var second = AssemblyDisplayMeshExporter.Export(new AssemblyM1Pipeline().CompileFile(fixture));
        Assert.Equal(AssemblyDisplayMeshExporter.Serialize(document), AssemblyDisplayMeshExporter.Serialize(second));
        Assert.Throws<InvalidOperationException>(() => AssemblyDisplayMeshExporter.Export(compilation with { Geometry = null }));
    }

    [Fact]
    public void ExplicitObliquePlacementKeepsDoublePrecision()
    {
        var fixture = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/Assembly/annular-pair.firmament");
        var source = File.ReadAllText(fixture).Replace("1,0,0,0,0,1,0,0,0,0,1,0,60,0,0,1", "0.7071067811865476,0.7071067811865475,0,0,-0.7071067811865475,0.7071067811865476,0,0,0,0,1,0,60,0,0,1", StringComparison.Ordinal);
        var compilation = new AssemblyM1Pipeline().Compile(source, fixture);
        Assert.True(compilation.IsSuccess, string.Join("\n", compilation.Diagnostics.Select(d => d.Message)));
        var transform = compilation.Ir!.Instances.Single(i => i.Path.ToString() == "AnnularPair.Second").ResolvedTransform!.Matrix;
        Assert.Equal(double.Cos(double.Pi / 4), transform[0], 14);
        Assert.True(Aetheris.Kernel.Core.Math.Transform3D.FromRowMajor(transform).IsRigid());
    }

    [Fact]
    public void ObliqueFrameMateRetainsPrecisionThroughMaterializedResiduals()
    {
        var fixture = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/Assembly/annular-pair.firmament");
        var source = File.ReadAllText(fixture);
        source = source[..source.IndexOf("Assembly AnnularPair", StringComparison.Ordinal)] + """
            Interface RegisteredSeat {
                Role Moving requires DatumFrameCapable;
                Role Fixed requires DatumFrameCapable;
                Lower FrameCoincident Moving Fixed SameDirection;
            }
            Assembly Oblique {
                <Assembly Oblique>
                    <Part First = Sleeve<R: 20mm>>
                        Semantic Origin { DatumFrame Frame = [0,0,0] x [1,0,0] y [0,1,0] z [0,0,1]; }
                        Semantic Seat { DatumFrame Frame = [60,0,0] x [0.7648421872844885,-0.644217687237691,0] y [0.644217687237691,0.7648421872844885,0] z [0,0,1]; }
                    </Part>
                    <Part Second = Sleeve<R: 20mm>>
                        Semantic Mount { DatumFrame Frame = [0,0,0] x [1,0,0] y [0,1,0] z [0,0,1]; }
                    </Part>
                </Assembly>
                Anchor: Oblique.First.Origin;
                Mate Install: RegisteredSeat { Moving: Oblique.Second.Mount; Fixed: Oblique.First.Seat; }
            }
            """;
        var compilation = new AssemblyM1Pipeline().Compile(source, fixture);
        Assert.True(compilation.IsSuccess, string.Join("\n", compilation.Diagnostics.Select(d => d.Message)));
        var residual = Assert.Single(compilation.Geometry!.Artifact.MateResiduals);
        Assert.True(residual.Passed);
        Assert.InRange(residual.AngularResidualRadians, 0, 1e-14);
        var document = AssemblyDisplayMeshExporter.Export(compilation);
        Assert.Equal(double.Cos(.7), document.Occurrences.Single(o => o.Path == "Oblique.Second").Transform[0], 14);
    }
}
