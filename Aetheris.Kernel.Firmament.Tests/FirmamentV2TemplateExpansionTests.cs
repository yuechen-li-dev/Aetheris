using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentV2TemplateExpansionTests
{
    [Fact]
    public void CanonicalFiniteFeatureTemplate_UsesAngleBrackets_AndLegacyCallFormRemainsCompatible()
    {
        const string canonical = """
            Model CanonicalFeatureTemplate {
                Units: mm
                Record MountSpec { Center: Point2 Diameter: Length }
                Static Mounts: MountSpec[] = [ MountSpec { Center: Point2(0mm, 0mm) Diameter: 6mm } ]
                Template<spec: MountSpec> MountHole {
                    Hole<Shaft> Mount { On: +Z Center: spec.Center Diameter: spec.Diameter End: ThroughAll }
                }
                Box Plate { Size: [30mm, 20mm, 5mm] }
                Modify Plate { Pattern MountsPattern Over Mounts { MountHole<Current> } }
            }
            """;
        var parsed = FirmamentV2Parser.Parse(canonical);
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        Assert.Equal("MountHole", Assert.Single(parsed.Document!.StaticAuthoring!.Templates).Name);

        var legacy = canonical.Replace("Template<spec: MountSpec> MountHole", "Template MountHole(MountSpec spec)", StringComparison.Ordinal)
            .Replace("MountHole<Current>", "MountHole(Current)", StringComparison.Ordinal);
        Assert.True(FirmamentV2Parser.Parse(legacy).IsSuccess);
    }

    [Fact]
    public void TypedRecordParameter_BindsStaticRecordMembersAndRequireBeforeAir()
    {
        var first = FirmamentV2Parser.Parse(WidgetSource("WidgetA"));
        var second = FirmamentV2Parser.Parse(WidgetSource("WidgetA"));

        Assert.True(first.IsSuccess, string.Join(Environment.NewLine, first.Diagnostics));
        Assert.True(second.IsSuccess, string.Join(Environment.NewLine, second.Diagnostics));
        var expansion = Assert.Single(first.Document!.ConceptIr!.TemplateInstantiations!);
        var record = Assert.Single(expansion.RecordArguments!);
        Assert.Equal("Spec", record.Key);
        Assert.Equal("WidgetSpec", record.Value.RecordType);
        Assert.Equal("TallWidget", record.Value.StaticValue);
        Assert.Equal("25mm", record.Value.Members["Height"]);
        Assert.Equal("StaticRecord", record.Value.Provenance);
        Assert.Equal("Passed:40mm > 0mm && 25mm > 0mm", expansion.RequireResults!["Positive"]);
        Assert.Equal(expansion.SpecializationIdentity, Assert.Single(second.Document!.ConceptIr!.TemplateInstantiations!).SpecializationIdentity);
        Assert.Equal([40d, 20d, 25d], first.Document.Solid.Box!.Size);
        Assert.DoesNotContain("Static", first.Document.ConceptIr.MaterializedStruct.SourceSpelling, StringComparison.Ordinal);
        Assert.DoesNotContain("Template", first.Document.ConceptIr.MaterializedStruct.SourceSpelling, StringComparison.Ordinal);
    }

    [Fact]
    public void TypedRecordParameter_ReportsTypedBindingAndMemberDiagnostics()
    {
        var wrong = FirmamentV2Parser.Parse(WidgetSource("Bad").Replace("Spec: TallWidget", "Spec: OtherValue", StringComparison.Ordinal) + "\nRecord OtherSpec { Width: Length Height: Length Depth: Length }\nStatic OtherValue: OtherSpec = OtherSpec { Width: 1mm Height: 1mm Depth: 1mm }");
        Assert.Contains(wrong.Diagnostics, d => d.StartsWith("firmament-template-record-argument-type-mismatch:Spec:expected-WidgetSpec:actual-OtherSpec", StringComparison.Ordinal));

        var unknown = FirmamentV2Parser.Parse(WidgetSource("Bad").Replace("Spec: TallWidget", "Spec: Missing", StringComparison.Ordinal));
        Assert.Contains(unknown.Diagnostics, d => d.StartsWith("firmament-template-unknown-static-record-value:Spec:Missing", StringComparison.Ordinal));

        var collection = WidgetSource("Bad").Replace("Spec: TallWidget", "Spec: WidgetValues", StringComparison.Ordinal)
            + "\nStatic WidgetValues: WidgetSpec[] = [WidgetSpec { Width: 40mm Height: 25mm Depth: 20mm }]";
        var collectionParse = FirmamentV2Parser.Parse(collection);
        Assert.Contains(collectionParse.Diagnostics, d => d.StartsWith("firmament-template-record-collection-scalar-mismatch:Spec", StringComparison.Ordinal));

        var member = FirmamentV2Parser.Parse(WidgetSource("Bad").Replace("Spec.Height", "Spec.MissingHeight", StringComparison.Ordinal));
        Assert.Contains(member.Diagnostics, d => d.StartsWith("firmament-template-unknown-record-member:Spec.MissingHeight", StringComparison.Ordinal));

        var runtime = FirmamentV2Parser.Parse(WidgetSource("Bad").Replace("Spec: TallWidget", "Spec: RuntimeWidget", StringComparison.Ordinal) + "\nStruct RuntimeWidget { Box Runtime { Size: [1mm, 1mm, 1mm] } }");
        Assert.Contains(runtime.Diagnostics, d => d.StartsWith("firmament-template-materialized-value-not-compile-time-record:Spec:RuntimeWidget", StringComparison.Ordinal));
    }

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
        Assert.Contains(missing.Diagnostics, d => d.Contains("expected-signature:MountingBracket<", StringComparison.Ordinal));
        var unknown = FirmamentV2Parser.Parse(Source("Bad", "60mm", "40mm", "20mm", "Compact").Replace("Variant: Compact", "Nope: Compact", StringComparison.Ordinal));
        Assert.Contains(unknown.Diagnostics, d => d.StartsWith("firmament-template-unknown-argument:Nope", StringComparison.Ordinal));
        var mismatch = FirmamentV2Parser.Parse(Source("Bad", "60", "40mm", "20mm", "Compact"));
        Assert.Contains(mismatch.Diagnostics, d => d.StartsWith("firmament-template-value-argument-type-mismatch:Width", StringComparison.Ordinal));
        var constraint = FirmamentV2Parser.Parse(Source("Bad", "60mm", "40mm", "20mm", "Compact").Replace("TBody: Box", "TBody: Sphere", StringComparison.Ordinal));
        Assert.Contains(constraint.Diagnostics, d => d.StartsWith("firmament-template-type-argument-does-not-satisfy-concept:TBody", StringComparison.Ordinal));
        var require = FirmamentV2Parser.Parse(Source("Bad", "0mm", "40mm", "20mm", "Compact"));
        Assert.Contains(require.Diagnostics, d => d.StartsWith("firmament-template-require-failed:Bad.ValidDimensions", StringComparison.Ordinal));
        Assert.Contains(require.Diagnostics, d => d.Contains("template-signature:MountingBracket<", StringComparison.Ordinal));
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

    private static string WidgetSource(string instance) => $$"""
        Concept WidgetConcept {
            Bounds: Box3
            TopPlane: Plane
            ChamferDistance: Length
        }
        Record WidgetSpec {
            Width: Length
            Height: Length
            Depth: Length
        }
        Static TallWidget: WidgetSpec = WidgetSpec {
            Width: 40mm
            Height: 25mm
            Depth: 20mm
        }
        Template < Spec: WidgetSpec >
        Struct Widget: WidgetConcept {
            Require Positive => Spec.Width > 0mm && Spec.Height > 0mm
            Concept Struct Design: WidgetConcept {
                Bounds: Box3 {
                    Size: [Spec.Width, Spec.Depth, Spec.Height]
                }
                TopPlane: Bounds.Face(+Z)
                ChamferDistance: 1mm
            }
            Box Body {
                Bounds: Design.Bounds
            }
            Modify Body {
                EdgeFinish TopBreak {
                    Face: Design.TopPlane
                    Target: Boundary
                    Kind: Chamfer
                    Distance: Design.ChamferDistance
                }
            }
            Expose {
                Bounds: Design.Bounds
                TopPlane: Body.Top
                ChamferDistance: Design.ChamferDistance
            }
        }
        Struct {{instance}} = Widget < Spec: TallWidget >
        """;
}
