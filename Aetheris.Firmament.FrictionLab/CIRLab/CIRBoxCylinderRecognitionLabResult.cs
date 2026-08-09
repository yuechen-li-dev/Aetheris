using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.FrictionLab;

public sealed record CirBoxCylinderRecognitionLabResult(
    bool Success,
    CirLabRecognitionReason Reason,
    string Diagnostic,
    SdfBoxNode? Box,
    Vector3D BoxTranslation,
    SdfCylinderNode? Cylinder,
    Vector3D CylinderTranslation,
    string? Axis,
    double ThroughLength,
    SdfNode? NormalizedRoot);
