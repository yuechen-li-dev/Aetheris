using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

[Trait("Suite", "Language")]
public sealed class FirmamentRequirementsTests
{
    private const string Source = """
        Concept Positive(value: Length) => value > 0mm
        Concept Wall(minimum: Length, maximum: Length) {
          present: Positive(minimum)
          even: maximum <= minimum * 1.5
        }
        """;

    private static readonly Dictionary<string, string> Measured = new(StringComparer.Ordinal)
    {
        ["wall.minimum"] = "2mm",
        ["wall.maximum"] = "2.5mm",
        ["wall.maximumThick"] = "4mm",
    };

    [Fact]
    public void MeasuredNamesAreSubstitutedAsQuantities()
    {
        var result = Assert.Single(FirmamentRequirements.Check(Source, [("walls", "Wall(wall.minimum, wall.maximum)")], Measured));
        Assert.True(result.Passed, result.Message);
        Assert.True(result.Valid);
    }

    [Fact]
    public void ALongerNameIsNotSplitByAShorterOne()
    {
        var result = Assert.Single(FirmamentRequirements.Check(Source, [("walls", "Wall(wall.minimum, wall.maximumThick)")], Measured));
        Assert.False(result.Passed);
        Assert.True(result.Valid);
        Assert.Contains("Wall.even", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownConceptIsInvalidNotFailed()
    {
        var result = Assert.Single(FirmamentRequirements.Check(Source, [("walls", "Wal(wall.minimum, wall.maximum)")], Measured));
        Assert.False(result.Passed);
        Assert.False(result.Valid);
    }
}
