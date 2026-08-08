using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.StandardLibrary;

/// <summary>
/// Engineering parameters for a threadless material representation of a hex-head bolt.
/// Length values are millimetres and angles are degrees. Thread fields are semantic only.
/// </summary>
public sealed record HexBoltSpec(
    double NominalDiameter,
    double Length,
    double HeadAcrossFlats,
    double HeadHeight,
    double TopFlatDiameter,
    double TopChamferAngle,
    double TipChamferLength,
    double TipDiameter,
    double ThreadLength,
    string ThreadDesignation,
    string PropertyClass,
    double UnderHeadRadius = 0d);

public static class McMasterHexBoltSpecs
{
    public const string ReferencePartNumber = "91180A151";

    /// <summary>Audited from McMaster-Carr 91180A151_NO THREADS.STEP.</summary>
    public static HexBoltSpec Reference91180A151 { get; } = new(
        NominalDiameter: 8d,
        Length: 35d,
        HeadAcrossFlats: 13d,
        HeadHeight: 5.3d,
        TopFlatDiameter: 12.35d,
        TopChamferAngle: 25d,
        TipChamferLength: 0.9375d,
        TipDiameter: 6.125d,
        ThreadLength: 22d,
        ThreadDesignation: "M8 x 1.25",
        PropertyClass: "8.8",
        UnderHeadRadius: 0.2d);
}

public enum HexBoltAdmissionCode
{
    NonFiniteOrNonPositiveDimension,
    ThreadLengthOutsideShank,
    TopFlatOutsideHex,
    TopChamferConsumesHead,
    TipChamferInvalid,
    UnderHeadRadiusInvalid,
    EmptySemanticMetadata
}

public sealed record HexBoltAdmissionDiagnostic(HexBoltAdmissionCode Code, string Field, string Message);

public sealed record HexBoltDerivedDimensions(
    double HeadApothem,
    double HeadCircumradius,
    double TopFlatRadius,
    double TopConeSemiAngleDegrees,
    double TopConeApexX,
    double TopConeSideMidpointX,
    double TopConeCornerX,
    double TipChamferStartX);

public enum ExactConstructionSemanticKind { Part, Region, Face }

public sealed record ExactConstructionSemanticDescendant(
    string StableId,
    ExactConstructionSemanticKind Kind,
    FaceId? Face = null,
    string? ParentStableId = null,
    string? Metadata = null);

public sealed record ExactConstructionSemanticModel(
    string BodyStableId,
    IReadOnlyList<ExactConstructionSemanticDescendant> Descendants,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record HexBoltDefinition(
    HexBoltSpec Spec,
    HexBoltDerivedDimensions Dimensions,
    BrepBody Body,
    ExactConstructionSemanticModel Semantics,
    string DeterministicSignature);
