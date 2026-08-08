using Aetheris.Kernel.Firmament.FirmamentV2;
using System.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentV2StaticTablesWithM1Tests
{
    [Fact]
    public void ColumnarTable_KeyLookup_WithDerivation_AndNestedRecordTemplateBinding_AreErasedBeforeAir()
    {
        var parse = FirmamentV2Parser.Parse(Source());
        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        var staticAuthoring = parse.Document!.StaticAuthoring!;
        var table = Assert.Single(staticAuthoring.Tables!);
        Assert.Equal("WidgetStandards", table.Name);
        Assert.Equal("StandardRow", table.RowType);
        Assert.Equal("Kind", table.KeyField);
        Assert.Equal(2, table.RowCount);
        Assert.Equal(["Small", "Large"], table.Columns["Kind"]);

        var instance = Assert.Single(parse.Document.ConceptIr!.TemplateInstantiations!);
        var spec = Assert.Single(instance.RecordArguments!).Value;
        Assert.Equal("TallSmall", spec.StaticValue);
        Assert.Equal("30mm", spec.Members["Height"]);
        Assert.Equal("40mm", spec.Members["Standard.Width"]);
        Assert.Contains("Table:WidgetStandards row:0 key:Small", spec.Provenance, StringComparison.Ordinal);
        Assert.Contains("derivedFrom:Base", spec.Provenance, StringComparison.Ordinal);
        Assert.Equal([40d, 20d, 30d], parse.Document.Solid.Box!.Size);
        Assert.DoesNotContain("Static Table", parse.Document.ConceptIr.MaterializedStruct.SourceSpelling, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Width: [40mm]", "firmament-template-table-unequal-column-length")]
    [InlineData("Width: [40deg, 80mm]", "firmament-template-table-column-type-mismatch")]
    [InlineData("Kind: [Small, Small]", "firmament-template-table-duplicate-key")]
    [InlineData("Unknown: [1mm, 2mm]", "firmament-template-table-unknown-column")]
    public void Table_ReportsDeterministicTypedDiagnostics(string replacement, string diagnostic)
    {
        var parse = FirmamentV2Parser.Parse(Source().Replace("Width: [40mm, 80mm]", replacement, StringComparison.Ordinal));
        Assert.Contains(parse.Diagnostics, value => value.StartsWith(diagnostic, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Height: 30deg", "firmament-template-with-field-type-mismatch")]
    [InlineData("Missing: 30mm", "firmament-template-with-unknown-field")]
    [InlineData("Height: 30mm Height: 31mm", "firmament-template-with-duplicate-field")]
    public void With_ReportsTypedImmutableRecordDiagnostics(string replacement, string diagnostic)
    {
        var parse = FirmamentV2Parser.Parse(Source().Replace("Height: 30mm", replacement, StringComparison.Ordinal));
        Assert.Contains(parse.Diagnostics, value => value.StartsWith(diagnostic, StringComparison.Ordinal));
    }

    [Fact]
    public void ThousandRowColumnarTable_BindsASelectedRowWithinTheStaticCompilerBudget()
    {
        var indices = string.Join(", ", Enumerable.Range(0, 1000));
        var widths = string.Join(", ", Enumerable.Repeat("40mm", 1000));
        var depths = string.Join(", ", Enumerable.Repeat("20mm", 1000));
        var source = Source()
            .Replace("Enum WidgetKind { Small Large }", string.Empty, StringComparison.Ordinal)
            .Replace("Kind: WidgetKind", "Index: int", StringComparison.Ordinal)
            .Replace("Key: Kind", "Key: Index", StringComparison.Ordinal)
            .Replace("Kind: [Small, Large]", "Index: [" + indices + "]", StringComparison.Ordinal)
            .Replace("Width: [40mm, 80mm]", "Width: [" + widths + "]", StringComparison.Ordinal)
            .Replace("Depth: [20mm, 30mm]", "Depth: [" + depths + "]", StringComparison.Ordinal)
            .Replace("WidgetStandards[Small]", "WidgetStandards[500]", StringComparison.Ordinal);
        var stopwatch = Stopwatch.StartNew();
        var parse = FirmamentV2Parser.Parse(source);
        stopwatch.Stop();

        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        Assert.Equal(1000, Assert.Single(parse.Document!.StaticAuthoring!.Tables!).RowCount);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Static Table parse took {stopwatch.Elapsed}.");
    }

    private static string Source() => """
        Model TableDogfood {
            Units: mm
            Enum WidgetKind { Small Large }
            Concept WidgetConcept {
                Bounds: Box3
                TopPlane: Plane
                ChamferDistance: Length
            }
            Record StandardRow {
                Kind: WidgetKind
                Width: Length
                Depth: Length
            }
            Record WidgetSpec {
                Standard: StandardRow
                Height: Length
            }
            Static Table WidgetStandards: StandardRow Key: Kind {
                Kind: [Small, Large]
                Width: [40mm, 80mm]
                Depth: [20mm, 30mm]
            }
            Static SmallStandard = WidgetStandards[Small]
            Static Base: WidgetSpec = WidgetSpec {
                Standard: SmallStandard
                Height: 20mm
            }
            Static TallSmall = Base with {
                Height: 30mm
            }
            Template < Spec: WidgetSpec >
            Struct Widget: WidgetConcept {
                Require Positive => Spec.Standard.Width > 0mm && Spec.Height > 0mm
                Concept Struct Design: WidgetConcept {
                    Bounds: Box3 {
                        Size: [Spec.Standard.Width, Spec.Standard.Depth, Spec.Height]
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
            Struct Example = Widget < Spec: TallSmall >
        }
        """;
}
