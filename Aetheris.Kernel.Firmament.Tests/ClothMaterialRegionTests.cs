using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using Aetheris.Cloth3D;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ClothMaterialRegionTests
{
    [Fact]
    public void RegionalCompliancePreservesRestMetricsMassesAndSeamsAndRoundTrips()
    {
        var definition = ClothDefinition3D.Grid("regions", 4, 4, new(1, 1), Vector3.Zero) with
        {
            Stitches = [new("test-seam", [0, 15], [1, -1], .0001f)],
        };
        var baseline = definition.Compile();
        int[] selected = Enumerable.Range(0, 12).ToArray();
        var regionalDefinition = definition with
        {
            MaterialRegions = [new("collar", selected.ToImmutableArray(), .25f, 4)],
        };
        var regional = regionalDefinition.Compile();
        Assert.NotEqual(baseline.ContentKey, regional.ContentKey);
        Assert.True(baseline.InverseMasses.SequenceEqual(regional.InverseMasses));
        Assert.Equal(baseline.Constraints.Length, regional.Constraints.Length);
        int changed = 0;
        for (int index = 0; index < baseline.Constraints.Length; index++)
        {
            var before = baseline.Constraints[index];
            var after = regional.Constraints[index];
            Assert.Equal(before.RestLength, after.RestLength);
            Assert.True(before.Vertices.SequenceEqual(after.Vertices));
            Assert.True(before.Weights.SequenceEqual(after.Weights));
            bool contained = before.Kind != ClothConstraintKind3D.Stitch
                && before.Vertices.All(selected.Contains);
            bool bend = before.Kind is ClothConstraintKind3D.FlatBend or ClothConstraintKind3D.VertexBend;
            float scale = contained ? (bend ? 4 : .25f) : 1;
            Assert.Equal(before.Compliance * scale, after.Compliance);
            changed += after.Compliance != before.Compliance ? 1 : 0;
        }
        Assert.True(changed > 0);
        var json = new JsonSerializerOptions { IncludeFields = true };
        var restored = JsonSerializer.Deserialize<ClothDefinition3D>(JsonSerializer.Serialize(regionalDefinition, json), json)!;
        Assert.Equal(regional.ContentKey, restored.Compile().ContentKey);
        // Region compilation feeds the existing scalar GPU stencil ABI.
        var gpu = new ClothGpuData3D(regional);
        int packed = 0;
        foreach (var color in regional.ConstraintColors)
        {
            foreach (int original in color)
            {
                Assert.Equal(regional.Constraints[original].Compliance, gpu.Source[packed++ * 12 + 6]);
            }
        }
    }

    [Fact]
    public void InvalidOrOverlappingRegionsAreRejectedBeforeSimulation()
    {
        var definition = ClothDefinition3D.Grid("invalid", 3, 3, new(1, 1), Vector3.Zero);
        Assert.Throws<ArgumentException>(() => (definition with
        {
            MaterialRegions = [new("one", [0, 1]), new("two", [1, 2])],
        }).Compile());
        foreach (var region in new[]
        {
            new ClothMaterialRegion3D("", [0]),
            new ClothMaterialRegion3D("bad", [9]),
            new ClothMaterialRegion3D("bad", [0, 0]),
            new ClothMaterialRegion3D("bad", [], 1, 1),
            new ClothMaterialRegion3D("bad", [0], float.NaN, 1),
            new ClothMaterialRegion3D("bad", [0], 0, 1),
        })
        {
            Assert.Throws<ArgumentException>(() => (definition with { MaterialRegions = [region] }).Compile());
        }
    }
}
