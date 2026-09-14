namespace Aetheris.Kernel.Core.Mechanisms;

/// <summary>An ideal rotary joint with an axially withdrawn indexing pin.
/// This is a mechanical primitive, not a sequencer, adder or state-machine host.</summary>
public sealed record RotaryIndexDefinition(int Positions, double RequiredPinLiftMm, double MaximumPinLiftMm)
{
    public double IndexAngleRadians => 2 * double.Pi / Positions;
    public void Validate()
    {
        if (Positions is < 2 or > 100 || !double.IsFinite(RequiredPinLiftMm) || RequiredPinLiftMm <= 0
            || !double.IsFinite(MaximumPinLiftMm) || MaximumPinLiftMm < RequiredPinLiftMm)
            throw new ArgumentException("mechanism-index-definition-invalid");
    }
}

public sealed record RotaryStorageSnapshot(string OccurrenceId, double UnwrappedRadians, double PinLiftMm, RotaryIndexDefinition Definition, int Version = 1);
public sealed record RotaryIndexCrossing(string OccurrenceId, long Crossing, int Index, bool Rollover);

public sealed class IndexedRotaryStorage
{
    private const double AngularTolerance = 1e-10;
    public RotaryIndexDefinition Definition { get; }
    public string OccurrenceId { get; }
    public double UnwrappedRadians { get; private set; }
    public double PinLiftMm { get; private set; }
    public double PositionRadians => UnwrappedRadians % (2 * double.Pi);
    public bool Locked => PinLiftMm < Definition.RequiredPinLiftMm;
    public int? ReadIndex
    {
        get
        {
            var coordinate = UnwrappedRadians / Definition.IndexAngleRadians;
            var nearest = double.Round(coordinate);
            return double.Abs(coordinate - nearest) <= AngularTolerance
                ? (int)((long)nearest % Definition.Positions) : null;
        }
    }

    public IndexedRotaryStorage(string occurrenceId, RotaryIndexDefinition definition, int preloadIndex = 0)
    {
        definition.Validate();
        if (string.IsNullOrWhiteSpace(occurrenceId)) throw new ArgumentException("mechanism-occurrence-missing");
        if (preloadIndex < 0 || preloadIndex >= definition.Positions) throw new ArgumentOutOfRangeException(nameof(preloadIndex));
        Definition = definition;
        OccurrenceId = occurrenceId;
        // Explicit setup only. Drive never accepts a target digit or register value.
        UnwrappedRadians = preloadIndex * definition.IndexAngleRadians;
    }

    public void SetPinLift(double liftMm)
    {
        if (!double.IsFinite(liftMm) || liftMm < 0 || liftMm > Definition.MaximumPinLiftMm) throw new ArgumentOutOfRangeException(nameof(liftMm));
        if (liftMm < Definition.RequiredPinLiftMm && ReadIndex is null)
            throw new InvalidOperationException("mechanism-index-pin-misaligned");
        PinLiftMm = liftMm;
    }

    public IReadOnlyList<RotaryIndexCrossing> Advance(double radians)
    {
        if (!double.IsFinite(radians) || radians < 0 || radians > 200 * double.Pi
            || UnwrappedRadians + radians > 2_000_000 * double.Pi)
            throw new ArgumentOutOfRangeException(nameof(radians), "mechanism-index-drive-outside-envelope");
        if (radians == 0) return [];
        if (Locked) throw new InvalidOperationException("mechanism-lock-drive-conflict");
        var start = UnwrappedRadians / Definition.IndexAngleRadians;
        var finish = (UnwrappedRadians + radians) / Definition.IndexAngleRadians;
        if (double.Abs(finish - double.Round(finish)) <= AngularTolerance) finish = double.Round(finish);
        var events = new List<RotaryIndexCrossing>();
        for (var crossing = (long)double.Floor(start + AngularTolerance) + 1; crossing <= (long)double.Floor(finish); crossing++)
        {
            var index = (int)(crossing % Definition.Positions);
            events.Add(new(OccurrenceId, crossing, index, index == 0));
        }
        UnwrappedRadians = finish * Definition.IndexAngleRadians;
        return events;
    }

    public RotaryStorageSnapshot Capture() => new(OccurrenceId, UnwrappedRadians, PinLiftMm, Definition);

    public void Restore(RotaryStorageSnapshot snapshot)
    {
        if (snapshot.Version != 1 || snapshot.Definition != Definition || snapshot.OccurrenceId != OccurrenceId || !double.IsFinite(snapshot.UnwrappedRadians)
            || snapshot.UnwrappedRadians < 0 || snapshot.UnwrappedRadians > 2_000_000 * double.Pi
            || !double.IsFinite(snapshot.PinLiftMm) || snapshot.PinLiftMm < 0 || snapshot.PinLiftMm > Definition.MaximumPinLiftMm)
            throw new ArgumentException("mechanism-index-snapshot-invalid");
        var coordinate = snapshot.UnwrappedRadians / Definition.IndexAngleRadians;
        if (snapshot.PinLiftMm < Definition.RequiredPinLiftMm
            && double.Abs(coordinate - double.Round(coordinate)) > AngularTolerance)
            throw new ArgumentException("mechanism-index-snapshot-lock-conflict");
        UnwrappedRadians = snapshot.UnwrappedRadians;
        PinLiftMm = snapshot.PinLiftMm;
    }
}
