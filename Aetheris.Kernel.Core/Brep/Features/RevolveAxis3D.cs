using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Brep.Features;

/// <summary>
/// Explicit world-space axis definition for revolve features.
/// Direction is validated by the consuming operation.
/// </summary>
public readonly record struct RevolveAxis3D(Point3D Origin, Vector3D Direction);
