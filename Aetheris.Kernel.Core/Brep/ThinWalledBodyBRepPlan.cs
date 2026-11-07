using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Geometry.Planar;

namespace Aetheris.Kernel.Core.Brep;

/// <summary>
/// Single topology authority for an admitted open thin-walled vessel.  The body is a closed
/// manifold boundary around material; the top is an opening into its cavity, closed by a rim.
/// It is intentionally built from paired supports, never inferred from occupancy subtraction.
/// </summary>
public sealed record ThinWalledBodyBRepPlan(
    string PrimitiveKind,
    IReadOnlyList<string> OuterFaces,
    IReadOnlyList<string> InnerFaces,
    IReadOnlyList<string> RimFaces,
    IReadOnlyList<string> ClosedFaces,
    IReadOnlyList<string> OpeningLoops,
    IReadOnlyList<ThinWallThicknessWitness> ThicknessWitnesses,
    string DeterministicSignature)
{
    public bool IsAuthoritative => true;
    public string Kind => "ThinWalledBody";
}

public sealed record ThinWalledBodyRealization(
    HollowBodyFeature Feature,
    ThinWalledBodyConstruction Construction,
    ThinWalledBodyBRepPlan Plan,
    BrepBody Body);

public static class ThinWalledBodyBRepPlanner
{
    private const double Tol = 1e-8;
    private static readonly Direction3D PlusX = Direction3D.Create(new Vector3D(1, 0, 0));
    private static readonly Direction3D PlusY = Direction3D.Create(new Vector3D(0, 1, 0));
    private static readonly Direction3D PlusZ = Direction3D.Create(new Vector3D(0, 0, 1));
    private static readonly Direction3D MinusZ = Direction3D.Create(new Vector3D(0, 0, -1));

    public static KernelResult<ThinWalledBodyRealization> CreateFrustum(double bottomRadius, double topRadius, double height, double wallThickness)
    {
        if (!FinitePositive(bottomRadius, topRadius, height, wallThickness) || double.Abs(topRadius - bottomRadius) <= Tol) return Failure("FrustumOffsetConeDegenerate");
        var k = (topRadius - bottomRadius) / height;
        var normalScale = double.Sqrt(1d + k * k);
        // Parallel-line form at fixed z.  The horizontal decrement is larger than T for a sloped cone,
        // which is precisely why this does not use naive radial shrinking.
        var innerBottom = bottomRadius + k * wallThickness - wallThickness * normalScale;
        var innerTop = topRadius - wallThickness * normalScale;
        if (innerBottom <= Tol) return Failure("FrustumInnerBottomInvalid");
        if (innerTop <= Tol) return Failure("FrustumInnerTopInvalid");
        var witness = new HollowConstructionWitness("ParallelConicalOffset", true,
            "rInner(z)=Rb+kz-T*sqrt(1+k^2), trimmed at z=T and z=H",
            ["OuterConicalWall -> InnerConicalWall", "OuterBottomPlane -> InnerBottomPlane"],
            ["T>0", "frustum is non-cylindrical", "inner bottom radius>0", "inner top radius>0"]);
        var feature = new HollowBodyFeature("Frustum", new Dictionary<string, double> { ["BottomRadius"] = bottomRadius, ["TopRadius"] = topRadius, ["Height"] = height }, wallThickness, ["Top"], "ConstantNormalThickness", witness, "Firmament Primitive<Hollow>");
        var thickness = new[]
        {
            new ThinWallThicknessWitness("ConicalWall", "Cone", "Cone", wallThickness, "inward support normal"),
            new ThinWallThicknessWitness("Bottom", "Plane", "Plane", wallThickness, "+Z")
        };
        var construction = new ThinWalledBodyConstruction(feature, ["OuterConicalWall", "OuterBottom"], ["InnerConicalWall", "InnerBottom"], ["TopAnnularRim"], ["OuterBottom", "InnerBottom"], thickness);
        var plan = MakePlan("Frustum", construction, 2, 2, 1, 2, $"{bottomRadius:R}|{topRadius:R}|{height:R}|{wallThickness:R}|{innerBottom:R}|{innerTop:R}");
        return Realize(feature, construction, plan, [Line(0, 0, bottomRadius, 0), Line(bottomRadius, 0, topRadius, height), Line(topRadius, height, 0, height), Line(0, height, 0, 0)], wallThickness);
    }

    public static KernelResult<ThinWalledBodyRealization> CreateCylinder(double radius, double height, double wallThickness, double bottomBlendRadius = 0)
    {
        if (!FinitePositive(radius, height, wallThickness) || radius <= wallThickness + Tol || height <= wallThickness + Tol)
            return Failure("CylinderInnerBoundaryDegenerate");
        if (!double.IsFinite(bottomBlendRadius) || bottomBlendRadius < 0 ||
            (bottomBlendRadius > 0 && (bottomBlendRadius <= wallThickness + Tol || bottomBlendRadius >= radius - Tol || bottomBlendRadius >= height - Tol)))
            return Failure("CylinderBottomBlendInvalid");
        var blended = bottomBlendRadius > 0;
        var witness = new HollowConstructionWitness("CoaxialCylindricalOffset", true,
            blended ? "innerRadius=outerRadius-T; innerBottom=T; paired bottom torus radii=Rblend and Rblend-T; common major radius=outerRadius-Rblend" :
                "innerRadius=outerRadius-T; innerBottom=outerBottom+T; innerTop=outerTop",
            blended ? ["OuterCylindricalWall -> InnerCylindricalWall", "OuterBottomBlend -> InnerBottomBlend", "OuterBottomPlane -> InnerBottomPlane"] :
                ["OuterCylindricalWall -> InnerCylindricalWall", "OuterBottomPlane -> InnerBottomPlane"],
            blended ? ["T>0", "radius>Rblend>T", "height>Rblend"] : ["T>0", "radius>T", "height>T"]);
        var feature = new HollowBodyFeature("Cylinder", new Dictionary<string, double> { ["Radius"] = radius, ["Height"] = height, ["BottomBlendRadius"] = bottomBlendRadius },
            wallThickness, ["Top"], "ConstantNormalThickness", witness, "Firmament Primitive<Hollow>");
        ThinWallThicknessWitness[] thickness =
        [
            new("CylindricalWall", "Cylinder", "Cylinder", wallThickness, "coaxial radial inward"),
            new("Bottom", "Plane", "Plane", wallThickness, "+Z")
        ];
        if (blended) thickness = [.. thickness, new("BottomBlend", "Torus", "Torus", wallThickness, "paired normal offset")];
        var construction = new ThinWalledBodyConstruction(feature,
            blended ? ["OuterCylindricalWall", "OuterBottomBlend", "OuterBottom"] : ["OuterCylindricalWall", "OuterBottom"],
            blended ? ["InnerCylindricalWall", "InnerBottomBlend", "InnerBottom"] : ["InnerCylindricalWall", "InnerBottom"],
            ["TopAnnularRim"], ["OuterBottom", "InnerBottom"], thickness);
        var plan = MakePlan("Cylinder", construction, blended ? 3 : 2, blended ? 3 : 2, 1, 2, $"{radius:R}|{height:R}|{wallThickness:R}|{bottomBlendRadius:R}");
        PlanarSpan[] meridian = blended
            ? [Line(0, 0, radius - bottomBlendRadius, 0), new PlanarArc((radius - bottomBlendRadius, bottomBlendRadius), bottomBlendRadius, -double.Pi / 2d, double.Pi / 2d),
                Line(radius, bottomBlendRadius, radius, height), Line(radius, height, 0, height), Line(0, height, 0, 0)]
            : [Line(0, 0, radius, 0), Line(radius, 0, radius, height), Line(radius, height, 0, height), Line(0, height, 0, 0)];
        return Realize(feature, construction, plan, meridian, wallThickness);
    }

    /// <summary>
    /// The primitive's body is its exterior meridian hollowed and turned by the general routes: the primitive owns
    /// only its admission and its witness, never its own topology.
    /// </summary>
    private static KernelResult<ThinWalledBodyRealization> Realize(HollowBodyFeature feature, ThinWalledBodyConstruction construction, ThinWalledBodyBRepPlan plan,
        IReadOnlyList<PlanarSpan> exteriorMeridian, double wallThickness)
    {
        var wall = MeridianHollow.Wall(exteriorMeridian, wallThickness, openTop: true, openBottom: false);
        if (!wall.IsSuccess) return KernelResult<ThinWalledBodyRealization>.Failure(wall.Diagnostics);
        var shell = BrepRevolvedShell.Build(wall.Value, RevolutionFrame.WorldZ);
        if (!shell.IsSuccess) return KernelResult<ThinWalledBodyRealization>.Failure(shell.Diagnostics);
        return KernelResult<ThinWalledBodyRealization>.Success(new ThinWalledBodyRealization(feature, construction, plan, shell.Value.Body));
    }

    private static PlanarLine Line(double r0, double z0, double r1, double z1) => new((r0, z0), (r1, z1));

    public static ThinWalledBodyBRepPlan MakePlan(string primitive, ThinWalledBodyConstruction construction, int outer, int inner, int rim, int closed, string parameters)
    {
        var outerFaces = construction.OuterBoundaryRoles; var innerFaces = construction.InnerBoundaryRoles; var rimFaces = construction.RimRoles; var closedFaces = construction.ClosedRegionRoles;
        var source = $"ThinWalledBody|{primitive}|{parameters}|{string.Join(';', outerFaces)}|{string.Join(';', innerFaces)}|{string.Join(';', rimFaces)}";
        return new(primitive, outerFaces, innerFaces, rimFaces, closedFaces, ["Opening(Top)"], construction.ThicknessWitnesses, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant());
    }

    private static bool FinitePositive(params double[] values) => values.All(v => double.IsFinite(v) && v > Tol);
    private static KernelResult<ThinWalledBodyRealization> Failure(string code) => KernelResult<ThinWalledBodyRealization>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, code, "ThinWalledBody.Admission")]);
}
