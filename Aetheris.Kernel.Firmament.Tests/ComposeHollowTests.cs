using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ComposeHollowTests
{
    private static double RoundedRect(double w, double h, double r) => w * h - (4d - double.Pi) * r * r;

    public static TheoryData<string, double> Canonical => new()
    {
        // Exterior minus cavity, from the closed forms of each section.
        { "rounded-tray", RoundedRect(120, 80, 12) * 24 - RoundedRect(116, 76, 10) * 22 },
        {
            "enclosure-boss-pocket",
            RoundedRect(100, 60, 8) * 20 + RoundedRect(30, 30, 6) * 12 - 20 * 24 * 6
            - (RoundedRect(96, 56, 6) * 12 + (RoundedRect(96, 56, 6) - RoundedRect(24, 28, 2)) * 6 + RoundedRect(26, 26, 4) * 12)
        },
        // The reflex corner adds the square of the wall less a quarter disc; the bore's tube grows it by the wall.
        { "bracket-tray", (1275 - 9 * double.Pi) * 15 - (891 + 4 - double.Pi - 25 * double.Pi) * 13 },
    };

    [Theory]
    [MemberData(nameof(Canonical))]
    public void HollowSectionStacksBuildAtConstantWall(string fixture, double expectedVolume)
    {
        var built = FirmamentBuildAndExport.CompileSource(File.ReadAllText(Fixture("Canonical", fixture)));
        Assert.True(built.IsSuccess, string.Join(Environment.NewLine, built.Diagnostics.Select(d => d.Message)));
        var imported = Step242Importer.ImportBody(built.Value.StepText);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(d => d.Message)));
        var enclosure = BrepMassProperties.EvaluateEnclosure(imported.Value);
        Assert.True(enclosure.IsEnclosed && enclosure.IsOrientationConsistent, string.Join(" | ", enclosure.Diagnostics));
        var volume = BrepMassProperties.Evaluate(imported.Value, new(0.01d, double.Pi / 90d, 1d, MaximumSegments: 256)).AbsoluteVolume;
        Assert.InRange(volume, expectedVolume * (1 - 2e-4), expectedVolume * (1 + 2e-4));
    }

    [Theory]
    [InlineData("narrow-neck", "hollow-wall-offset-collapse")]
    [InlineData("closed-vessel", "firmament-v2-hollow-closed-vessel-unsupported")]
    [InlineData("side-opening", "firmament-v2-hollow-opening-unsupported")]
    public void HollowRefusalsNameTheirCause(string fixture, string code)
    {
        var built = FirmamentBuildAndExport.CompileSource(File.ReadAllText(Fixture("Invalid", fixture)));
        Assert.False(built.IsSuccess);
        Assert.Contains(built.Diagnostics, d => d.Message.Contains(code, StringComparison.Ordinal));
    }

    [Fact]
    public void ConvexArcsNoLargerThanTheWallVanishIntoSharpCorners()
    {
        // A 20 x 10 rounded rectangle with 1mm corners, inset by 2mm, is a sharp 16 x 6 rectangle.
        var diagnostics = new List<string>();
        var loop = ProfileInset2D.InsetLoop(RoundedRectangle(20, 10, 1), 2, outer: true, "test", diagnostics);
        Assert.NotNull(loop);
        Assert.Equal(4, loop!.Count);
        Assert.All(loop, curve => Assert.IsType<LineArcLineSegment2D>(curve));
        Assert.Equal(96d, ProfileInset2D.SignedArea(loop), 9);
    }

    [Fact]
    public void ConvexArcsLargerThanTheWallStayConcentric()
    {
        var diagnostics = new List<string>();
        var loop = ProfileInset2D.InsetLoop(RoundedRectangle(20, 10, 3), 2, outer: true, "test", diagnostics);
        Assert.NotNull(loop);
        Assert.Equal(RoundedRect(16, 6, 1), ProfileInset2D.SignedArea(loop!), 9);
        Assert.All(loop!.OfType<LineArcCircularArc2D>(), arc => Assert.Equal(1d, arc.Radius, 12));
    }

    [Fact]
    public void HoleLoopsGrowByTheWall()
    {
        var diagnostics = new List<string>();
        var loop = ProfileInset2D.InsetLoop([new LineArcFullCircle2D((0, 0), 3)], 2, outer: false, "test", diagnostics);
        Assert.Equal(5d, Assert.IsType<LineArcFullCircle2D>(Assert.Single(loop!)).Radius, 12);
    }

    private static IReadOnlyList<LineArcProfileCurve2D> RoundedRectangle(double w, double h, double r)
    {
        var (x, y) = (w / 2, h / 2);
        return
        [
            new LineArcLineSegment2D((-x + r, -y), (x - r, -y)),
            new LineArcCircularArc2D((x - r, -y + r), r, -double.Pi / 2, double.Pi / 2),
            new LineArcLineSegment2D((x, -y + r), (x, y - r)),
            new LineArcCircularArc2D((x - r, y - r), r, 0, double.Pi / 2),
            new LineArcLineSegment2D((x - r, y), (-x + r, y)),
            new LineArcCircularArc2D((-x + r, y - r), r, double.Pi / 2, double.Pi / 2),
            new LineArcLineSegment2D((-x, y - r), (-x, -y + r)),
            new LineArcCircularArc2D((-x + r, -y + r), r, double.Pi, double.Pi / 2),
        ];
    }

    private static string Fixture(string kind, string name) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, $"../../../../fixtures/{kind}/Hollow/{name}.firmament"));
}
