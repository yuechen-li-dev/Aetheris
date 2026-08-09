using Aetheris.Continuum.Cir;
using Aetheris.Continuum.Lattice;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Boundaries;

/// <summary>
/// Builds the same small tangent-graph representation used by the isolated M1-M3 paths directly
/// from a whole-shell face. Exact BRep support remains the differential authority, while CIR probes
/// validate material side and trim ownership. No tessellation is consulted.
/// </summary>
internal static class WholePartBoundaryMapFactory
{
    private readonly record struct Point2(double U, double V);

    public static bool TryBuild(CellIndex index, BoundingBox3D cell, IContinuumRegion region, WholeShellBoundaryQuery shell,
        WholeShellBoundaryCandidate face, MaterialSideEvidence side, BoundaryEvaluationCache cache,
        out SampledBoundaryOffsetMap? map, out string rejection)
    {
        map = null; rejection = string.Empty;
        if (face.SupportKind is not (SurfaceGeometryKind.Cylinder or SurfaceGeometryKind.Cone or SurfaceGeometryKind.Torus))
        { rejection = "support has a direct non-map path"; return false; }
        if (side.MaterialSideNormal is not Vector3D materialNormal)
        { rejection = "material-side evidence is unresolved"; return false; }

        try
        {
            var center = Center(cell);
            var origin = ExactSupportBoundaryQuery.ProjectToSupport(shell.Body, face.FaceId, center, shell.Transform);
            var support = shell.Body.GetFaceSurface(face.FaceId);
            var tangentU = GeneratorDirection(support, origin, shell.Transform, materialNormal);
            tangentU -= materialNormal * tangentU.Dot(materialNormal);
            if (!tangentU.TryNormalize(out tangentU)) throw new InvalidOperationException("support-local generator is degenerate");
            var tangentV = materialNormal.Cross(tangentU);
            if (!tangentV.TryNormalize(out tangentV)) throw new InvalidOperationException("support-local transverse direction is degenerate");
            var frame = new BoundaryLocalFrame(origin, materialNormal, tangentU, tangentV);
            var projected = Corners(cell).Select(p => p - origin).ToArray();
            var domain = new BoundaryMapDomain(projected.Min(p => p.Dot(tangentU)), projected.Max(p => p.Dot(tangentU)),
                projected.Min(p => p.Dot(tangentV)), projected.Max(p => p.Dot(tangentV)));
            domain=AdmissibleGraphDomain(support,origin,shell.Transform,domain);
            var (nu, nv) = Resolution(support, domain, cell);
            var certificate = new EngineeringBoundaryMapCertificate(BoundaryMapCertificateDecision.Acceptable,
                PositionErrorBound(support, domain, nu, nv), NormalErrorDegrees(support, domain, nu, nv), nu * nv,
                "support-family curvature bound with anisotropic generator/transverse resolution");
            var frameKey = $"{face.Reference.SourceId}:{origin.X:R}:{origin.Y:R}:{origin.Z:R}";
            ExactBoundaryEvaluation Exact(double u, double v)
            {
                var basePoint = origin + (tangentU * u) + (tangentV * v);
                var offset = SolveOffset(support, basePoint, materialNormal, shell.Transform);
                var boundary = basePoint + (materialNormal * offset);
                var normal = ExactSupportBoundaryQuery.ExactSupportNormal(shell.Body, face.FaceId, boundary, shell.Transform);
                if (normal.Dot(materialNormal) < 0d) normal = -normal;
                return new(offset, normal);
            }
            // Reject a non-graph chart before allocating/filling its sample grid. Coarse whole-part
            // cells commonly span a torus/cylinder tangent horizon; probing the four domain corners
            // makes that invalidity cheap and deterministic instead of discovering it late in Build.
            _=Exact(domain.MinimumU,domain.MinimumV);_=Exact(domain.MaximumU,domain.MinimumV);
            _=Exact(domain.MinimumU,domain.MaximumV);_=Exact(domain.MaximumU,domain.MaximumV);
            var scale = double.Max(1d, (shell.Bounds.Max - shell.Bounds.Min).Length);
            map = RuntimeBoundaryMapBuild.Build(index, face.Reference, frame, domain, nu, nv,
                new BoundaryOffsetMapErrorPolicy(scale * 2e-5d, .5d, 24), Exact,
                (u, v) => new BoundaryEvaluationKey(frameKey, nu, nv, BitConverter.DoubleToInt64Bits(u), BitConverter.DoubleToInt64Bits(v)),
                cache, certificate, trimSignedDistance: point =>
                {
                    var exactPoint=ExactSupportBoundaryQuery.ProjectToSupport(shell.Body,face.FaceId,point,shell.Transform);
                    // Curved global trims need not remain convex in one tangent chart. The exact sampled
                    // face bounds are therefore the conservative ownership gate, with CIR confirming that
                    // the projected point belongs to the complete material boundary.
                    var trimDistance=AabbSignedDistance(face.Bounds,exactPoint);
                    return region.Classify(exactPoint, scale * 2e-7d) == ContinuumPointClassification.Boundary
                        ? double.Max(1e-12d,trimDistance) : -double.Max(1e-12d,double.Abs(trimDistance));
                });
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        { rejection = ex.Message; map = null; return false; }

    }

    private static Vector3D GeneratorDirection(SurfaceGeometry support, Point3D worldOrigin, Transform3D transform, Vector3D normal)
    {
        if (support.Cylinder is CylinderSurface cylinder) return transform.Apply(cylinder.Axis).ToVector();
        if (support.Cone is ConeSurface cone)
        {
            var local = transform.Inverse().Apply(worldOrigin); var delta = local - cone.Apex;
            var axial = delta.Dot(cone.Axis.ToVector()); var radial = delta - (cone.Axis.ToVector() * axial);
            if (!radial.TryNormalize(out radial)) radial = cone.ReferenceAxis.ToVector();
            return transform.Apply(Direction3D.Create(cone.Axis.ToVector() + (radial * double.Tan(cone.SemiAngleRadians)))).ToVector();
        }
        if (support.Torus is TorusSurface torus)
        {
            var local = transform.Inverse().Apply(worldOrigin); var delta = local - torus.Center;
            var axial = delta.Dot(torus.Axis.ToVector()); var radial = delta - (torus.Axis.ToVector() * axial);
            if (!radial.TryNormalize(out radial)) radial = torus.XAxis.ToVector();
            var azimuth = torus.Axis.ToVector().Cross(radial);
            return transform.Apply(Direction3D.Create(azimuth)).ToVector();
        }
        var seed = double.Abs(normal.X) < .8d ? new Vector3D(1, 0, 0) : new Vector3D(0, 1, 0);
        return seed - (normal * seed.Dot(normal));
    }

    private static (int U, int V) Resolution(SurfaceGeometry support, BoundaryMapDomain domain, BoundingBox3D cell)
    {
        var extentU = domain.MaximumU - domain.MinimumU; var extentV = domain.MaximumV - domain.MinimumV;
        var scale = double.Max(extentU, extentV); var radius = support.Cylinder?.Radius
            ?? (support.Cone is { } cone ? double.Max(scale, cone.PlacementRadius) : support.Torus!.Value.MinorRadius);
        var curved = int.Clamp(4 + (int)double.Ceiling(4d * scale / double.Max(radius, scale * .2d)), 6, 16);
        return support.Kind switch
        {
            SurfaceGeometryKind.Cylinder => (3, curved),
            SurfaceGeometryKind.Cone => (4, curved),
            SurfaceGeometryKind.Torus => (int.Clamp(curved / 2, 4, 12), curved),
            _ => (6, 6),
        };
    }

    private static BoundaryMapDomain AdmissibleGraphDomain(SurfaceGeometry support,Point3D worldOrigin,Transform3D transform,BoundaryMapDomain d)
    {
        static (double,double) Limit(double lo,double hi,double radius){var h=.72d*radius;return(double.Max(lo,-h),double.Min(hi,h));}
        if(support.Cylinder is { } cylinder){var v=Limit(d.MinimumV,d.MaximumV,cylinder.Radius);return new(d.MinimumU,d.MaximumU,v.Item1,v.Item2);}
        if(support.Cone is { } cone){var local=transform.Inverse().Apply(worldOrigin);var radius=double.Max(1e-6d,cone.AxialParameterFromPoint(local)*double.Tan(cone.SemiAngleRadians));var v=Limit(d.MinimumV,d.MaximumV,radius);return new(d.MinimumU,d.MaximumU,v.Item1,v.Item2);}
        var torus=support.Torus!.Value;var u=Limit(d.MinimumU,d.MaximumU,torus.MajorRadius+torus.MinorRadius);var v2=Limit(d.MinimumV,d.MaximumV,torus.MinorRadius);return new(u.Item1,u.Item2,v2.Item1,v2.Item2);
    }

    private static double SolveOffset(SurfaceGeometry support, Point3D worldBase, Vector3D worldNormal, Transform3D transform)
    {
        var inverse = transform.Inverse(); var localBase = inverse.Apply(worldBase);
        var localDirection = inverse.Apply(worldNormal); if (!localDirection.TryNormalize(out localDirection)) throw new InvalidOperationException("invalid local graph direction");
        var characteristic=support.Cylinder?.Radius??support.Torus?.MinorRadius??double.Max(1d,support.Cone?.PlacementRadius??1d);
        var w = 0d;
        for (var i = 0; i < 32; i++)
        {
            var p = localBase + (localDirection * w); var (f, gradient) = Implicit(support, p);
            if (double.Abs(f) <= 1e-10d*double.Max(1d,characteristic*characteristic)) return w;
            var derivative = gradient.Dot(localDirection);
            if (double.Abs(derivative) <= 1e-10d) throw new InvalidOperationException("height-map tangent horizon");
            var step=double.Clamp(f/derivative,-characteristic,characteristic);var next=w-step;
            for(var line=0;line<8;line++)
            {var trial=Implicit(support,localBase+(localDirection*next)).F;if(double.Abs(trial)<double.Abs(f))break;step*=.5d;next=w-step;}
            w=next;
            if (!double.IsFinite(w)) throw new InvalidOperationException("height-map root did not remain finite");
        }
        throw new InvalidOperationException("height-map root did not converge");
    }

    private static (double F, Vector3D Gradient) Implicit(SurfaceGeometry support, Point3D p)
    {
        if (support.Cylinder is CylinderSurface cylinder)
        { var d=p-cylinder.Origin;var axial=d.Dot(cylinder.Axis.ToVector());var radial=d-(cylinder.Axis.ToVector()*axial);return(radial.LengthSquared-cylinder.Radius*cylinder.Radius,radial*2d); }
        if (support.Cone is ConeSurface cone)
        { var d=p-cone.Apex;var axial=d.Dot(cone.Axis.ToVector());var radial=d-(cone.Axis.ToVector()*axial);var t=double.Tan(cone.SemiAngleRadians);return(radial.LengthSquared-axial*axial*t*t,(radial*2d)-(cone.Axis.ToVector()*(2d*axial*t*t))); }
        var torus=support.Torus!.Value;var q=p-torus.Center;var z=q.Dot(torus.Axis.ToVector());var rv=q-(torus.Axis.ToVector()*z);var rho=rv.Length;
        if(rho<=1e-12d)throw new InvalidOperationException("torus graph reached axis");var ro=rho-torus.MajorRadius;
        return(ro*ro+z*z-torus.MinorRadius*torus.MinorRadius,(rv*(2d*ro/rho))+(torus.Axis.ToVector()*(2d*z)));
    }

    private static double PositionErrorBound(SurfaceGeometry support, BoundaryMapDomain domain, int nu, int nv)
    { var h=double.Max((domain.MaximumU-domain.MinimumU)/(nu-1),(domain.MaximumV-domain.MinimumV)/(nv-1));var r=support.Cylinder?.Radius??support.Torus?.MinorRadius??double.Max(h,support.Cone?.PlacementRadius??h);return h*h/(4d*double.Max(r,1e-9d)); }
    private static double NormalErrorDegrees(SurfaceGeometry support, BoundaryMapDomain domain, int nu, int nv)
    { var h=double.Max((domain.MaximumU-domain.MinimumU)/(nu-1),(domain.MaximumV-domain.MinimumV)/(nv-1));var r=support.Cylinder?.Radius??support.Torus?.MinorRadius??double.Max(h,support.Cone?.PlacementRadius??h);return h/double.Max(r,1e-9d)*180d/double.Pi; }
    private static Point3D Center(BoundingBox3D b)=>new((b.Min.X+b.Max.X)*.5d,(b.Min.Y+b.Max.Y)*.5d,(b.Min.Z+b.Max.Z)*.5d);
    private static double AabbSignedDistance(BoundingBox3D b,Point3D p)=>double.Min(double.Min(double.Min(p.X-b.Min.X,b.Max.X-p.X),double.Min(p.Y-b.Min.Y,b.Max.Y-p.Y)),double.Min(p.Z-b.Min.Z,b.Max.Z-p.Z));
    private static Point3D[] Corners(BoundingBox3D b)=>[new(b.Min.X,b.Min.Y,b.Min.Z),new(b.Max.X,b.Min.Y,b.Min.Z),new(b.Min.X,b.Max.Y,b.Min.Z),new(b.Max.X,b.Max.Y,b.Min.Z),new(b.Min.X,b.Min.Y,b.Max.Z),new(b.Max.X,b.Min.Y,b.Max.Z),new(b.Min.X,b.Max.Y,b.Max.Z),new(b.Max.X,b.Max.Y,b.Max.Z)];

    internal sealed class ProjectedTrimDomain
    {
        private readonly Point2[] _hull;
        private ProjectedTrimDomain(Point2[] hull)=>_hull=hull;
        public static ProjectedTrimDomain Create(IEnumerable<Point3D> samples,BoundaryLocalFrame frame)
        {var p=samples.Select(x=>x-frame.Origin).Select(x=>new Point2(x.Dot(frame.TangentU),x.Dot(frame.TangentV))).Distinct().OrderBy(x=>x.U).ThenBy(x=>x.V).ToArray();return new(Hull(p));}
        public double SignedDistance(double u,double v)
        {if(_hull.Length<3)return double.PositiveInfinity;var point=new Point2(u,v);var minimum=double.PositiveInfinity;for(var i=0;i<_hull.Length;i++){var a=_hull[i];var b=_hull[(i+1)%_hull.Length];var edge=new Point2(b.U-a.U,b.V-a.V);var cross=edge.U*(point.V-a.V)-edge.V*(point.U-a.U);minimum=double.Min(minimum,cross/double.Sqrt(edge.U*edge.U+edge.V*edge.V));}return minimum;}
        private static Point2[] Hull(Point2[] p){if(p.Length<=2)return p;var lo=new List<Point2>();foreach(var x in p){while(lo.Count>=2&&Cross(lo[^2],lo[^1],x)<=1e-14)lo.RemoveAt(lo.Count-1);lo.Add(x);}var hi=new List<Point2>();for(var i=p.Length-1;i>=0;i--){var x=p[i];while(hi.Count>=2&&Cross(hi[^2],hi[^1],x)<=1e-14)hi.RemoveAt(hi.Count-1);hi.Add(x);}lo.RemoveAt(lo.Count-1);hi.RemoveAt(hi.Count-1);lo.AddRange(hi);return lo.ToArray();}
        private static double Cross(Point2 a,Point2 b,Point2 c)=>(b.U-a.U)*(c.V-a.V)-(b.V-a.V)*(c.U-a.U);
    }
}
