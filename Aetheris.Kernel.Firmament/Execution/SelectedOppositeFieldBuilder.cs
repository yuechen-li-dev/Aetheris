using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Firmament.Execution;

internal enum SelectedOppositeFieldBuildStatus
{
    Success,
    Deferred,
    Unsupported
}

internal sealed record SelectedOppositeFieldBuildResult(
    SelectedOppositeFieldBuildStatus Status,
    CirNode? Node,
    string Diagnostic);

internal static class SelectedOppositeFieldBuilder
{
    internal static SelectedOppositeFieldBuildResult TryBuild(SourceSurfaceDescriptor selectedOpposite)
    {
        if (selectedOpposite.Family == SurfacePatchFamily.Cylindrical)
        {
            if (selectedOpposite.CylindricalGeometryEvidence is not { } cylinder)
            {
                return new(SelectedOppositeFieldBuildStatus.Deferred, null, "selected-opposite-field: cylinder-geometry-missing");
            }

            if (cylinder.Radius <= 1e-12d || cylinder.Height <= 1e-12d || cylinder.AxisDirection.Length <= 1e-12d)
            {
                return new(SelectedOppositeFieldBuildStatus.Deferred, null, "selected-opposite-field: cylinder-geometry-invalid");
            }

            var axis = cylinder.AxisDirection / cylinder.AxisDirection.Length;
            var center = new Point3D((cylinder.BottomCenter.X + cylinder.TopCenter.X) * 0.5d, (cylinder.BottomCenter.Y + cylinder.TopCenter.Y) * 0.5d, (cylinder.BottomCenter.Z + cylinder.TopCenter.Z) * 0.5d);
            if (!TryCreateCylinderWorldTransform(center, axis, out var transform))
            {
                return new(SelectedOppositeFieldBuildStatus.Deferred, null, "selected-opposite-field: cylinder-orientation-unsupported");
            }

            var node = new CirTransformNode(new CirCylinderNode(cylinder.Radius, cylinder.Height), transform);
            return new(SelectedOppositeFieldBuildStatus.Success, node, "selected-opposite-field: cylinder");
        }

        if (selectedOpposite.Family == SurfacePatchFamily.Spherical)
        {
            return new(SelectedOppositeFieldBuildStatus.Deferred, null, "selected-opposite-field: sphere-geometry-missing");
        }

        if (selectedOpposite.Family == SurfacePatchFamily.Toroidal)
        {
            return new(SelectedOppositeFieldBuildStatus.Deferred, null, "selected-opposite-field: torus-geometry-missing");
        }

        return new(SelectedOppositeFieldBuildStatus.Unsupported, null, $"selected-opposite-field: unsupported-family:{selectedOpposite.Family}");
    }

    private static bool TryCreateCylinderWorldTransform(Point3D center, Vector3D axisDirection, out Transform3D transform)
    {
        transform = Transform3D.Identity;
        var localAxis = new Vector3D(0d, 0d, 1d);
        var dot = localAxis.Dot(axisDirection);
        if (dot > 1d - 1e-9d)
        {
            transform = Transform3D.CreateTranslation(center - Point3D.Origin);
            return true;
        }

        if (dot < -1d + 1e-9d)
        {
            transform = Transform3D.Compose(Transform3D.CreateRotationX(Math.PI), Transform3D.CreateTranslation(center - Point3D.Origin));
            return true;
        }

        return false;
    }
}
