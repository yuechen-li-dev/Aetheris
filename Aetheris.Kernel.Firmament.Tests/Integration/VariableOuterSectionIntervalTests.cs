using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests.Integration;

public sealed class VariableOuterSectionIntervalTests
{
    [Fact]
    public void AcceptsExplicitOuterCorrespondenceAndUnchangedCircularInnerLoop()
    {
        var lower = Rectangle("lower", 0d); var upper = Rectangle("upper", 1d);
        var inner = Circle("shaft", 5d, 5d, 2d);
        var interval = new VariableOuterSectionInterval("transition", 7d, 8d, lower, upper,
            Enumerable.Range(0, 4).Select(i => new VariableOuterVertexCorrespondence(lower.Loops[0].Segments[i].Provenance.StableId, upper.Loops[0].Segments[i].Provenance.StableId, i, i)).ToArray(),
            Enumerable.Range(0, 4).Select(i => new VariableOuterSegmentCorrespondence(lower.Loops[0].Segments[i].Provenance.StableId, upper.Loops[0].Segments[i].Provenance.StableId, i, i)).ToArray(),
            [new("inner:shaft", "hole:shaft", inner, inner)], "edgefinish:Top", ["test"]);
        Assert.True(VariableOuterSectionIntervalValidator.Validate(interval).IsValid);
    }

    [Fact]
    public void RejectsChangingInnerLoop()
    {
        var lower = Rectangle("lower", 0d); var upper = Rectangle("upper", 1d); var a = Circle("shaft", 5d, 5d, 2d); var b = Circle("shaft", 5d, 5d, 3d);
        var interval = new VariableOuterSectionInterval("transition", 7d, 8d, lower, upper, [], [], [new("inner:shaft", "hole:shaft", a, b)], "edgefinish:Top", ["test"]);
        Assert.Contains(VariableOuterSectionIntervalValidator.Validate(interval).Diagnostics, x => x.StartsWith("VariableOuterSectionIntervalInnerLoopChanged", StringComparison.Ordinal));
    }

    private static ResolvedProfile2D Rectangle(string name, double inset)
    {
        var p = new[] { (inset, inset), (10d - inset, inset), (10d - inset, 10d - inset), (inset, 10d - inset) };
        var segments = p.Select((point, i) => new ResolvedProfileSegment2D($"S{i}", new LineArcLineSegment2D(point, p[(i + 1) % 4]), new ProfileSegmentProvenance($"{name}:S{i}", "test", "test", "test", "XY"))).ToArray();
        return new(name, "XY", [new("Outer", true, segments)]);
    }
    private static ResolvedProfile2D Circle(string name, double x, double y, double radius) => new(name, "XY", [new("Outer", true, [new ResolvedProfileSegment2D("Circle", new LineArcFullCircle2D((x, y), radius), new ProfileSegmentProvenance($"{name}:circle", "test", "test", "test", "XY"))])]);
}
