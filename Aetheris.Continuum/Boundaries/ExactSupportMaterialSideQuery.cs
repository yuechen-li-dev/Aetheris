using Aetheris.Continuum.Cir;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Continuum.Boundaries;

/// <summary>Exact analytic support differential. It deliberately ignores SameSense and material occupancy.</summary>
public static class ExactSupportBoundaryQuery
{
    public static Point3D ProjectToSupport(BrepBody body,FaceId faceId,Point3D worldPoint,Transform3D transform)
    {
        var inverse=transform.Inverse();var p=inverse.Apply(worldPoint);var support=body.GetFaceSurface(faceId);
        var projected=support.Kind switch
        {
            SurfaceGeometryKind.Plane=>ProjectPlane(support.Plane!.Value,p),
            SurfaceGeometryKind.Cylinder=>ProjectCylinder(support.Cylinder!.Value,p),
            SurfaceGeometryKind.Cone=>ProjectCone(support.Cone!.Value,p),
            SurfaceGeometryKind.Sphere=>ProjectSphere(support.Sphere!.Value,p),
            SurfaceGeometryKind.Torus=>ProjectTorus(support.Torus!.Value,p),
            _=>throw new NotSupportedException($"Exact support projection is not implemented for {support.Kind}.")
        };return transform.Apply(projected);
    }

    public static Vector3D ExactSupportNormal(BrepBody body,FaceId faceId,Point3D worldPoint,Transform3D transform)
    {
        var local=transform.Inverse().Apply(worldPoint);var support=body.GetFaceSurface(faceId);
        var normal=support.Kind switch
        {
            SurfaceGeometryKind.Plane=>support.Plane!.Value.Normal.ToVector(),
            SurfaceGeometryKind.Cylinder=>CylinderNormal(support.Cylinder!.Value,local),
            SurfaceGeometryKind.Cone=>ConeNormal(support.Cone!.Value,local),
            SurfaceGeometryKind.Sphere=>local-support.Sphere!.Value.Center,
            SurfaceGeometryKind.Torus=>TorusNormal(support.Torus!.Value,local),
            _=>throw new NotSupportedException($"Exact support normal is not implemented for {support.Kind}.")
        };
        normal=transform.Apply(normal);if(!normal.TryNormalize(out normal))throw new InvalidOperationException("Support transform collapsed the normal.");return normal;
    }

    private static Vector3D CylinderNormal(Aetheris.Kernel.Core.Geometry.Surfaces.CylinderSurface c,Point3D p)
    {var d=p-c.Origin;return d-(c.Axis.ToVector()*d.Dot(c.Axis.ToVector()));}
    private static Vector3D ConeNormal(Aetheris.Kernel.Core.Geometry.Surfaces.ConeSurface c,Point3D p)
    {var d=p-c.Apex;var axial=d.Dot(c.Axis.ToVector());var radial=d-(c.Axis.ToVector()*axial);radial.TryNormalize(out radial);return radial-(c.Axis.ToVector()*double.Tan(c.SemiAngleRadians));}
    private static Vector3D TorusNormal(Aetheris.Kernel.Core.Geometry.Surfaces.TorusSurface t,Point3D p)
    {var d=p-t.Center;var axial=d.Dot(t.Axis.ToVector());var radial=d-(t.Axis.ToVector()*axial);radial.TryNormalize(out radial);var center=t.Center+(radial*t.MajorRadius);return p-center;}
    private static Point3D ProjectPlane(Aetheris.Kernel.Core.Geometry.Surfaces.PlaneSurface s,Point3D p)=>p-(s.Normal.ToVector()*(p-s.Origin).Dot(s.Normal.ToVector()));
    private static Point3D ProjectCylinder(Aetheris.Kernel.Core.Geometry.Surfaces.CylinderSurface s,Point3D p){var d=p-s.Origin;var axial=d.Dot(s.Axis.ToVector());var radial=d-(s.Axis.ToVector()*axial);if(!radial.TryNormalize(out radial))radial=s.XAxis.ToVector();return s.Origin+(s.Axis.ToVector()*axial)+(radial*s.Radius);}
    private static Point3D ProjectCone(Aetheris.Kernel.Core.Geometry.Surfaces.ConeSurface s,Point3D p){var d=p-s.Apex;var axial=double.Max(0d,d.Dot(s.Axis.ToVector()));var radial=d-(s.Axis.ToVector()*axial);if(!radial.TryNormalize(out radial))radial=s.ReferenceAxis.ToVector();return s.Apex+(s.Axis.ToVector()*axial)+(radial*(axial*double.Tan(s.SemiAngleRadians)));}
    private static Point3D ProjectSphere(Aetheris.Kernel.Core.Geometry.Surfaces.SphereSurface s,Point3D p){var radial=p-s.Center;if(!radial.TryNormalize(out radial))radial=s.XAxis.ToVector();return s.Center+(radial*s.Radius);}
    private static Point3D ProjectTorus(Aetheris.Kernel.Core.Geometry.Surfaces.TorusSurface s,Point3D p){var d=p-s.Center;var axial=d.Dot(s.Axis.ToVector());var radial=d-(s.Axis.ToVector()*axial);if(!radial.TryNormalize(out radial))radial=s.XAxis.ToVector();var tubeCenter=s.Center+(radial*s.MajorRadius);var tube=p-tubeCenter;if(!tube.TryNormalize(out tube))tube=s.Axis.ToVector();return tubeCenter+(tube*s.MinorRadius);}
}

public static class MaterialSideClassifier
{
    public static MaterialSideEvidence ClassifyMaterialSide(FaceId faceId,Point3D boundaryPoint,Vector3D exactSupportNormal,
        IContinuumRegion continuumRegion,double localScale,bool sameSense)
    {
        var epsilon=double.Clamp(localScale*1e-6d,1e-8d,localScale*1e-3d);
        var plus=continuumRegion.Classify(boundaryPoint+(exactSupportNormal*epsilon),epsilon*.1d);
        var minus=continuumRegion.Classify(boundaryPoint-(exactSupportNormal*epsilon),epsilon*.1d);
        var plusInside=plus==ContinuumPointClassification.Inside;var minusInside=minus==ContinuumPointClassification.Inside;
        var status=plusInside^minusInside?MaterialSideStatus.Resolved:MaterialSideStatus.Inconsistent;
        Vector3D? material=plusInside^minusInside?(plusInside?exactSupportNormal:-exactSupportNormal):null;
        double? plusField=continuumRegion is IImplicitFieldCapability field?field.FieldValue(boundaryPoint+(exactSupportNormal*epsilon)):null;
        double? minusField=continuumRegion is IImplicitFieldCapability field2?field2.FieldValue(boundaryPoint-(exactSupportNormal*epsilon)):null;
        return new(faceId,boundaryPoint,exactSupportNormal,epsilon,plus,minus,plusField,minusField,material,status,
            $"Occupied side selected exclusively from CIR probes; SameSense={sameSense} is recorded only as topology/parameterization evidence.");
    }
}
