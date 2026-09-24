using System.Diagnostics;

namespace Aetheris.Kernel.Core.Diagnostics;

/// <summary>Opt-in, synchronous build-phase measurements for local qualification.</summary>
public static class BuildPerfTrace
{
    private sealed class Frame(string name, long start, long allocated, Frame? parent)
    {
        public string Name { get; } = name;
        public long Start { get; } = start;
        public long Allocated { get; } = allocated;
        public Frame? Parent { get; } = parent;
        public long ChildTicks { get; set; }
    }

    private sealed class Scope(Frame? frame) : IDisposable
    {
        public void Dispose()
        {
            if (frame is null) return;
            var ticks = Stopwatch.GetTimestamp() - frame.Start;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - frame.Allocated;
            Samples.Add(new(frame.Name, ticks * 1000d / Stopwatch.Frequency,
                (ticks - frame.ChildTicks) * 1000d / Stopwatch.Frequency, allocated));
            if (frame.Parent is not null) frame.Parent.ChildTicks += ticks;
            _current = frame.Parent;
        }
    }

    public sealed record Sample(string Name, double InclusiveMilliseconds, double ExclusiveMilliseconds, long AllocatedBytes);
    public sealed record BuildPerfSnapshot(Sample[] Phases, Dictionary<string, long> Counts, int Gc0, int Gc1, int Gc2);
    private static Frame? _current;
    private static bool _enabled;
    private static readonly List<Sample> Samples = [];
    private static readonly Dictionary<string, long> Counts = new(StringComparer.Ordinal);

    public static void Start()
    {
        Samples.Clear(); Counts.Clear(); _current = null; _enabled = true;
    }

    public static void Stop() { _enabled = false; _current = null; }

    public static IDisposable Phase(string name)
    {
        if (!_enabled) return new Scope(null);
        var frame = new Frame(name, Stopwatch.GetTimestamp(), GC.GetAllocatedBytesForCurrentThread(), _current);
        _current = frame;
        return new Scope(frame);
    }

    public static void Count(string name, long value)
    {
        if (_enabled) Counts[name] = value;
    }

    public static BuildPerfSnapshot Snapshot() => new(Samples.ToArray(), Counts.ToDictionary(),
        GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
}
