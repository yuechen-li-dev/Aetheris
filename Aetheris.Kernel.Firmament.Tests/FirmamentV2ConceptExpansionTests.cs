using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentV2ConceptExpansionTests
{
    [Fact]
    public void ConceptStruct_ResolvesSpatialMembersAndDeterministicGridWithoutMaterializing()
    {
        var parse = FirmamentV2Parser.Parse(Source("Struct"));

        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        var ir = Assert.IsType<ConceptIrDocument>(parse.Document!.ConceptIr);
        var concept = Assert.Single(ir.Concepts);
        Assert.Equal("MountingFrame", concept.Name);
        Assert.Equal("Point3[]", concept.Members["MountPoints"].Type.ToString());
        var conceptStruct = Assert.Single(ir.Structs);
        Assert.False(conceptStruct.Materialized);
        Assert.Equal("CompileTimeOnlyErased", conceptStruct.ErasureStatus);
        var bounds = Assert.IsType<ConceptIrBox3Value>(conceptStruct.Members["Bounds"]);
        Assert.Equal([80d, 50d, 25d], bounds.Size);
        var plane = Assert.IsType<ConceptIrPlaneValue>(conceptStruct.Members["TopPlane"]);
        Assert.Equal(new ConceptIrPoint3(0, 0, 25), plane.Origin);
        Assert.Equal(new ConceptIrVector3(0, 0, 1), plane.Normal);
        var axis = Assert.IsType<ConceptIrAxisValue>(conceptStruct.Members["CenterAxis"]);
        Assert.Equal(new ConceptIrPoint3(0, 0, 12.5), axis.Origin);
        var points = Assert.IsType<ConceptIrPointSetValue>(conceptStruct.Members["MountPoints"]);
        Assert.Equal(2, points.Points.Count);
        Assert.Equal(new ConceptIrPoint3(-30, 0, 25), points.Points[0].Point);
        Assert.Equal(new ConceptIrPoint3(30, 0, 25), points.Points[1].Point);
        Assert.Equal([0, 1], points.Points.Select(p => p.Ordinal));
        Assert.All(points.Points, point => Assert.StartsWith("concept:BracketConcept.MountPoints[", point.StableId, StringComparison.Ordinal));
        Assert.Equal("ErasedBeforeFeatureAir", ir.ErasureStatus);
        Assert.Single(parse.Document.Solids);
    }

    [Theory]
    [InlineData("Struct")]
    [InlineData("Model")]
    public void StructAndModel_NormalizeToSameMaterializedDocument(string spelling)
    {
        var parse = FirmamentV2Parser.Parse(Source(spelling));
        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        Assert.Equal("Bracket", parse.Document!.ModelName);
        Assert.Equal("Box", parse.Document.Solid.RecordType);
        Assert.Equal([80d, 50d, 25d], parse.Document.Solid.Box!.Size);
        Assert.Equal(spelling, parse.Document.ConceptIr!.MaterializedStruct.SourceSpelling);
    }

    [Fact]
    public void StructuralConformance_ReportsMissingMember()
    {
        var source = Source("Struct").Replace("    CenterAxis: Bounds.Center.Axis(+Z)\r\n", string.Empty, StringComparison.Ordinal)
            .Replace("    CenterAxis: Bounds.Center.Axis(+Z)\n", string.Empty, StringComparison.Ordinal);
        var parse = FirmamentV2Parser.Parse(source);
        Assert.False(parse.IsSuccess);
        Assert.Contains(parse.Diagnostics, d => d.StartsWith(ConceptIrResolver.MissingMember + ":BracketConcept.CenterAxis", StringComparison.Ordinal));
    }

    [Fact]
    public void StaticReference_IndexOutOfRange_IsDiagnosedBeforeLowering()
    {
        var source = Source("Struct").Replace("    Box Base {", "    Anchor: BracketConcept.MountPoints[2]\n    Box Base {", StringComparison.Ordinal);
        var parse = FirmamentV2Parser.Parse(source);
        Assert.False(parse.IsSuccess);
        Assert.Contains(parse.Diagnostics, d => d.StartsWith(ConceptIrResolver.IndexOutOfRange + ":BracketConcept.MountPoints[2]", StringComparison.Ordinal));
    }

    [Fact]
    public void ConceptStruct_CircularDependency_IsDiagnosed()
    {
        var source = Source("Struct").Replace("    TopPlane: Bounds.Face(+Z)", "    TopPlane: CenterAxis\n    CenterAxis: TopPlane", StringComparison.Ordinal)
            .Replace("    CenterAxis: Bounds.Center.Axis(+Z)\n", string.Empty, StringComparison.Ordinal);
        var parse = FirmamentV2Parser.Parse(source);
        Assert.False(parse.IsSuccess);
        Assert.Contains(ConceptIrResolver.CircularDependency, parse.Diagnostics);
    }

    [Fact]
    public void MaterializedStruct_RequiresExplicitTypedExposeSurface()
    {
        var valid = FirmamentV2Parser.Parse(Source("Struct"));
        Assert.True(valid.IsSuccess, string.Join(Environment.NewLine, valid.Diagnostics));
        var materialized = valid.Document!.ConceptIr!.MaterializedStruct;
        Assert.Equal("Valid", materialized.Conformance);
        Assert.Equal(["Bounds", "TopPlane", "CenterAxis", "MountPoints"], materialized.ExposedMembers.Select(m => m.Name));
        Assert.Equal(ConceptIrSemanticPhase.FeatureAir, materialized.ExposedMembers.Single(m => m.Name == "TopPlane").Phase);
        Assert.Equal("Base.Top", materialized.ExposedMembers.Single(m => m.Name == "TopPlane").SemanticReference);

        var missing = FirmamentV2Parser.Parse(Source("Struct").Replace(ExposeBlock, string.Empty, StringComparison.Ordinal));
        Assert.False(missing.IsSuccess);
        Assert.Contains(missing.Diagnostics, d => d == ConceptIrResolver.MissingMember + ":Bracket.Bounds");
    }

    [Fact]
    public void MaterializedExpose_DiagnosesDuplicateUnknownMismatchInvalidAndCircularMembers()
    {
        var duplicate = FirmamentV2Parser.Parse(Source("Struct").Replace("Bounds: BracketConcept.Bounds", "Bounds: BracketConcept.Bounds\n                Bounds: BracketConcept.Bounds", StringComparison.Ordinal));
        Assert.Contains(duplicate.Diagnostics, d => d.StartsWith(ConceptIrResolver.DuplicateExposedMember, StringComparison.Ordinal));

        var unknown = FirmamentV2Parser.Parse(Source("Struct").Replace("TopPlane: Base.Top", "TopPlane: Base.Top\n                Extra: Base.Top", StringComparison.Ordinal));
        Assert.Contains(unknown.Diagnostics, d => d == ConceptIrResolver.UnknownMember + ":Bracket.Extra");

        var mismatch = FirmamentV2Parser.Parse(Source("Struct").Replace("TopPlane: Base.Top", "TopPlane: BracketConcept.Bounds", StringComparison.Ordinal));
        Assert.Contains(mismatch.Diagnostics, d => d.StartsWith(ConceptIrResolver.TypeMismatch + ":Bracket.TopPlane", StringComparison.Ordinal));

        var invalid = FirmamentV2Parser.Parse(Source("Struct").Replace("TopPlane: Base.Top", "TopPlane: Base.Bottomless", StringComparison.Ordinal));
        Assert.Contains(invalid.Diagnostics, d => d.StartsWith(ConceptIrResolver.InvalidMaterializedReference + ":TopPlane", StringComparison.Ordinal));

        var unrepresentable = FirmamentV2Parser.Parse(Source("Struct").Replace("TopPlane: Base.Top", "TopPlane: Base.TopFaceId", StringComparison.Ordinal));
        Assert.Contains(unrepresentable.Diagnostics, d => d.StartsWith(ConceptIrResolver.ExposedMemberUnrepresentable + ":TopPlane", StringComparison.Ordinal));

        var circular = FirmamentV2Parser.Parse(Source("Struct").Replace("TopPlane: Base.Top", "TopPlane: CenterAxis", StringComparison.Ordinal)
            .Replace("CenterAxis: BracketConcept.CenterAxis", "CenterAxis: TopPlane", StringComparison.Ordinal));
        Assert.Contains(circular.Diagnostics, d => d.StartsWith(ConceptIrResolver.CircularExposureDependency + ":TopPlane", StringComparison.Ordinal));
    }

    [Fact]
    public void PointIdentity_IsStableWhenUnrelatedConceptDeclarationIsAdded()
    {
        var baseline = FirmamentV2Parser.Parse(Source("Struct")).Document!.ConceptIr!.ResolvedValues.OfType<ConceptIrPoint3Value>().ToArray();
        var changed = FirmamentV2Parser.Parse("Concept Unrelated { Datum: Plane }\n" + Source("Struct")).Document!.ConceptIr!.ResolvedValues.OfType<ConceptIrPoint3Value>().ToArray();
        Assert.Equal(baseline.Select(p => (p.StableId, p.Ordinal, p.Point)), changed.Select(p => (p.StableId, p.Ordinal, p.Point)));
    }

    internal static string Source(string materializedKeyword) => $$"""
        Concept MountingFrame {
            Bounds: Box3
            TopPlane: Plane
            CenterAxis: Axis
            MountPoints: Point3[]
        }

        Concept Struct BracketConcept: MountingFrame {
            Bounds: Box3 {
                Size: [80mm, 50mm, 25mm]
            }
            TopPlane: Bounds.Face(+Z)
            CenterAxis: Bounds.Center.Axis(+Z)
            MountPoints: Grid {
                Within: Bounds.Face(+Z).Inset(10mm)
                Columns: 2
                Rows: 1
            }
        }

        {{materializedKeyword}} Bracket: MountingFrame {
            Box Base {
                Bounds: BracketConcept.Bounds
            }
            Modify Base {
                EdgeFinish TopBreak {
                    Face: BracketConcept.TopPlane
                    Target: Boundary
                    Kind: Chamfer
                    Distance: 1.5mm
                }
            }
            Expose {
                Bounds: BracketConcept.Bounds
                TopPlane: Base.Top
                CenterAxis: BracketConcept.CenterAxis
                MountPoints: BracketConcept.MountPoints
            }
        }
        """;

    private const string ExposeBlock = """
            Expose {
                Bounds: BracketConcept.Bounds
                TopPlane: Base.Top
                CenterAxis: BracketConcept.CenterAxis
                MountPoints: BracketConcept.MountPoints
            }
        """;
}
