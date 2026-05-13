using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.FrictionLab;

public sealed record CirBoxCylinderRecognitionLabResult(
    bool Success,
    CirLabRecognitionReason Reason,
    string Diagnostic,
    CirBoxNode? Box,
    Vector3D BoxTranslation,
    CirCylinderNode? Cylinder,
    Vector3D CylinderTranslation,
    string? Axis,
    double ThroughLength,
    CirNode? NormalizedRoot);
