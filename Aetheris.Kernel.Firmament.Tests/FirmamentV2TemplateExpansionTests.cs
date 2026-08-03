using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentV2TemplateExpansionTests
{
    [Theory]
    [InlineData("CompactBracket", "60mm", "40mm", "20mm", "Compact", 60d, 40d, 20d)]
    [InlineData("StandardBracket", "80mm", "50mm", "25mm", "Standard", 80d, 50d, 25d)]
    public void TemplateStruct_MonomorphizesBeforeConceptIrAndFeatureAir(string instance, string width, string depth, string height, string variant, double x, double y, double z)
    {
        var parse = FirmamentV2Parser.Parse(Source(instance, width, depth, height, variant));

        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        var ir = Assert.IsType<ConceptIrDocument>(parse.Document!.ConceptIr);
        var expansion = Assert.Single(ir.TemplateInstantiations!);
        Assert.Equal("MountingBracket", expansion.Template);
        Assert.Equal(instance, expansion.Instance);
        Assert.Equal("Box", expansion.TypeArguments["TBody"]);
        Assert.Equal(width, expansion.ValueArguments["Width"]);
        Assert.Equal(variant, expansion.ValueArguments["Variant"]);
        Assert.Equal("ExpandedBeforeFeatureAir", expansion.Status);
        Assert.Contains(instance + "::Design", expansion.GeneratedDeclarations);
        Assert.Equal("ErasedBeforeFeatureAir", ir.ErasureStatus);
        Assert.Equal([x, y, z], parse.Document.Solid.Box!.Size);
        Assert.DoesNotContain("Template", parse.Document.ConceptIr.MaterializedStruct.SourceSpelling, StringComparison.Ordinal);
        Assert.Contains("firmament-template-expanded-before-feature-air", parse.Diagnostics);
        Assert.Equal(variant, expansion.SelectedMatchArms!["Variant"]);
        Assert.Empty(ir.StaticSelections!); // the selected Match was erased before Concept IR's static evaluator.
    }

    [Fact]
    public void TemplateBinding_RejectsMissingUnknownMismatchConstraintAndRequire()
    {
        var missing = FirmamentV2Parser.Parse(Source("Bad", "", "40mm", "20mm", "Compact"));
        Assert.Contains(missing.Diagnostics, d => d.StartsWith("firmament-template-missing-required-argument:Width", StringComparison.Ordinal));
        var unknown = FirmamentV2Parser.Parse(Source("Bad", "60mm", "40mm", "20mm", "Compact").Replace("Variant: Compact", "Nope: Compact", StringComparison.Ordinal));
        Assert.Contains(unknown.Diagnostics, d => d.StartsWith("firmament-template-unknown-argument:Nope", StringComparison.Ordinal));
        var mismatch = FirmamentV2Parser.Parse(Source("Bad", "60", "40mm", "20mm", "Compact"));
        Assert.Contains(mismatch.Diagnostics, d => d.StartsWith("firmament-template-value-argument-type-mismatch:Width", StringComparison.Ordinal));
        var constraint = FirmamentV2Parser.Parse(Source("Bad", "60mm", "40mm", "20mm", "Compact").Replace("TBody: Box", "TBody: Sphere", StringComparison.Ordinal));
        Assert.Contains(constraint.Diagnostics, d => d.StartsWith("firmament-template-type-argument-does-not-satisfy-concept:TBody", StringComparison.Ordinal));
        var require = FirmamentV2Parser.Parse(Source("Bad", "0mm", "40mm", "20mm", "Compact"));
        Assert.Contains(require.Diagnostics, d => d.StartsWith("firmament-template-require-failed:Bad.ValidDimensions", StringComparison.Ordinal));
    }

    [Fact]
    public void TemplateBinding_AppliesTypedEnumDefault()
    {
        var source = Source("DefaultBracket", "80mm", "50mm", "25mm", "Standard").Replace(", Variant: Standard", string.Empty, StringComparison.Ordinal);
        var parse = FirmamentV2Parser.Parse(source);
        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        var expansion = Assert.Single(parse.Document!.ConceptIr!.TemplateInstantiations!);
        Assert.Equal("Standard", expansion.ValueArguments["Variant"]);
        Assert.Contains("Variant", expansion.DefaultedArguments);
        Assert.Equal("Standard", expansion.SelectedMatchArms!["Variant"]);
    }

    [Fact]
    public void TemplateBinding_RejectsBadDefaultsDefaultCyclesNonBoolRequiresAndRecursiveSpecializations()
    {
        var badDefault = FirmamentV2Parser.Parse("""
            Template < Width: Length = Nope > Struct A { }
            Struct X = A <>
            """);
        Assert.Contains(badDefault.Diagnostics, d => d.StartsWith("firmament-template-default-value-type-mismatch:Width", StringComparison.Ordinal));
        var defaultCycle = FirmamentV2Parser.Parse("""
            Template < Width: Length = Depth, Depth: Length = Width > Struct A { }
            Struct X = A <>
            """);
        Assert.Contains(defaultCycle.Diagnostics, d => d.StartsWith("firmament-template-default-dependency-cycle:Width -> Depth -> Width", StringComparison.Ordinal));
        var nonBool = FirmamentV2Parser.Parse("""
            Template < Width: Length > Struct A { Require Broken => Width }
            Struct X = A < Width: 1mm >
            """);
        Assert.Contains(nonBool.Diagnostics, d => d.StartsWith("firmament-template-require-non-bool:Broken", StringComparison.Ordinal));
        var recursive = FirmamentV2Parser.Parse("""
            Template <> Struct A { Struct Nested = A <> }
            Struct X = A <>
            """);
        Assert.Contains(recursive.Diagnostics, d => d.StartsWith("firmament-template-recursive-specialization:A -> A", StringComparison.Ordinal));
    }

    private static string Source(string instance, string width, string depth, string height, string variant) => $$"""
        Concept MountingFrame {
            Bounds: Box3
            TopPlane: Plane
            CenterAxis: Axis
            ChamferDistance: Length
        }
        Concept PrismaticBody {
            Bounds: Box3
            TopPlane: Plane
        }
        Enum BracketVariant {
            Compact
            Standard
        }
        Template < type TBody satisfies PrismaticBody, Width: Length, Depth: Length, Height: Length, Variant: BracketVariant = Standard >
        Struct MountingBracket: MountingFrame {
            Require ValidDimensions => Width > 0mm && Depth > 0mm && Height > 0mm
            Concept Struct Design: MountingFrame {
                Bounds: Box3 {
                    Size: [Width, Depth, Height]
                }
                TopPlane: Bounds.Face(+Z)
                CenterAxis: Bounds.Center.Axis(+Z)
                ChamferDistance: Match Variant {
                    Compact => 1mm
                    Standard => 1.5mm
                }
            }
            Box Base {
                Bounds: Design.Bounds
            }
            Modify Base {
                EdgeFinish TopBreak {
                    Face: Design.TopPlane
                    Target: Boundary
                    Kind: Chamfer
                    Distance: Design.ChamferDistance
                }
            }
            Expose {
                Bounds: Design.Bounds
                TopPlane: Base.Top
                CenterAxis: Design.CenterAxis
                ChamferDistance: Design.ChamferDistance
            }
        }
        Struct {{instance}} = MountingBracket < TBody: Box, {{(width.Length == 0 ? string.Empty : "Width: " + width + ",")}} Depth: {{depth}}, Height: {{height}}, Variant: {{variant}} >
        """;
}
