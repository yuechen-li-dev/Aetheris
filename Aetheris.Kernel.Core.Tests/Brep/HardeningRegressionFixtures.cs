using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Brep;

internal static class HardeningRegressionFixtures
{
    internal static BrepBody OverlapLeftBox() => BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(0d, 4d, 0d, 4d, 0d, 4d)).Value;

    internal static BrepBody OverlapRightBox() => BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(2d, 6d, 1d, 3d, -1d, 2d)).Value;

    internal static BrepBody TouchingOnlyBox() => BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(4d, 6d, 0d, 4d, 0d, 4d)).Value;

    internal static BrepBody ContainedBox() => BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(1d, 2d, 1d, 2d, 1d, 2d)).Value;

    internal static PolylineProfile2D CanonicalExtrudeProfile() => PolylineProfile2D.Rectangle(2d, 2d);

    internal static IReadOnlyList<ProfilePoint2D> CanonicalRevolveSupportedProfile =>
    [
        new ProfilePoint2D(2d, 0d),
        new ProfilePoint2D(2d, 5d),
    ];

    internal static IReadOnlyList<ProfilePoint2D> CanonicalRevolveUnsupportedProfile =>
    [
        new ProfilePoint2D(1d, 0d),
        new ProfilePoint2D(1d, 2d),
    ];

    internal static BrepBody LShapeUnionLeftBox() => BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(0d, 2d, 0d, 4d, 0d, 2d)).Value;

    internal static BrepBody LShapeUnionRightBox() => BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(2d, 4d, 0d, 2d, 0d, 2d)).Value;

    internal static ExtrudeFrame3D CanonicalFrame() =>
        new(
            Point3D.Origin,
            Direction3D.Create(new Vector3D(0d, 0d, 1d)),
            Direction3D.Create(new Vector3D(1d, 0d, 0d)));

    internal static Ray3D TopDownCenterRay() => new(new Point3D(0d, 0d, 5d), Direction3D.Create(new Vector3D(0d, 0d, -1d)));

    internal static Ray3D TopDownEdgeTieRay() => new(new Point3D(1d, 1d, 3d), Direction3D.Create(new Vector3D(0d, 0d, -1d)));
}
