using Aetheris.Kernel.Firmament.FirmamentV2;
namespace Aetheris.Kernel.Firmament.Tests;
public sealed class FirmamentV2TemplatePatternTests
{
    [Fact]
    public void PersistedCompactTemplate_Parses()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../demos/template-m4b-compact.firmament"));
        var parse = FirmamentV2Parser.Parse(File.ReadAllText(path));
        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
    }
    [Fact]
    public void Pattern_ExpandsGridToConcreteSemanticHoles()
    {
        var parse = FirmamentV2Parser.Parse("""
            Concept Struct Design {
                Bounds: Box3 {
                    Size: [60mm, 40mm, 20mm]
                }
                Top: Bounds.Face(+Z)
                Points: Grid {
                    Within: Bounds.Face(+Z).Inset(8mm)
                    Columns: 2
                    Rows: 1
                }
            }
            Struct Bracket {
                Box Base {
                    Bounds: Design.Bounds
                }
                Modify Base {
                    Pattern MountHoles {
                        Source: Design.Points
                        Hole<Shaft> Item {
                            on: Base.Top
                            center: Item
                            diameter: 8.5mm
                            end: ThroughAll
                        }
                    }
                }
            }
            """);
        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        Assert.Equal(2, parse.Document!.ModifyBlocks!.Single().SemanticHoles.Count);
        Assert.Equal(["MountHoles[0]", "MountHoles[1]"], parse.Document.ModifyBlocks.Single().SemanticHoles.Select(x => x.Name));
        var expansion = Assert.Single(parse.Document.ConceptIr!.PatternExpansions!);
        Assert.Equal(2, expansion.Count);
        Assert.Equal("Point3", expansion.ElementType);
    }

    [Fact]
    public void Pattern_RejectsNonStaticSourceAndUnboundItem()
    {
        var invalidSource = FirmamentV2Parser.Parse("""
            Struct Bracket {
                Box Base { Size: [60mm, 40mm, 20mm] }
                Modify Base {
                    Pattern Holes {
                        Source: Base.Top
                        Hole<Shaft> Item { On: Base.Top Center: Item Diameter: 8.5mm End: ThroughAll }
                    }
                }
            }
            """);
        Assert.Contains(invalidSource.Diagnostics, d => d.StartsWith("firmament-pattern-source-not-static-point3-collection", StringComparison.Ordinal));

        var unboundItem = FirmamentV2Parser.Parse("""
            Concept Struct Design {
                Bounds: Box3 {
                    Size: [60mm, 40mm, 20mm]
                }
                Points: Grid {
                    Within: Bounds.Face(+Z).Inset(8mm)
                    Columns: 2
                    Rows: 1
                }
            }
            Struct Bracket {
                Box Base { Bounds: Design.Bounds }
                Modify Base {
                    Pattern Holes {
                        Source: Design.Points
                        Hole<Shaft> Item {
                            On: Base.Top
                            Center: Other
                            Diameter: 8.5mm
                            End: ThroughAll
                        }
                    }
                }
            }
            """);
        Assert.Contains(unboundItem.Diagnostics, d => d == "firmament-pattern-unbound-item");
    }
}
