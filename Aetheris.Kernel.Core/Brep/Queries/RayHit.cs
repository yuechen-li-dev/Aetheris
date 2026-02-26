using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Queries;

public readonly record struct RayHit(
    double T,
    Point3D Point,
    Direction3D? Normal,
    FaceId? FaceId = null,
    EdgeId? EdgeId = null);
