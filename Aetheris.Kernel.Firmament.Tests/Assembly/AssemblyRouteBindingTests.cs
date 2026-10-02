using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class AssemblyRouteBindingTests
{
    private const string Source = """
        Template<H: Length> Struct Terminal {
         Expose { Semantic Entry { DatumFrame Frame = [0mm,0mm,H] x [1,0,0] y [0,1,0] z [0,0,1]; } }
         Rect2 R { Center: [0mm,0mm]; Size: [2mm,2mm] }
         Profile P { Loop Outer { R |> TraceLoop } }
         Extrude Body { Profile: P From: 0mm To: H }
        }
        Template<A: Point3,B: Point3,C: Point3> Struct Lead {
         WireRoute Route {
          Diameter: 1mm; Material: Standard.Materials.StainlessSteel.304_Annealed; MinimumBendRadius: 3mm;
          Path { Start { At: A; } Corner Turn { At: B; Radius: 3mm; } End { At: C; } }
         }
        }
        Assembly Product {
         <Assembly Product>
          <Part Start = Terminal<H:10mm>> Placement { From: Origin; To: World; } </Part>
          <Part Bend = Terminal<H:10mm>> Placement { From: Origin; To: World; TranslateLocal: [30mm,0mm,0mm]; } </Part>
          <Part End = Terminal<H:10mm>> Placement { From: Origin; To: World; TranslateLocal: [30mm,30mm,0mm]; } </Part>
          <Part Wire = Lead<>>
           Placement { From: Origin; To: World; TranslateLocal: [5mm,-5mm,1mm]; }
           Bind {
            A { At: Point3(0mm,0mm,0mm); On: Product.Start.Entry.Frame; }
            B { At: Point2(0mm,0mm); On: Product.Bend.Entry.Frame; }
            C { At: Point3(0mm,0mm,0mm); On: Product.End.Entry.Frame; }
           }
          </Part>
         </Assembly>
         Anchor: Product;
        }
        """;

    [Fact]
    public void FinalPublicFramesBindExactlyOnceIntoRouteLocalCoordinates()
    {
        var result = new AssemblyM1Pipeline().Compile(Source);
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        var points = result.Ir!.RouteBindings!;
        Assert.Equal(3, points.Count);
        Assert.Equal(new double[] { 0,0,10 }, points[0].WorldPoint);
        Assert.Equal(new double[] { -5,5,9 }, points[0].LocalPoint);
        Assert.Contains("A:Point3(-5mm,5mm,9mm)", points[0].SpecializedDefinition);
        Assert.Equal(2, result.Geometry!.DefinitionBodies.Count);
    }

    [Fact]
    public void EndpointPlacementEditRebuildsOnlyBoundRoute()
    {
        using var session = new FirmamentCompilationSession();
        var before = session.Compile(Source);
        var after = session.Compile(Source.Replace("[30mm,30mm,0mm]", "[30mm,40mm,0mm]"));
        Assert.True(before.IsSuccess && after.IsSuccess, string.Join("\n", after.Diagnostics));
        Assert.Equal(1, after.Reuse!.ReusedDefinitions);
        Assert.Equal(1, after.Reuse.RebuiltDefinitions);
        Assert.Equal(40, after.Ir!.RouteBindings![2].WorldPoint[1]);
    }

    [Fact]
    public void UnknownPortStopsBeforeMaterialization()
    {
        var result = new AssemblyM1Pipeline().Compile(Source.Replace("Product.End.Entry.Frame", "Product.End.Hidden.Frame"));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == "assembly-route-bind-port-unresolved");
        Assert.Null(result.Geometry);
    }
}
