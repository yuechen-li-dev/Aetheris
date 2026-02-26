using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Picking;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Brep;

public sealed class HardeningRandomizedStressTests
{
    [Fact]
    public void Randomized_CreateBoxDimensions_WithFixedSeed_AreAlwaysValid()
    {
        var random = new Random(214748);

        for (var i = 0; i < 64; i++)
        {
            var width = 0.1d + (random.NextDouble() * 20d);
            var height = 0.1d + (random.NextDouble() * 20d);
            var depth = 0.1d + (random.NextDouble() * 20d);

            var result = BrepPrimitives.CreateBox(width, height, depth);
            Assert.True(result.IsSuccess);
            Assert.True(BrepDisplayTessellator.Tessellate(result.Value).IsSuccess);
        }
    }

    [Fact]
    public void Randomized_BoxBooleanClassification_WithFixedSeed_IsNoThrowAndDeterministicDiagnostics()
    {
        var random = new Random(19840615);

        for (var i = 0; i < 80; i++)
        {
            var left = CreateRandomAxisAlignedBox(random);
            var right = CreateRandomAxisAlignedBox(random);

            var exception = Record.Exception(() => BrepBoolean.Union(left, right));
            Assert.Null(exception);

            var result = BrepBoolean.Union(left, right);
            if (!result.IsSuccess)
            {
                var diagnostic = Assert.Single(result.Diagnostics);
                Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
                Assert.False(string.IsNullOrWhiteSpace(diagnostic.Message));
            }
        }
    }

    [Fact]
    public void Randomized_PickRays_WithFixedSeed_DoNotThrow_AndReturnStableOrderingShape()
    {
        var random = new Random(90210);
        var body = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var tessellation = BrepDisplayTessellator.Tessellate(body).Value;
        var options = PickQueryOptions.Default with { IncludeBackfaces = true, EdgeTolerance = 1e-5d, SortTieTolerance = 1e-5d };

        for (var i = 0; i < 50; i++)
        {
            var origin = new Point3D(
                (random.NextDouble() * 8d) - 4d,
                (random.NextDouble() * 8d) - 4d,
                (random.NextDouble() * 8d) - 4d);
            var directionVector = new Vector3D(
                (random.NextDouble() * 2d) - 1d,
                (random.NextDouble() * 2d) - 1d,
                (random.NextDouble() * 2d) - 1d);

            if (!Direction3D.TryCreate(directionVector, out var direction))
            {
                direction = Direction3D.Create(new Vector3D(1d, 0d, 0d));
            }

            var ray = new Ray3D(origin, direction);
            var first = BrepPicker.Pick(body, tessellation, ray, options);
            var second = BrepPicker.Pick(body, tessellation, ray, options);

            Assert.True(first.IsSuccess);
            Assert.True(second.IsSuccess);
            Assert.Equal(first.Value.Count, second.Value.Count);

            for (var h = 1; h < first.Value.Count; h++)
            {
                Assert.True(first.Value[h - 1].T <= first.Value[h].T + 1e-9d);
            }

            Assert.Equal(first.Value, second.Value);
        }
    }

    private static BrepBody CreateRandomAxisAlignedBox(Random random)
    {
        var minX = (random.NextDouble() * 12d) - 6d;
        var minY = (random.NextDouble() * 12d) - 6d;
        var minZ = (random.NextDouble() * 12d) - 6d;
        var sizeX = 0.2d + (random.NextDouble() * 4d);
        var sizeY = 0.2d + (random.NextDouble() * 4d);
        var sizeZ = 0.2d + (random.NextDouble() * 4d);
        return BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(minX, minX + sizeX, minY, minY + sizeY, minZ, minZ + sizeZ)).Value;
    }
}
