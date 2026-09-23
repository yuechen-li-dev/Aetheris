using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentFieldProjectionTests
{
    private const string Source = """
        Model Witness {
          Units: mm
          Box Body { Size: [30mm, 20mm, 10mm] }
          Modify Body {
            Hole<Shaft> H {
              On: +Z Center: Point2(0mm, 0mm)
              Diameter: 10mm /* keep this comment */ End: ThroughAll
            }
          }
        }
        """;

    [Fact]
    public void CanonicalBoxAndHoleExposeParserOwnedValuesAndRanges()
    {
        var projections = Project(Source);
        var box = Assert.Single(projections, item => item.SemanticId == "Body");
        var size = Assert.Single(box.Fields, item => item.FieldId.Value == "Box.Size");
        Assert.Equal("[30mm, 20mm, 10mm]", size.AuthoredValue);
        Assert.Equal([30d, 20d, 10d], size.EffectiveValue!.Components);
        Assert.False(size.Editable);
        Assert.Equal(size.AuthoredValue, Source.Substring(size.ValueSpan!.Start, size.ValueSpan.Length));

        var hole = Assert.Single(projections, item => item.SemanticId == "Body.H");
        var diameter = Assert.Single(hole.Fields, item => item.FieldId.Value == "Hole.Diameter");
        Assert.Equal("10mm", diameter.AuthoredValue);
        Assert.Equal(10d, diameter.EffectiveValue!.Number);
        Assert.Equal("Authored", diameter.Origin);
        Assert.True(diameter.Editable);
        Assert.Equal("10mm", Source.Substring(diameter.ValueSpan!.Start, diameter.ValueSpan.Length));
    }

    [Fact]
    public void RewriteOnlyChangesBoundLiteralAndRejectsStaleOrInvalidInputs()
    {
        var hole = Assert.Single(Project(Source), item => item.SemanticId == "Body.H");
        var next = new FirmamentProjectedValue("Length", "12 mm", 12, Unit: "mm");
        var changed = FirmamentFieldProjector.Rewrite(Source, hole.SourceRevision, hole, new("Hole.Diameter"), next);
        Assert.True(changed.Success);
        Assert.Equal(Source.Replace("Diameter: 10mm", "Diameter: 12mm", StringComparison.Ordinal), changed.NewSource);
        Assert.Contains("/* keep this comment */", changed.NewSource);
        Assert.Equal("stale_revision", FirmamentFieldProjector.Rewrite(Source + " ", hole.SourceRevision, hole, new("Hole.Diameter"), next).Code);
        Assert.Equal("invalid_value", FirmamentFieldProjector.Rewrite(Source, hole.SourceRevision, hole, new("Hole.Diameter"), next with { Number = -1 }).Code);
        Assert.Equal("field_readonly", FirmamentFieldProjector.Rewrite(Source, hole.SourceRevision, hole, new("Hole.On"), next).Code);
    }

    [Fact]
    public void HelixProjectsAuthoredAndDefaultedValuesFromWireFormAir()
    {
        const string source = """
            Model W {
              Units: mm
              WireForm Spring {
                Diameter: 1mm
                Origin: [0mm, 0mm, 0mm]
                Tangent: [0, 0, 1]
                Up: [0, 1, 0]
                Helix Winding {
                  Radius: 5mm
                  Turns: 4
                  Pitch: 2mm
                }
              }
            }
            """;
        var parsed = WireFormAuthoring.Parse(source);
        Assert.True(parsed.IsSuccess);
        var helix = Assert.Single(FirmamentFieldProjector.ProjectWireForm(parsed.Value, source, "helix.firmament", 2));
        var radius = Assert.Single(helix.Fields, item => item.FieldId.Value == "Helix.Radius");
        Assert.Equal("5mm", radius.AuthoredValue);
        Assert.Equal(5d, radius.EffectiveValue!.Number);
        Assert.Equal("Authored", radius.Origin);
        var height = Assert.Single(helix.Fields, item => item.FieldId.Value == "Helix.Height");
        Assert.Equal("Derived", height.Origin);
        Assert.Equal(8d, height.EffectiveValue!.Number);
        var hand = Assert.Single(helix.Fields, item => item.FieldId.Value == "Helix.Handedness");
        Assert.Equal("Defaulted", hand.Origin);
        Assert.Null(hand.ValueSpan);

        var expression = source.Replace("Radius: 5mm", "Radius: 2mm + 3mm", StringComparison.Ordinal);
        var expressionAir = WireFormAuthoring.Parse(expression);
        Assert.True(expressionAir.IsSuccess);
        var expressionHelix = Assert.Single(FirmamentFieldProjector.ProjectWireForm(expressionAir.Value, expression, "helix.firmament", 3));
        var expressionRadius = Assert.Single(expressionHelix.Fields, item => item.FieldId.Value == "Helix.Radius");
        Assert.Equal("2mm + 3mm", expressionRadius.AuthoredValue);
        Assert.Equal(5d, expressionRadius.EffectiveValue!.Number);
        Assert.False(expressionRadius.Editable);
    }

    [Fact]
    public void LoftProjectsBoundNamesAndDefaultTwist()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var source = File.ReadAllText(Path.Combine(directory.FullName, "fixtures", "Canonical", "SectionChain", "solid-loft-common-ray.firmament"));
        var bound = LoftAuthoringParser.Compile(source, materialize: false);
        Assert.True(bound.IsSuccess, string.Join("; ", bound.Diagnostics));
        var loft = Assert.IsType<FirmamentConstructProjection>(FirmamentFieldProjector.ProjectLoft(bound, source, "loft.firmament", 1));
        Assert.Equal("Body", loft.SemanticId);
        var rear = Assert.Single(loft.Fields, item => item.FieldId.Value == "Loft.RearProfile");
        Assert.Equal("LowerOutline", rear.EffectiveValue!.Text);
        Assert.Equal("LowerOutline", rear.AuthoredValue);
        Assert.Equal("Authored", rear.Origin);
        var twist = Assert.Single(loft.Fields, item => item.FieldId.Value == "Loft.Twist");
        Assert.Equal("Defaulted", twist.Origin);
        Assert.Null(twist.ValueSpan);
    }

    private static IReadOnlyList<FirmamentConstructProjection> Project(string source)
    {
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.NotNull(parsed.Document);
        return FirmamentFieldProjector.Project(parsed.Document, source, "witness.firmament", 4);
    }
}
