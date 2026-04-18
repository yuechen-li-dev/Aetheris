using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Brep.Boolean;

public readonly record struct BrepRecognitionProfile(
    int FaceCount,
    bool IsSingleSurface,
    SurfaceGeometryKind? PrimarySurfaceKind,
    int PlaneFaceCount,
    int CylinderFaceCount,
    int ConeFaceCount)
{
    public static BrepRecognitionProfile Scan(BrepBody body, ToleranceContext _)
    {
        var faceCount = 0;
        var planeFaceCount = 0;
        var cylinderFaceCount = 0;
        var coneFaceCount = 0;
        var isSingleSurface = true;
        SurfaceGeometryKind? primarySurfaceKind = null;

        foreach (var binding in body.Bindings.FaceBindings)
        {
            faceCount++;
            var kind = body.Geometry.GetSurface(binding.SurfaceGeometryId).Kind;
            if (primarySurfaceKind is null)
            {
                primarySurfaceKind = kind;
            }
            else if (primarySurfaceKind != kind)
            {
                isSingleSurface = false;
            }

            switch (kind)
            {
                case SurfaceGeometryKind.Plane:
                    planeFaceCount++;
                    break;
                case SurfaceGeometryKind.Cylinder:
                    cylinderFaceCount++;
                    break;
                case SurfaceGeometryKind.Cone:
                    coneFaceCount++;
                    break;
            }
        }

        return new BrepRecognitionProfile(
            faceCount,
            faceCount > 0 && isSingleSurface,
            primarySurfaceKind,
            planeFaceCount,
            cylinderFaceCount,
            coneFaceCount);
    }
}
