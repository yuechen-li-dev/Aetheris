using Aetheris.Kernel.Core.Air;

namespace Aetheris.Kernel.Firmament.Materializer;

internal enum AirHoleFootprintContact
{
    /// <summary>The hole mouth lies wholly inside the entry face, leaving a wall of positive thickness on every side.</summary>
    Inside,
    /// <summary>The hole wall touches a side face along a line: the wall there would have zero thickness.</summary>
    Tangent,
    /// <summary>The hole cuts through at least one side face. A through hole at an edge is a notch, not an error.</summary>
    Breakout,
    /// <summary>The hole mouth does not overlap the entry face at all, so it would remove nothing.</summary>
    Misses
}

internal sealed record AirHoleFootprint(AirHoleFootprintContact Contact, string? Face, double? Threshold, string Axis)
{
    /// <summary>
    /// Classifies a face-local hole mouth against the rectangular entry face of a Box host. This is the only place the
    /// question "does this hole break out?" is answered, so every hole route gives the same answer to it.
    /// </summary>
    public static AirHoleFootprint Classify(AirHoleFeature feature, AirHoleSimpleShaftHost host, double tolerance)
    {
        if (feature.Placement is not AirFaceLocalHolePlacement placement) return new(AirHoleFootprintContact.Inside, null, null, "");
        var u = placement.U; var v = placement.V; var r = feature.Shaft.Radius;
        var halfW = host.Width / 2d; var halfD = host.Depth / 2d;

        // Nearest distance from the mouth centre to the face rectangle. A mouth that at most grazes the rectangle removes
        // no material from the face, which is never what an author means by a hole.
        var outsideX = Math.Max(Math.Abs(u) - halfW, 0d);
        var outsideY = Math.Max(Math.Abs(v) - halfD, 0d);
        if (Math.Sqrt(outsideX * outsideX + outsideY * outsideY) >= r - tolerance) return new(AirHoleFootprintContact.Misses, null, null, "");

        // Signed wall thickness left on each side: positive leaves material, zero is tangency, negative cuts through.
        var walls = new (string Face, double Gap, double Threshold, string Axis)[]
        {
            ("+X", halfW - (u + r), halfW - r, "X"),
            ("-X", halfW - (r - u), r - halfW, "X"),
            ("+Y", halfD - (v + r), halfD - r, "Y"),
            ("-Y", halfD - (r - v), r - halfD, "Y"),
        };
        var tangent = walls.FirstOrDefault(w => Math.Abs(w.Gap) <= tolerance);
        if (tangent.Face is not null) return new(AirHoleFootprintContact.Tangent, tangent.Face, tangent.Threshold, tangent.Axis);
        var breakout = walls.FirstOrDefault(w => w.Gap < -tolerance);
        return breakout.Face is not null
            ? new(AirHoleFootprintContact.Breakout, breakout.Face, breakout.Threshold, breakout.Axis)
            : new(AirHoleFootprintContact.Inside, null, null, "");
    }

    /// <summary>The author-facing rejection for a tangent or missing hole, in the author's own terms and units.</summary>
    public string Explain(AirHoleFeature feature) => Contact switch
    {
        AirHoleFootprintContact.Tangent =>
            $"Hole '{feature.FeatureId}' (diameter {Format(feature.Shaft.Diameter)}) is exactly tangent to the {Face} face: its wall would be zero thickness along a line, "
            + $"which is neither machinable nor a valid solid. Put Center {Axis} {(Face!.StartsWith('+') ? "below" : "above")} {Format(Threshold!.Value)} to keep a wall on that side, "
            + $"or {(Face!.StartsWith('+') ? "above" : "below")} it to cut through the {Face} face.",
        AirHoleFootprintContact.Misses =>
            $"Hole '{feature.FeatureId}' does not overlap its entry face, so it would remove no material. Move its Center onto the face.",
        _ => ""
    };

    private static string Format(double millimetres) => $"{millimetres:0.######}mm";
}
