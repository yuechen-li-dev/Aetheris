using Aetheris.Kernel.Core.Mechanisms;

namespace Aetheris.Kernel.Core.Tests.Mechanisms;

public sealed class IndexedRotaryStorageTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(10)]
    [InlineData(12)]
    public void IndependentOccurrencesReadOnlyReachedIndicesAndRetainAllCrossings(int positions)
    {
        var definition = new RotaryIndexDefinition(positions, 4.5, 5);
        for (var preload = 0; preload < positions; preload++)
        {
            var a = new IndexedRotaryStorage("source", definition, preload);
            var b = new IndexedRotaryStorage("destination", definition);
            var original = a.Capture();
            Assert.Throws<InvalidOperationException>(() => b.Advance(.1));
            b.SetPinLift(5);
            b.Advance(definition.IndexAngleRadians / 2);
            Assert.Null(b.ReadIndex);
            Assert.Throws<InvalidOperationException>(() => b.SetPinLift(0));
            var events = b.Advance(2 * double.Pi - definition.IndexAngleRadians / 2);
            Assert.Equal(positions, events.Count);
            Assert.Single(events, e => e.Rollover);
            Assert.Equal(0, b.ReadIndex);
            b.SetPinLift(0);
            Assert.Equal(original, a.Capture());
        }
    }

    [Fact]
    public void LargeIrregularAndResumedAdvancesHaveIdenticalEventOrder()
    {
        var definition = new RotaryIndexDefinition(10, 4.5, 5);
        var direct = new IndexedRotaryStorage("wheel", definition, 9);
        var stepped = new IndexedRotaryStorage("wheel", definition, 9);
        direct.SetPinLift(5); stepped.SetPinLift(5);
        var expected = direct.Advance(8 * double.Pi);
        var events = new List<RotaryIndexCrossing>();
        double accumulated = 0;
        for (var i = 0; i < 100; i++)
        {
            var step = (i % 7 + 1) * .019;
            events.AddRange(stepped.Advance(step));
            accumulated += step;
            if (i == 50)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(stepped.Capture());
                stepped = new IndexedRotaryStorage("wheel", definition);
                stepped.Restore(System.Text.Json.JsonSerializer.Deserialize<RotaryStorageSnapshot>(json)!);
            }
        }
        events.AddRange(stepped.Advance(8 * double.Pi - accumulated));
        Assert.Equal(expected, events);
        Assert.Equal(direct.UnwrappedRadians, stepped.UnwrappedRadians, 9);
        Assert.Equal(direct.ReadIndex, stepped.ReadIndex);
    }

    [Fact]
    public void InvalidSnapshotsAndDriveAreAtomicFailures()
    {
        var joint = new IndexedRotaryStorage("a", new(10, 4.5, 5));
        var before = joint.Capture();
        Assert.Throws<ArgumentException>(() => joint.Restore(before with { OccurrenceId = "b" }));
        Assert.Throws<ArgumentException>(() => joint.Restore(before with { UnwrappedRadians = .1 }));
        Assert.Throws<ArgumentException>(() => joint.Restore(before with { Definition = new(12, 4.5, 5) }));
        Assert.Throws<ArgumentException>(() => joint.Restore(before with { Version = 2 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => joint.Advance(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => joint.Advance(-.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => joint.SetPinLift(6));
        Assert.Equal(before, joint.Capture());
    }
}
