using Aetheris.Kernel.StandardLibrary;

namespace Aetheris.Kernel.StandardLibrary.Tests;

public sealed class GearFamilyCatalogTests
{
    [Fact]
    public void AllQualifiedGearFamiliesAreDiscoverableWithoutSourceInspection()
    {
        Assert.Equal(["BevelGear", "InternalSpurGear", "MiterGear", "Pawl", "RatchetGear", "SpurGear"],
            GearFamilyCatalog.Families.Select(item => item.Name).Order(StringComparer.Ordinal));
        Assert.All(GearFamilyCatalog.Families, item =>
        {
            Assert.StartsWith("PowerTransmission.", item.Category, StringComparison.Ordinal);
            Assert.NotEmpty(item.RequiredParameters); Assert.NotEmpty(item.Construction);
        });
    }
}
