using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>
/// Hollow primitives that are section stacks. Like the revolved ones in <see cref="ThinWalledBodyBRepPlanner"/>,
/// a primitive owns its admission and its witness; its body comes from the general hollow route.
/// </summary>
public static class HollowPrimitives
{
    private const double Tol = 1e-8;

    public static KernelResult<ThinWalledBodyRealization> RoundedBox(double width, double depth, double height, double cornerRadius, double wallThickness)
    {
        if (!FinitePositive(width, depth, height, cornerRadius, wallThickness)) return Failure("RoundedBoxInnerBoundaryDegenerate");
        if (width <= 2d * wallThickness + Tol || depth <= 2d * wallThickness + Tol || height <= wallThickness + Tol) return Failure("WallThicknessTooLarge");
        if (cornerRadius <= wallThickness + Tol) return Failure("RoundedBoxInnerRadiusDegenerate");
        if (cornerRadius >= double.Min(width, depth) / 2d - Tol) return Failure("RoundedBoxRadiusTooLarge");

        var witness = new HollowConstructionWitness("InsetRoundedProfile", true,
            "innerWidth=outerWidth-2T; innerDepth=outerDepth-2T; innerRadius=outerRadius-T; innerBottom=outerBottom+T; innerTop=outerTop",
            ["OuterPlanarWall -> InnerPlanarWall", "OuterCornerCylinder -> InnerCornerCylinder", "OuterBottomPlane -> InnerBottomPlane"],
            ["T>0", "width>2T", "depth>2T", "height>T", "cornerRadius>T"]);
        var feature = new HollowBodyFeature("RoundedBox", new Dictionary<string, double> { ["Width"] = width, ["Depth"] = depth, ["Height"] = height, ["CornerRadius"] = cornerRadius }, wallThickness, ["Top"], "ConstantNormalThickness", witness, "Firmament Primitive<Hollow>");
        var thickness = new List<ThinWallThicknessWitness>();
        thickness.AddRange(Enumerable.Range(0, 4).Select(i => new ThinWallThicknessWitness($"PlanarWall({i})", "Plane", "Plane", wallThickness, "inward normal")));
        thickness.AddRange(Enumerable.Range(0, 4).Select(i => new ThinWallThicknessWitness($"CornerWall({i})", "Cylinder", "Cylinder", wallThickness, "coaxial radial inward")));
        thickness.Add(new ThinWallThicknessWitness("Bottom", "Plane", "Plane", wallThickness, "+Z"));
        var construction = new ThinWalledBodyConstruction(feature,
            ["OuterBottom", .. Enumerable.Range(0, 8).Select(i => $"OuterWall({i})")],
            ["InnerBottom", .. Enumerable.Range(0, 8).Select(i => $"InnerWall({i})")],
            Enumerable.Range(0, 8).Select(i => $"Rim({i})").ToArray(), ["OuterBottom", "InnerBottom"], thickness);
        var plan = ThinWalledBodyBRepPlanner.MakePlan("RoundedBox", construction, 8, 8, 8, 2, $"{width:R}|{depth:R}|{height:R}|{cornerRadius:R}|{wallThickness:R}");
        // The body is the rounded-rectangle extrusion hollowed through the general section-stack route.
        var outline = RoundedRectangle("HollowRoundedBox.Outline", width, depth, cornerRadius);
        var operations = new[] { new PrismaticProfileOperation("Stock", PrismaticProfileIntent.Base, outline.Name, 0d, height, "Stock", "primitive", null, null) };
        var composition = new PrismaticProfileCompositionFeature("RoundedBox", "XY", "+Z",
            new PrismaticProfilePlacement("LegacyImplicitWorldXY", 0d, 0d, 0d, "XY", "+Z", "+X", false), operations, [0d, height], "primitive-hollow")
            { Hollow = new PrismaticHollowIntent(wallThickness, OpenTop: true, OpenBottom: false, "primitive") };
        var stack = PrismaticSectionStackCompiler.Normalize(new PrismaticProfileCompositionParseResult(composition, new Dictionary<string, ResolvedProfile2D> { [outline.Name] = outline }, []), out var stackDiagnostics);
        if (stack is null) return Failure(string.Join("; ", stackDiagnostics));
        var emitted = PrismaticSectionStackEmitter.Emit(stack);
        if (emitted.Body is null) return Failure(string.Join("; ", emitted.Diagnostics));
        return KernelResult<ThinWalledBodyRealization>.Success(new ThinWalledBodyRealization(feature, construction, plan, BrepSplitFaceMerge.Merge(emitted.Body)));
    }

    private static ResolvedProfile2D RoundedRectangle(string name, double width, double depth, double radius)
    {
        var (x, y, r) = (width / 2d, depth / 2d, radius);
        LineArcProfileCurve2D[] curves =
        [
            new LineArcLineSegment2D((-x + r, -y), (x - r, -y)),
            new LineArcCircularArc2D((x - r, -y + r), r, -double.Pi / 2d, double.Pi / 2d),
            new LineArcLineSegment2D((x, -y + r), (x, y - r)),
            new LineArcCircularArc2D((x - r, y - r), r, 0d, double.Pi / 2d),
            new LineArcLineSegment2D((x - r, y), (-x + r, y)),
            new LineArcCircularArc2D((-x + r, y - r), r, double.Pi / 2d, double.Pi / 2d),
            new LineArcLineSegment2D((-x, y - r), (-x, -y + r)),
            new LineArcCircularArc2D((-x + r, -y + r), r, double.Pi, double.Pi / 2d),
        ];
        return new ResolvedProfile2D(name, "XY", [new ResolvedProfileLoop2D($"{name}.Outer", true, curves.Select((curve, i) =>
            new ResolvedProfileSegment2D($"{name}.{i}", curve, new ProfileSegmentProvenance($"{name}.{i}", name, "generated", "hollow-primitive", "XY"))).ToArray())]);
    }

    private static bool FinitePositive(params double[] values) => values.All(v => double.IsFinite(v) && v > Tol);
    private static KernelResult<ThinWalledBodyRealization> Failure(string code) => KernelResult<ThinWalledBodyRealization>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, code, "ThinWalledBody.Admission")]);
}
