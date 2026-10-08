using System.Xml.Linq;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Visualization;

namespace Aetheris.Kernel.Core.Tests.Visualization;

public sealed class WireframeProjectionTests
{
    [Fact]
    public void ZUpIsometricViewKeepsVerticalEdgesVerticalAndRaisesPositiveZ()
    {
        var svg = BrepWireframeSvgRenderer.RenderEdges([new Point3D[] { new(0,0,0), new(0,0,10) }],
            new BrepWireframeOptions(View: WireframeView.IsometricZUp, Center: true));
        var root = XElement.Parse(svg); XNamespace ns = root.Name.Namespace;
        var points = root.Descendants(ns + "polyline").Single().Attribute("points")!.Value.Split(' ')
            .Select(p => p.Split(',').Select(v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray()).ToArray();
        Assert.Equal(points[0][0], points[1][0]);
        Assert.True(points[1][1] < points[0][1]);
    }

    [Fact]
    public void TopologyProjectionCentersDegenerateAxisAndRetainsEveryPlacedLine()
    {
        IReadOnlyList<IReadOnlyList<Point3D>> edges = [
            new Point3D[] { new(0, 0, 0), new(0, 0, 10) },
            new Point3D[] { new(0, 0, 20), new(0, 0, 30) }];
        var options = new BrepWireframeOptions(View: WireframeView.Front, Width: 400, Height: 400,
            Background: "#fff", BoundaryColor: "#000", BoundaryWidth: 2.5, Center: true, ShowLabel: false);
        var first = BrepWireframeSvgRenderer.RenderEdges(edges, options);
        Assert.Equal(first, BrepWireframeSvgRenderer.RenderEdges(edges, options));
        var root = XElement.Parse(first); XNamespace ns = root.Name.Namespace;
        var lines = root.Descendants(ns + "polyline").ToArray();
        Assert.Equal(2, lines.Length);
        Assert.All(lines, line => {
            Assert.Equal("2.5", line.Attribute("stroke-width")!.Value);
            Assert.All(line.Attribute("points")!.Value.Split(' '), p => Assert.StartsWith("200.00,", p));
        });
        Assert.Empty(root.Descendants(ns + "text"));
        Assert.Throws<ArgumentException>(() => BrepWireframeSvgRenderer.RenderEdges([]));
        Assert.Throws<ArgumentException>(() => BrepWireframeSvgRenderer.RenderEdges([new Point3D[] { new(double.NaN, 0, 0), new(0, 0, 1) }]));
    }
}
