using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public enum GearFamily
{
    SpurGear,
    InternalSpurGear,
    BevelGear,
    MiterGear,
    RatchetGear,
    Pawl
}

/// <summary>Finite typed gear AIR. Product details such as hubs remain optional descendants.</summary>
public sealed record GearAir(
    string Name,
    GearFamily Family,
    double? ModuleMm,
    int? Teeth,
    double? PressureAngleDegrees,
    double FaceWidthMm,
    double BoreDiameterMm,
    double PhaseDegrees,
    double BacklashMm,
    double? OutsideDiameterMm,
    double? PitchConeAngleDegrees,
    double? HubDiameterMm,
    double? HubLengthMm,
    double? WidthMm,
    double? LengthMm,
    double? PivotDiameterMm,
    double? NoseLengthMm,
    double? EngagementAngleDegrees,
    double? DriveFaceAngleDegrees,
    IReadOnlyList<double> Axis,
    FirmamentV2SourceSpan SourceSpan)
{
    public double? PitchDiameterMm => ModuleMm is { } module && Teeth is { } teeth ? module * teeth : null;
    public double? PitchRadiusMm => PitchDiameterMm / 2d;
    public double? BaseDiameterMm => PitchDiameterMm is { } pitch && PressureAngleDegrees is { } pressure
        ? pitch * Math.Cos(pressure * Math.PI / 180d) : null;
    public double? AddendumDiameterMm => Family switch
    {
        GearFamily.SpurGear or GearFamily.BevelGear or GearFamily.MiterGear when PitchDiameterMm is { } pitch && ModuleMm is { } module => pitch + 2d * module,
        GearFamily.InternalSpurGear when PitchDiameterMm is { } pitch && ModuleMm is { } module => pitch - 2d * module,
        _ => OutsideDiameterMm
    };
    public double? RootDiameterMm => Family switch
    {
        GearFamily.SpurGear or GearFamily.BevelGear or GearFamily.MiterGear when PitchDiameterMm is { } pitch && ModuleMm is { } module => pitch - 2.5d * module,
        GearFamily.InternalSpurGear when PitchDiameterMm is { } pitch && ModuleMm is { } module => pitch + 2.5d * module,
        _ => null
    };
}

/// <summary>A typed semantic relationship between two subjects of the same authoring domain.</summary>
public sealed record InterfaceAir<T>(
    string Name,
    T A,
    T B,
    string Kind,
    bool Compatible,
    double? ExpectedCenterDistanceMm,
    double? Ratio,
    int RotationSign,
    string AxisRelation,
    double? ShaftAngleDegrees,
    double? EngagementPhaseDegrees,
    string? AllowedDirection,
    IReadOnlyList<string> RejectionReasons,
    FirmamentV2SourceSpan SourceSpan);

public sealed record GearAuthoringDocument(
    string ModelName,
    IReadOnlyList<GearAir> Gears,
    IReadOnlyList<InterfaceAir<GearAir>> Interfaces,
    IReadOnlyList<string> Diagnostics)
{
    public bool IsSuccess => Diagnostics.Count == 0;
}

public sealed record GearMaterialization(
    BrepBody Body,
    int ToothProfileCurveCount,
    int InvoluteSpanCount,
    double MaximumInvoluteApproximationErrorMm,
    string ToothConstruction,
    IReadOnlyList<string> StableToothIds);

/// <summary>
/// Built-in gear family binding and Gear AIR lowering. The author supplies engineering
/// parameters; this compiler-owned implementation constructs and radially replicates teeth.
/// </summary>
public static class GearAuthoring
{
    public const string Prefix = "firmament-gear-";
    private const double Tol = 1e-9;
    private static readonly Regex Header = new(
        @"\b(?<family>SpurGear|InternalSpurGear|BevelGear|MiterGear|RatchetGear|Pawl)\s+(?<name>[A-Za-z_]\w*)\s*\{",
        RegexOptions.CultureInvariant);
    private static readonly Regex InterfaceHeader = new(
        @"\bInterface\s*<\s*Gear\s*>\s+(?<name>[A-Za-z_]\w*)\s*\{",
        RegexOptions.CultureInvariant);

    public static bool IsGearSource(string source) => Header.IsMatch(source) || InterfaceHeader.IsMatch(source);
    public static bool HasGearDefinitions(string source) => Header.IsMatch(source);

    /// <summary>Parses only the compiler-owned Gear declarations in a mixed-domain source.</summary>
    public static GearAuthoringDocument ParseDefinitions(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var diagnostics = new List<string>();
        var modelMatch = Regex.Match(source, @"\bModel\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant);
        var modelName = modelMatch.Success ? modelMatch.Groups["name"].Value : "GearModel";
        if (!Regex.IsMatch(source, @"\bUnits\s*:\s*mm\b", RegexOptions.CultureInvariant))
            diagnostics.Add(Prefix + "units-invalid:mm-required");
        var gears = ParseGearDeclarations(source, diagnostics);
        if (gears.Count == 0) diagnostics.Add(Prefix + "declaration-missing");
        return new(modelName, gears, [], diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }

    public static GearAuthoringDocument Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var diagnostics = new List<string>();
        var modelMatch = Regex.Match(source, @"\bModel\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant);
        var modelName = modelMatch.Success ? modelMatch.Groups["name"].Value : "GearModel";
        if (!Regex.IsMatch(source, @"\bUnits\s*:\s*mm\b", RegexOptions.CultureInvariant))
            diagnostics.Add(Prefix + "units-invalid:mm-required");

        var gears = ParseGearDeclarations(source, diagnostics);

        var interfaces = new List<InterfaceAir<GearAir>>();
        foreach (Match header in InterfaceHeader.Matches(source))
        {
            var open = source.IndexOf('{', header.Index); var close = MatchingBrace(source, open);
            if (close < 0) { diagnostics.Add(Prefix + "interface-unclosed:" + header.Groups["name"].Value); continue; }
            var body = source[(open + 1)..close]; var aName = Identifier(body, "A"); var bName = Identifier(body, "B");
            if (aName is null || bName is null) { diagnostics.Add(Prefix + "interface-endpoint-missing:" + header.Groups["name"].Value); continue; }
            var a = gears.SingleOrDefault(gear => gear.Name == aName); var b = gears.SingleOrDefault(gear => gear.Name == bName);
            if (a is null || b is null) { diagnostics.Add(Prefix + "interface-endpoint-unresolved:" + header.Groups["name"].Value); continue; }
            interfaces.Add(EvaluateInterface(header.Groups["name"].Value, a, b,
                Angle(body, "ShaftAngle"), Angle(body, "EngagementPhase"), Identifier(body, "AllowedDirection"),
                new(header.Index, close - header.Index + 1)));
        }
        diagnostics.AddRange(interfaces.Where(item => !item.Compatible).SelectMany(item => item.RejectionReasons.Select(reason => $"{Prefix}interface-incompatible:{item.Name}:{reason}")));
        if (gears.Count == 0) diagnostics.Add(Prefix + "declaration-missing");
        return new(modelName, gears, interfaces, diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }

    private static List<GearAir> ParseGearDeclarations(string source, List<string> diagnostics)
    {
        var gears = new List<GearAir>();
        foreach (Match header in Header.Matches(source))
        {
            var open = source.IndexOf('{', header.Index);
            var close = MatchingBrace(source, open);
            if (close < 0) { diagnostics.Add(Prefix + "declaration-unclosed:" + header.Groups["name"].Value); continue; }
            var family = Enum.Parse<GearFamily>(header.Groups["family"].Value);
            var name = header.Groups["name"].Value;
            var body = source[(open + 1)..close];
            double? module = Length(body, "Module");
            int? teeth = Integer(body, "Teeth");
            double? pressure = Angle(body, "PressureAngle");
            var face = Length(body, family == GearFamily.Pawl ? "Thickness" : "FaceWidth") ?? 0d;
            var bore = Length(body, "BoreDiameter") ?? 0d;
            var gear = new GearAir(name, family, module, teeth, pressure, face, bore,
                Angle(body, "Phase") ?? 0d, Length(body, "Backlash") ?? 0d,
                Length(body, "OutsideDiameter"), Angle(body, "PitchConeAngle"),
                Length(body, "HubDiameter"), Length(body, "HubLength"),
                Length(body, "Width"), Length(body, "Length"), Length(body, "PivotDiameter"),
                Length(body, "NoseLength"), Angle(body, "EngagementAngle"), Angle(body, "DriveFaceAngle"),
                Vector3(body, "Axis") ?? [0d, 0d, 1d],
                new(header.Index, close - header.Index + 1));
            Validate(gear, diagnostics);
            gears.Add(gear);
        }
        foreach (var duplicate in gears.GroupBy(gear => gear.Name, StringComparer.Ordinal).Where(group => group.Count() > 1))
            diagnostics.Add(Prefix + "duplicate-identity:" + duplicate.Key);
        return gears;
    }

    public static GearMaterialization? Materialize(GearAir gear, out IReadOnlyList<string> diagnostics)
    {
        var errors = new List<string>();
        Validate(gear, errors);
        if (errors.Count > 0) { diagnostics = errors; return null; }
        if (gear.Family is GearFamily.BevelGear or GearFamily.MiterGear)
        {
            var bevel = GearBevelLoftMaterializer.TryMaterialize(gear);
            diagnostics = bevel.Diagnostics;
            return bevel.Materialization;
        }

        int involuteCount;
        double maxError;
        var curves = gear.Family switch
        {
            GearFamily.SpurGear => ExternalInvoluteProfile(gear, out involuteCount, out maxError),
            GearFamily.InternalSpurGear => InternalInvoluteProfile(gear, out involuteCount, out maxError),
            GearFamily.RatchetGear => RatchetProfile(gear, out involuteCount, out maxError),
            GearFamily.Pawl => PawlProfile(gear, out involuteCount, out maxError),
            _ => throw new InvalidOperationException()
        };
        var loops = new List<LineArcProfileLoop2D>();
        if (gear.Family == GearFamily.InternalSpurGear)
        {
            loops.Add(new([new LineArcFullCircle2D((0d, 0d), gear.OutsideDiameterMm!.Value / 2d)], false));
            loops.Add(new(curves, true));
        }
        else
        {
            loops.Add(new(curves, false));
            if (gear.BoreDiameterMm > Tol) loops.Add(new([new LineArcFullCircle2D((0d, 0d), gear.BoreDiameterMm / 2d)], true));
            if (gear.Family == GearFamily.Pawl && gear.PivotDiameterMm is > Tol)
            {
                var pivot = Rotate((gear.WidthMm!.Value * .24d, 0d), (gear.EngagementAngleDegrees ?? 0d) * Math.PI / 180d);
                loops.Add(new([new LineArcFullCircle2D(pivot, gear.PivotDiameterMm.Value / 2d)], true));
            }
        }
        var emitted = LineArcProfileExtrudeEmitter.TryEmit(new(loops, gear.FaceWidthMm, LocalStartDepth: 0d, LocalEndDepth: gear.FaceWidthMm));
        errors.AddRange(emitted.Diagnostics.Where(item => item.Contains("Invalid", StringComparison.Ordinal) || item.Contains("Unsupported", StringComparison.Ordinal) || item.Contains("Open", StringComparison.Ordinal)));
        diagnostics = errors;
        if (emitted.Status != LineArcProfileExtrudeStatus.Succeeded || emitted.Body is null) return null;
        var toothIds = gear.Teeth is { } teeth ? Enumerable.Range(0, teeth).Select(index => $"{gear.Name}.Tooth{index}").ToArray() : [$"{gear.Name}.Nose"];
        return new(emitted.Body, curves.Count, involuteCount, maxError,
            gear.Family is GearFamily.SpurGear or GearFamily.InternalSpurGear ? "AnalyticInvolute->CubicHermiteNonRationalBSpline" : "ExactLineArcProfile",
            toothIds);
    }

    public static InterfaceAir<GearAir> EvaluateInterface(string name, GearAir a, GearAir b, double? shaftAngle,
        double? engagementPhase, string? allowedDirection, FirmamentV2SourceSpan span)
    {
        var rejection = new List<string>();
        var ratchetPawl = (a.Family == GearFamily.RatchetGear && b.Family == GearFamily.Pawl)
            || (a.Family == GearFamily.Pawl && b.Family == GearFamily.RatchetGear);
        if (!ratchetPawl && (a.ModuleMm is null || b.ModuleMm is null || Math.Abs(a.ModuleMm.Value - b.ModuleMm.Value) > Tol)) rejection.Add("module-mismatch");
        if (!ratchetPawl && (a.PressureAngleDegrees is null || b.PressureAngleDegrees is null || Math.Abs(a.PressureAngleDegrees.Value - b.PressureAngleDegrees.Value) > Tol)) rejection.Add("pressure-angle-mismatch");
        var internalCount = new[] { a, b }.Count(gear => gear.Family == GearFamily.InternalSpurGear);
        var bevelCount = new[] { a, b }.Count(gear => gear.Family is GearFamily.BevelGear or GearFamily.MiterGear);
        var kind = ratchetPawl ? "RatchetPawl" : internalCount == 1 ? "ExternalInternal" : bevelCount == 2 ? "Bevel" : "ExternalExternal";
        if (ratchetPawl)
        {
            if (allowedDirection is not ("Clockwise" or "CounterClockwise")) rejection.Add("allowed-direction-required");
            if (engagementPhase is null || !double.IsFinite(engagementPhase.Value)) rejection.Add("engagement-phase-required");
        }
        else if (internalCount == 1)
        {
            var internalGear = a.Family == GearFamily.InternalSpurGear ? a : b;
            var externalGear = ReferenceEquals(internalGear, a) ? b : a;
            if (externalGear.Family != GearFamily.SpurGear) rejection.Add("family-mismatch");
            if (internalGear.Teeth <= externalGear.Teeth) rejection.Add("internal-tooth-count-not-greater");
        }
        else if (bevelCount != 0 && bevelCount != 2) rejection.Add("family-mismatch");
        else if (bevelCount == 2)
        {
            var effectiveShaftAngle = shaftAngle ?? 90d;
            var coneA = a.Family == GearFamily.MiterGear ? a.PitchConeAngleDegrees ?? 45d : a.PitchConeAngleDegrees;
            var coneB = b.Family == GearFamily.MiterGear ? b.PitchConeAngleDegrees ?? 45d : b.PitchConeAngleDegrees;
            if (effectiveShaftAngle <= 0d || effectiveShaftAngle >= 180d || !double.IsFinite(effectiveShaftAngle)) rejection.Add("shaft-angle-invalid");
            if (coneA is null || coneB is null || Math.Abs(coneA.Value + coneB.Value - effectiveShaftAngle) > 1e-6) rejection.Add("pitch-cones-do-not-close");
            if ((a.Family == GearFamily.MiterGear || b.Family == GearFamily.MiterGear)
                && (a.Family != GearFamily.MiterGear || b.Family != GearFamily.MiterGear || a.Teeth != b.Teeth)) rejection.Add("miter-requires-equal-pair");
        }
        else if (bevelCount == 0 && (a.Family != GearFamily.SpurGear || b.Family != GearFamily.SpurGear)) rejection.Add("family-mismatch");
        double? center = !ratchetPawl && bevelCount == 0 && a.ModuleMm is { } module && a.Teeth is { } za && b.Teeth is { } zb
            ? module * (internalCount == 1 ? Math.Abs(zb - za) : za + zb) / 2d : null;
        double? ratio = a.Teeth is { } teethA && b.Teeth is { } teethB ? (double)teethA / teethB : null;
        var axisRelation = ratchetPawl ? "EngagementPlane" : bevelCount == 2 ? $"Intersecting:{shaftAngle ?? 90d:R}deg" : "Parallel";
        return new(name, a, b, kind, rejection.Count == 0, center, ratio,
            ratchetPawl ? 0 : internalCount == 1 ? 1 : -1, axisRelation, bevelCount == 2 ? shaftAngle ?? 90d : null,
            engagementPhase, allowedDirection, rejection, span);
    }

    private static void Validate(GearAir gear, List<string> diagnostics)
    {
        void Error(string code) => diagnostics.Add($"{Prefix}{code}:{gear.Name}");
        if (gear.FaceWidthMm <= Tol || !double.IsFinite(gear.FaceWidthMm)) Error("face-width-invalid");
        if (gear.BoreDiameterMm < 0d || !double.IsFinite(gear.BoreDiameterMm)) Error("bore-invalid");
        if (gear.BacklashMm < 0d || !double.IsFinite(gear.BacklashMm)) Error("backlash-invalid");
        if (!double.IsFinite(gear.PhaseDegrees)) Error("phase-invalid");
        if (gear.Axis.Count != 3 || gear.Axis.Any(value => !double.IsFinite(value))
            || Math.Abs(gear.Axis[0]) > Tol || Math.Abs(gear.Axis[1]) > Tol || Math.Abs(gear.Axis[2] - 1d) > Tol) Error("axis-outside-qualified-frame");
        if (gear.HubDiameterMm is not null || gear.HubLengthMm is not null) Error("hub-geometry-not-qualified");
        if (gear.Family == GearFamily.Pawl)
        {
            if (gear.WidthMm is null or <= 0d || gear.LengthMm is null or <= 0d || gear.PivotDiameterMm is null or <= 0d || gear.NoseLengthMm is null or <= 0d) Error("pawl-parameter-invalid");
            else if (gear.PivotDiameterMm >= Math.Min(gear.WidthMm.Value, gear.LengthMm.Value)) Error("pawl-pivot-too-large");
            return;
        }
        if (gear.Teeth is null or <= 0) Error("teeth-invalid");
        if (gear.Family == GearFamily.RatchetGear)
        {
            if (gear.Teeth is < 8) Error("ratchet-teeth-below-qualified-range");
            if (gear.OutsideDiameterMm is null or <= 0d) Error("outside-diameter-invalid");
            else if (gear.BoreDiameterMm >= gear.OutsideDiameterMm) Error("bore-too-large");
            if (gear.DriveFaceAngleDegrees is < -45d or > 45d) Error("drive-face-angle-outside-qualified-range");
            return;
        }
        if (gear.ModuleMm is null or <= 0d || !double.IsFinite(gear.ModuleMm.Value)) Error("module-invalid");
        if (gear.PressureAngleDegrees is null or < 14d or > 30d || !double.IsFinite(gear.PressureAngleDegrees.Value)) Error("pressure-angle-invalid");
        if (gear.Teeth is > 0 && gear.Family != GearFamily.InternalSpurGear && gear.Teeth < 17) Error("undercut-risk:minimum-teeth=17");
        if (gear.PitchRadiusMm is { } pitch && gear.Teeth is { } teeth && gear.BacklashMm >= Math.PI * pitch / teeth) Error("backlash-removes-tooth");
        if (gear.Family == GearFamily.InternalSpurGear)
        {
            if (gear.OutsideDiameterMm is null || gear.RootDiameterMm is null || gear.OutsideDiameterMm <= gear.RootDiameterMm) Error("internal-outside-diameter-too-small");
        }
        else if (gear.RootDiameterMm is { } root && gear.BoreDiameterMm >= root) Error("bore-too-large");
        if (gear.Family is GearFamily.BevelGear or GearFamily.MiterGear)
        {
            var angle = gear.Family == GearFamily.MiterGear ? gear.PitchConeAngleDegrees ?? 45d : gear.PitchConeAngleDegrees;
            if (angle is null or <= 5d or >= 85d) Error("pitch-cone-angle-invalid");
            else if (gear.PitchRadiusMm is { } radius && gear.FaceWidthMm >= radius / Math.Sin(angle.Value * Math.PI / 180d)) Error("face-width-exceeds-cone-distance");
        }
    }

    internal static IReadOnlyList<LineArcProfileCurve2D> ExternalInvoluteProfile(GearAir gear, out int involuteCount, out double maxError)
    {
        var module = gear.ModuleMm!.Value; var teeth = gear.Teeth!.Value; var pressure = gear.PressureAngleDegrees!.Value * Math.PI / 180d;
        var rp = module * teeth / 2d; var rb = rp * Math.Cos(pressure); var ra = rp + module; var rf = rp - 1.25d * module;
        var startRadius = Math.Max(rb, rf); var t0 = InvoluteParameter(rb, startRadius); var ta = InvoluteParameter(rb, ra); var tp = InvoluteParameter(rb, rp);
        var psiPitch = InvoluteAngle(tp); var half = Math.PI / (2d * teeth) - gear.BacklashMm / (2d * rp); var pitch = 2d * Math.PI / teeth;
        var curves = new List<LineArcProfileCurve2D>(); maxError = 0d; involuteCount = 0;
        for (var index = 0; index < teeth; index++)
        {
            var center = gear.PhaseDegrees * Math.PI / 180d + index * pitch;
            var rightRotation = center - half - psiPitch; var leftRotation = center + half + psiPitch;
            var rootRight = Polar(rf, rightRotation + InvoluteAngle(t0)); var baseRight = InvolutePoint(rb, t0, rightRotation, false);
            if (Distance(rootRight, baseRight) > 1e-8) curves.Add(new LineArcLineSegment2D(rootRight, baseRight));
            curves.AddRange(InvoluteBezierSpans(rb, t0, ta, rightRotation, false, out var rightError)); involuteCount += 2; maxError = Math.Max(maxError, rightError);
            var rightTip = InvolutePoint(rb, ta, rightRotation, false); var leftTip = InvolutePoint(rb, ta, leftRotation, true);
            curves.Add(ArcBetween(ra, rightTip, leftTip));
            var left = InvoluteBezierSpans(rb, t0, ta, leftRotation, true, out var leftError).Reverse().Select(Reverse).ToArray(); curves.AddRange(left); involuteCount += 2; maxError = Math.Max(maxError, leftError);
            var baseLeft = InvolutePoint(rb, t0, leftRotation, true); var rootLeft = Polar(rf, leftRotation - InvoluteAngle(t0));
            if (Distance(baseLeft, rootLeft) > 1e-8) curves.Add(new LineArcLineSegment2D(baseLeft, rootLeft));
            var nextCenter = center + pitch; var nextRightRotation = nextCenter - half - psiPitch;
            var nextRootRight = Polar(rf, nextRightRotation + InvoluteAngle(t0));
            curves.Add(ArcBetween(rf, rootLeft, nextRootRight));
        }
        return curves;
    }

    private static IReadOnlyList<LineArcProfileCurve2D> InternalInvoluteProfile(GearAir gear, out int involuteCount, out double maxError)
    {
        var module = gear.ModuleMm!.Value; var teeth = gear.Teeth!.Value; var pressure = gear.PressureAngleDegrees!.Value * Math.PI / 180d;
        var rp = module * teeth / 2d; var rb = rp * Math.Cos(pressure); var tip = rp - module; var root = rp + 1.25d * module;
        var startRadius = Math.Max(rb, tip); var t0 = InvoluteParameter(rb, startRadius); var tr = InvoluteParameter(rb, root); var tp = InvoluteParameter(rb, rp);
        var psiPitch = InvoluteAngle(tp); var half = Math.PI / (2d * teeth) - gear.BacklashMm / (2d * rp); var pitch = 2d * Math.PI / teeth;
        var ccw = new List<LineArcProfileCurve2D>(); maxError = 0d; involuteCount = 0;
        for (var index = 0; index < teeth; index++)
        {
            var center = gear.PhaseDegrees * Math.PI / 180d + index * pitch;
            var rightRotation = center - half + psiPitch; var leftRotation = center + half - psiPitch;
            var tipRight = Polar(tip, rightRotation - InvoluteAngle(t0)); var tipLeft = Polar(tip, leftRotation + InvoluteAngle(t0));
            ccw.Add(ArcBetween(tip, tipRight, tipLeft));
            var baseLeft = InvolutePoint(rb, t0, leftRotation, false);
            if (Distance(tipLeft, baseLeft) > 1e-8) ccw.Add(new LineArcLineSegment2D(tipLeft, baseLeft));
            ccw.AddRange(InvoluteBezierSpans(rb, t0, tr, leftRotation, false, out var leftError)); involuteCount += 2; maxError = Math.Max(maxError, leftError);
            var leftRoot = InvolutePoint(rb, tr, leftRotation, false);
            var nextCenter = center + pitch; var nextRightRotation = nextCenter - half + psiPitch;
            var nextRightRoot = InvolutePoint(rb, tr, nextRightRotation, true);
            ccw.Add(ArcBetween(root, leftRoot, nextRightRoot));
            var right = InvoluteBezierSpans(rb, t0, tr, nextRightRotation, true, out var rightError).Reverse().Select(Reverse).ToArray(); ccw.AddRange(right); involuteCount += 2; maxError = Math.Max(maxError, rightError);
            var baseRight = InvolutePoint(rb, t0, nextRightRotation, true); var nextTipRight = Polar(tip, nextRightRotation - InvoluteAngle(t0));
            if (Distance(baseRight, nextTipRight) > 1e-8) ccw.Add(new LineArcLineSegment2D(baseRight, nextTipRight));
        }
        return ccw.Reverse<LineArcProfileCurve2D>().Select(Reverse).ToArray();
    }

    private static IReadOnlyList<LineArcProfileCurve2D> RatchetProfile(GearAir gear, out int involuteCount, out double maxError)
    {
        involuteCount = 0; maxError = 0d; var teeth = gear.Teeth!.Value; var outer = gear.OutsideDiameterMm!.Value / 2d;
        var root = Math.Max(gear.BoreDiameterMm / 2d + outer * .12d, outer * .78d); var pitch = 2d * Math.PI / teeth; var phase = gear.PhaseDegrees * Math.PI / 180d;
        var driveAngle = (gear.DriveFaceAngleDegrees ?? 0d) * Math.PI / 180d;
        var drive = Math.Atan(Math.Tan(driveAngle) * (outer - root) / outer);
        if (Math.Abs(drive) >= pitch * .25d) drive = Math.CopySign(pitch * .25d, drive);
        var result = new List<LineArcProfileCurve2D>();
        for (var index = 0; index < teeth; index++)
        {
            var a = phase + index * pitch; var tipEnd = a + drive + pitch * .08d; var next = a + pitch;
            result.Add(new LineArcLineSegment2D(Polar(root, a), Polar(outer, a + drive)));
            result.Add(new LineArcCircularArc2D((0d, 0d), outer, a + drive, tipEnd - a - drive));
            result.Add(new LineArcLineSegment2D(Polar(outer, tipEnd), Polar(root, next)));
        }
        return result;
    }

    private static IReadOnlyList<LineArcProfileCurve2D> PawlProfile(GearAir gear, out int involuteCount, out double maxError)
    {
        involuteCount = 0; maxError = 0d; var width = gear.WidthMm!.Value; var length = gear.LengthMm!.Value; var nose = gear.NoseLengthMm!.Value;
        var angle = (gear.EngagementAngleDegrees ?? 0d) * Math.PI / 180d;
        var points = new[] { (-width / 2d, -length * .34d), (width * .28d, -length * .34d), (width / 2d + nose, 0d), (width * .28d, length * .34d), (-width / 2d, length * .34d) }
            .Select(point => Rotate(point, angle)).ToArray();
        return Enumerable.Range(0, points.Length).Select(index => (LineArcProfileCurve2D)new LineArcLineSegment2D(points[index], points[(index + 1) % points.Length])).ToArray();
    }

    private static IReadOnlyList<LineArcCubicBezier2D> InvoluteBezierSpans(double baseRadius, double from, double to, double rotation, bool mirror, out double maximumError)
    {
        const int spans = 2; var result = new List<LineArcCubicBezier2D>(spans); maximumError = 0d;
        for (var index = 0; index < spans; index++)
        {
            var a = from + (to - from) * index / spans; var b = from + (to - from) * (index + 1) / spans; var dt = b - a;
            var p0 = InvolutePoint(baseRadius, a, rotation, mirror); var p3 = InvolutePoint(baseRadius, b, rotation, mirror);
            var d0 = InvoluteDerivative(baseRadius, a, rotation, mirror); var d1 = InvoluteDerivative(baseRadius, b, rotation, mirror);
            var curve = new LineArcCubicBezier2D(p0, Add(p0, Scale(d0, dt / 3d)), Add(p3, Scale(d1, -dt / 3d)), p3); result.Add(curve);
            for (var sample = 1; sample < 8; sample++)
            {
                var u = sample / 8d; var exact = InvolutePoint(baseRadius, a + dt * u, rotation, mirror); var approx = Bezier(curve, u);
                maximumError = Math.Max(maximumError, Distance(exact, approx));
            }
        }
        return result;
    }

    private static (double X, double Y) InvolutePoint(double rb, double t, double rotation, bool mirror)
    {
        var x = rb * (Math.Cos(t) + t * Math.Sin(t)); var y = rb * (Math.Sin(t) - t * Math.Cos(t)); if (mirror) y = -y; return Rotate((x, y), rotation);
    }
    private static (double X, double Y) InvoluteDerivative(double rb, double t, double rotation, bool mirror)
    { var x = rb * t * Math.Cos(t); var y = rb * t * Math.Sin(t); if (mirror) y = -y; return Rotate((x, y), rotation); }
    private static double InvoluteParameter(double rb, double radius) => Math.Sqrt(Math.Max(0d, radius * radius / (rb * rb) - 1d));
    private static double InvoluteAngle(double t) => t - Math.Atan(t);
    private static LineArcCircularArc2D ArcBetween(double radius, (double X, double Y) start, (double X, double Y) end)
    { var a = Math.Atan2(start.Y, start.X); var b = Math.Atan2(end.Y, end.X); while (b <= a) b += 2d * Math.PI; return new((0d, 0d), radius, a, b - a); }
    private static LineArcProfileCurve2D Reverse(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => new LineArcLineSegment2D(line.End, line.Start),
        LineArcCircularArc2D arc => new LineArcCircularArc2D(arc.Center, arc.Radius, arc.StartAngleRadians + arc.SweepAngleRadians, -arc.SweepAngleRadians),
        LineArcCubicBezier2D bezier => new LineArcCubicBezier2D(bezier.End, bezier.Control2, bezier.Control1, bezier.Start),
        _ => throw new InvalidOperationException("gear-profile-reverse-unsupported")
    };
    private static (double X, double Y) Polar(double radius, double angle) => (radius * Math.Cos(angle), radius * Math.Sin(angle));
    private static (double X, double Y) Rotate((double X, double Y) point, double angle) => (point.X * Math.Cos(angle) - point.Y * Math.Sin(angle), point.X * Math.Sin(angle) + point.Y * Math.Cos(angle));
    private static (double X, double Y) Add((double X, double Y) a, (double X, double Y) b) => (a.X + b.X, a.Y + b.Y);
    private static (double X, double Y) Scale((double X, double Y) value, double scale) => (value.X * scale, value.Y * scale);
    private static (double X, double Y) Bezier(LineArcCubicBezier2D curve, double t)
    { var s = 1d - t; return (s*s*s*curve.Start.X + 3*s*s*t*curve.Control1.X + 3*s*t*t*curve.Control2.X + t*t*t*curve.End.X, s*s*s*curve.Start.Y + 3*s*s*t*curve.Control1.Y + 3*s*t*t*curve.Control2.Y + t*t*t*curve.End.Y); }
    private static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));
    private static double? Length(string body, string field) => NumberField(body, field, "mm");
    private static double? Angle(string body, string field) => NumberField(body, field, "deg");
    private static double? NumberField(string body, string field, string unit)
    { var match = Regex.Match(body, $@"\b{Regex.Escape(field)}\s*:\s*(?<value>[-+.\deE]+)\s*{unit}\b", RegexOptions.CultureInvariant); return match.Success && double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null; }
    private static int? Integer(string body, string field)
    { var match = Regex.Match(body, $@"\b{Regex.Escape(field)}\s*:\s*(?<value>[-+]?\d+)\b", RegexOptions.CultureInvariant); return match.Success && int.TryParse(match.Groups["value"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null; }
    private static string? Identifier(string body, string field)
    { var match = Regex.Match(body, $@"\b{Regex.Escape(field)}\s*:\s*(?<value>[A-Za-z_]\w*)\b", RegexOptions.CultureInvariant); return match.Success ? match.Groups["value"].Value : null; }
    private static IReadOnlyList<double>? Vector3(string body, string field)
    {
        var match = Regex.Match(body, $@"\b{Regex.Escape(field)}\s*:\s*\[\s*(?<x>[-+.\deE]+)\s*,\s*(?<y>[-+.\deE]+)\s*,\s*(?<z>[-+.\deE]+)\s*\]", RegexOptions.CultureInvariant);
        return match.Success ? [Number(match.Groups["x"].Value), Number(match.Groups["y"].Value), Number(match.Groups["z"].Value)] : null;
    }
    private static double Number(string value) => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    private static int MatchingBrace(string source, int open)
    { var depth = 0; for (var index = open; index >= 0 && index < source.Length; index++) { if (source[index] == '{') depth++; else if (source[index] == '}' && --depth == 0) return index; } return -1; }
}

internal sealed record GearBevelMaterializationResult(GearMaterialization? Materialization, IReadOnlyList<string> Diagnostics);

/// <summary>Implemented in the bounded straight-bevel loft lane; never a scaled extruded mesh.</summary>
internal static class GearBevelLoftMaterializer
{
    public static GearBevelMaterializationResult TryMaterialize(GearAir gear)
    {
        var angle = (gear.Family == GearFamily.MiterGear ? gear.PitchConeAngleDegrees ?? 45d : gear.PitchConeAngleDegrees!.Value) * Math.PI / 180d;
        var outer = GearAuthoring.ExternalInvoluteProfile(gear, out var involuteCount, out var maxError);
        var loops = new List<LineArcProfileLoop2D> { new(outer, false) };
        if (gear.BoreDiameterMm > 1e-9) loops.Add(new([new LineArcFullCircle2D((0d, 0d), gear.BoreDiameterMm / 2d)], true));
        var pitchRadius = gear.PitchRadiusMm!.Value;
        var coneDistance = pitchRadius / Math.Sin(angle);
        var scale = (coneDistance - gear.FaceWidthMm) / coneDistance;
        var axialWidth = gear.FaceWidthMm * Math.Cos(angle);
        var planned = ProfileExtrusionBRepPlanner.TryPlan(new(loops, axialWidth, LocalStartDepth: 0d, LocalEndDepth: axialWidth));
        if (!planned.Succeeded || planned.Plan is null) return new(null, planned.Diagnostics);
        var plan = planned.Plan;

        bool Outer(string source) => source.Contains(".Loop0.", StringComparison.Ordinal);
        Point3D ScaleEnd(Point3D point) => new(point.X * scale, point.Y * scale, point.Z);
        var vertices = plan.Vertices.Select(vertex => vertex.Role == ProfileExtrusionPlanRole.LocalEndVertex && Outer(vertex.SourceStableId)
            ? vertex with { WorldPoint = ScaleEnd(vertex.WorldPoint) } : vertex).ToArray();
        var vertexById = vertices.ToDictionary(vertex => vertex.Id);
        var edgeByCurve = plan.Edges.ToDictionary(edge => edge.CurveId);
        var curves = plan.Curves.Select(curve =>
        {
            var edge = edgeByCurve[curve.Id]; var a = vertexById[edge.StartVertexId].WorldPoint; var b = vertexById[edge.EndVertexId].WorldPoint;
            if (curve.StableId.EndsWith(":curve:longitudinal", StringComparison.Ordinal))
                return curve with { Geometry = CurveGeometry.FromLine(new Line3Curve(a, Direction3D.Create(b - a))), Trim = new(0d, (b - a).Length) };
            if (!curve.StableId.EndsWith(":curve:local-end", StringComparison.Ordinal) || !Outer(curve.SourceStableId)) return curve;
            return curve.Geometry.Kind switch
            {
                CurveGeometryKind.Line3 => curve with { Geometry = CurveGeometry.FromLine(new Line3Curve(a, Direction3D.Create(b - a))), Trim = new(0d, (b - a).Length) },
                CurveGeometryKind.Circle3 when curve.Geometry.Circle3 is { } circle => curve with
                {
                    Geometry = CurveGeometry.FromCircle(new Circle3Curve(ScaleEnd(circle.Center), circle.Normal, circle.Radius * scale, circle.XAxis))
                },
                CurveGeometryKind.BSpline3 when curve.Geometry.BSpline3 is { } spline => curve with
                {
                    Geometry = CurveGeometry.FromBSpline(new BSpline3Curve(spline.Degree, spline.ControlPoints.Select(ScaleEnd).ToArray(),
                        spline.KnotMultiplicities, spline.KnotValues, spline.CurveForm, spline.ClosedCurve, spline.SelfIntersect, spline.KnotSpec))
                },
                _ => curve
            };
        }).ToArray();

        var sideOrdinal = 0;
        var surfaces = plan.Surfaces.Select(surface =>
        {
            if (surface.Role != ProfileExtrusionPlanRole.SideFace) return surface;
            var sourceCurve = loops.SelectMany(loop => loop.Curves).ElementAt(sideOrdinal++);
            if (!Outer(surface.SourceStableId)) return surface;
            SurfaceGeometry geometry = sourceCurve switch
            {
                LineArcLineSegment2D line => PlaneFor(line),
                LineArcCircularArc2D arc => SurfaceGeometry.FromCone(new ConeSurface(new Point3D(0d, 0d, 0d),
                    Direction3D.Create(new Vector3D(0d, 0d, -1d)), arc.Radius, angle, Direction3D.Create(new Vector3D(1d, 0d, 0d)))),
                LineArcCubicBezier2D bezier => SurfaceGeometry.FromBSplineSurfaceWithKnots(new BSplineSurfaceWithKnots(3, 1,
                    new[] { bezier.Start, bezier.Control1, bezier.Control2, bezier.End }
                        .Select(point => (IReadOnlyList<Point3D>)[new Point3D(point.X, point.Y, 0d), new Point3D(point.X * scale, point.Y * scale, axialWidth)]).ToArray(),
                    "RULED_SURF", false, false, false, [4, 4], [2, 2], [0d, 1d], [0d, 1d], "PIECEWISE_BEZIER_KNOTS")),
                _ => surface.Geometry
            };
            return surface with { Geometry = geometry };

            SurfaceGeometry PlaneFor(LineArcLineSegment2D line)
            {
                var p0 = new Point3D(line.Start.X, line.Start.Y, 0d); var p1 = new Point3D(line.End.X, line.End.Y, 0d);
                var p2 = new Point3D(line.Start.X * scale, line.Start.Y * scale, axialWidth);
                var normal = Direction3D.Create((p1 - p0).Cross(p2 - p0));
                return SurfaceGeometry.FromPlane(new PlaneSurface(p0, normal, Direction3D.Create(p1 - p0)));
            }
        }).ToArray();

        var transformed = plan with { Vertices = vertices, Curves = curves, Surfaces = surfaces,
            StableId = $"brep-plan:straight-bevel:{gear.Name}:{gear.Teeth}:{gear.FaceWidthMm:R}" };
        var materialized = ProfileExtrusionBRepMaterializer.TryMaterialize(transformed);
        if (!materialized.Succeeded || materialized.Body is null) return new(null, materialized.Diagnostics);
        return new(new(materialized.Body, outer.Count, involuteCount, maxError,
            "AnalyticInvoluteSections->StraightPitchConeRuledNonRationalBSpline",
            Enumerable.Range(0, gear.Teeth!.Value).Select(index => $"{gear.Name}.Tooth{index}").ToArray()), materialized.Diagnostics);
    }
}
