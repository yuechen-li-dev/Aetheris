using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Surfacing;

public sealed record SectionChainGeometricJoinEvidence(string BoundaryIdentity, string BoundaryKind,
    double MaximumPositionError, double MaximumNormalAngleDegrees, double? MaximumShapeOperatorResidual,
    int SampleCount, string Status, string Method);

/// <summary>Geometric inspection of realized polynomial patches, independent of continuity labels.</summary>
internal static class SectionChainDifferentialInspection
{
    public static SectionChainGeometricJoinEvidence Measure(string identity, string kind,
        SmoothSectionChainTransitionPatch incoming, SmoothSectionChainTransitionPatch outgoing, bool acrossSections)
    {
        var position=0d;var angle=0d;var curvature=0d;var regular=true;
        // Include normalized arc knot boundaries as well as the regular sample grid.
        var samples=Enumerable.Range(0,33).Select(i=>i/32d)
            .Concat(acrossSections?incoming.Spline.KnotValuesU:incoming.Spline.KnotValuesV).Distinct().Order().ToArray();
        foreach(var t in samples)
        {
            var a=Jet(incoming.Spline,acrossSections?t:1,acrossSections?1:t);
            var b=Jet(outgoing.Spline,acrossSections?t:0,acrossSections?0:t);
            position=Math.Max(position,(a.P-b.P).Length);
            if (!a.Du.Cross(a.Dv).TryNormalize(out var na) || !b.Du.Cross(b.Dv).TryNormalize(out var nb))
            { regular=false;continue; }
            angle=Math.Max(angle,Math.Atan2(na.Cross(nb).Length,na.Dot(nb))*180/Math.PI);
            var tangent=acrossSections?a.Du:a.Dv;
            if(!tangent.TryNormalize(out var e1)){regular=false;continue;}
            var e2=na.Cross(e1);
            // Compare the second fundamental forms in one orthonormal world tangent basis.
            // This is invariant to parameter speed; a raw d2/dv2 comparison would not be G2.
            if (!Form(a,na,e1,e2,out var aa) || !Form(b,nb,e1,e2,out var bb)) {regular=false;continue;}
            curvature=Math.Max(curvature,Math.Sqrt(Math.Pow(aa.K11-bb.K11,2)+2*Math.Pow(aa.K12-bb.K12,2)+Math.Pow(aa.K22-bb.K22,2)));
        }
        var status=!regular?"Singular":position>1e-7?"BelowG0":angle>1e-3?"G0":curvature>1e-6?"G1":"G2WithinSampledTolerance";
        return new(identity,kind,position,angle,regular?curvature:null,samples.Length,status,
            "Analytic tensor B-spline first/second derivatives; Frobenius residual of second fundamental forms in a shared orthonormal world tangent basis. Curvature status requires G0/G1 first. Sampling is not a global proof.");
    }

    private static bool Form(PatchJet j,Vector3D n,Vector3D e1,Vector3D e2,out (double K11,double K12,double K22) form)
    {
        form=default;var E=j.Du.Dot(j.Du);var F=j.Du.Dot(j.Dv);var G=j.Dv.Dot(j.Dv);var determinant=E*G-F*F;
        if(!double.IsFinite(determinant)||determinant<=1e-20)return false;
        (double U,double V) Map(Vector3D v)=>( (G*j.Du.Dot(v)-F*j.Dv.Dot(v))/determinant, (E*j.Dv.Dot(v)-F*j.Du.Dot(v))/determinant );
        var a=Map(e1);var b=Map(e2);var l=n.Dot(j.Duu);var m=n.Dot(j.Duv);var r=n.Dot(j.Dvv);
        double B((double U,double V) x,(double U,double V) y)=>l*x.U*y.U+m*(x.U*y.V+x.V*y.U)+r*x.V*y.V;
        form=(B(a,a),B(a,b),B(b,b));return double.IsFinite(form.K11)&&double.IsFinite(form.K12)&&double.IsFinite(form.K22);
    }
    private static PatchJet Jet(BSplineSurfaceWithKnots s,double u,double v)
    {
        BSpline3Curve U(IReadOnlyList<Point3D> controls)=>new(s.DegreeU,controls,s.KnotMultiplicitiesU,s.KnotValuesU,"UNSPECIFIED",false,false,"UNSPECIFIED");
        var rows=s.ControlPoints.Select(row=>new BSpline3Curve(s.DegreeV,row,s.KnotMultiplicitiesV,s.KnotValuesV,"UNSPECIFIED",false,false,"UNSPECIFIED")).ToArray();
        var value=U(rows.Select(c=>c.Evaluate(v)).ToArray());
        var first=U(rows.Select(c=>Point3D.Origin+c.EvaluateTangent(v)).ToArray());
        var second=U(rows.Select(c=>Point3D.Origin+c.EvaluateSecondDerivative(v)).ToArray());
        return new(value.Evaluate(u),value.EvaluateTangent(u),first.Evaluate(u)-Point3D.Origin,
            value.EvaluateSecondDerivative(u),first.EvaluateTangent(u),second.Evaluate(u)-Point3D.Origin);
    }
    private sealed record PatchJet(Point3D P,Vector3D Du,Vector3D Dv,Vector3D Duu,Vector3D Duv,Vector3D Dvv);
}
