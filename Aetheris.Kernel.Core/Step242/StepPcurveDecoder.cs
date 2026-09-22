using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Step242;

internal sealed record ImportedStepPcurve(
    PcurveGeometry Geometry,
    bool SameSense,
    int PcurveEntityId,
    int CurveEntityId,
    string CurveType,
    int SurfaceEntityId);

internal static class StepPcurveDecoder
{
    internal static KernelResult<IReadOnlyList<ImportedStepPcurve>> DecodeForSurface(
        Step242ParsedDocument document,
        Step242ParsedEntity edgeGeometryEntity,
        int surfaceEntityId,
        ParameterInterval edgeDomain)
    {
        var surfaceCurve = Step242SubsetDecoder.TryGetConstructor(edgeGeometryEntity.Instance, "SURFACE_CURVE")
            ?? Step242SubsetDecoder.TryGetConstructor(edgeGeometryEntity.Instance, "SEAM_CURVE");
        if (surfaceCurve is null)
            return KernelResult<IReadOnlyList<ImportedStepPcurve>>.Success([]);
        if (surfaceCurve.Arguments.ElementAtOrDefault(2) is not Step242ListValue associated)
            return Failure<IReadOnlyList<ImportedStepPcurve>>("SURFACE_CURVE/SEAM_CURVE associated_geometry must be a list.", "Importer.Pcurve.AssociatedGeometry");

        var result = new List<ImportedStepPcurve>();
        foreach (var item in associated.Items)
        {
            if (item is not Step242EntityReference pcurveReference)
                return Failure<IReadOnlyList<ImportedStepPcurve>>("Associated pcurve must be an entity reference.", "Importer.Pcurve.AssociatedGeometry");
            var pcurveResult = document.TryGetEntity(pcurveReference.TargetId, "PCURVE");
            if (!pcurveResult.IsSuccess) return KernelResult<IReadOnlyList<ImportedStepPcurve>>.Failure(pcurveResult.Diagnostics);
            var pcurve = pcurveResult.Value;
            if (pcurve.Arguments.ElementAtOrDefault(1) is not Step242EntityReference supportReference
                || pcurve.Arguments.ElementAtOrDefault(2) is not Step242EntityReference representationReference)
                return Failure<IReadOnlyList<ImportedStepPcurve>>("PCURVE must reference its support and definitional representation.", "Importer.Pcurve.Binding");
            if (supportReference.TargetId != surfaceEntityId) continue;

            var representationResult = document.TryGetEntity(representationReference.TargetId, "DEFINITIONAL_REPRESENTATION");
            if (!representationResult.IsSuccess) return KernelResult<IReadOnlyList<ImportedStepPcurve>>.Failure(representationResult.Diagnostics);
            if (representationResult.Value.Arguments.ElementAtOrDefault(1) is not Step242ListValue items || items.Items.Count != 1
                || items.Items[0] is not Step242EntityReference curveReference)
                return Failure<IReadOnlyList<ImportedStepPcurve>>("PCURVE definitional representation must contain exactly one curve.", "Importer.Pcurve.DefinitionalRepresentation");
            var curveResult = document.TryGetEntity(curveReference.TargetId);
            if (!curveResult.IsSuccess) return KernelResult<IReadOnlyList<ImportedStepPcurve>>.Failure(curveResult.Diagnostics);
            var geometryResult = DecodeGeometry(document, curveResult.Value, edgeDomain);
            if (!geometryResult.IsSuccess) return KernelResult<IReadOnlyList<ImportedStepPcurve>>.Failure(geometryResult.Diagnostics);
            result.Add(new ImportedStepPcurve(geometryResult.Value.Geometry, geometryResult.Value.SameSense,
                pcurve.Id, curveResult.Value.Id, DescribeCurveType(curveResult.Value), supportReference.TargetId));
        }
        return KernelResult<IReadOnlyList<ImportedStepPcurve>>.Success(result);
    }

    private static KernelResult<DecodedPcurveGeometry> DecodeGeometry(Step242ParsedDocument document, Step242ParsedEntity source, ParameterInterval fallbackDomain)
    {
        var domain = fallbackDomain;
        var sameSense = true;
        var trimmed = Step242SubsetDecoder.TryGetConstructor(source.Instance, "TRIMMED_CURVE");
        if (trimmed is not null)
        {
            if (trimmed.Arguments.ElementAtOrDefault(1) is not Step242EntityReference basis
                || !TryReadTrim(trimmed.Arguments.ElementAtOrDefault(2), out var start)
                || !TryReadTrim(trimmed.Arguments.ElementAtOrDefault(3), out var end))
                return Failure<DecodedPcurveGeometry>("Pcurve TRIMMED_CURVE requires parameter trims and a basis curve.", "Importer.Pcurve.TrimmedCurve");
            if (trimmed.Arguments.ElementAtOrDefault(4) is not Step242BooleanValue senseAgreement)
                return Failure<DecodedPcurveGeometry>("Pcurve TRIMMED_CURVE requires an explicit sense_agreement value.", "Importer.Pcurve.TrimmedCurve");
            var basisResult = document.TryGetEntity(basis.TargetId);
            if (!basisResult.IsSuccess) return KernelResult<DecodedPcurveGeometry>.Failure(basisResult.Diagnostics);
            source = basisResult.Value;
            domain = new ParameterInterval(double.Min(start, end), double.Max(start, end));
            sameSense = senseAgreement.Value == (end >= start);
        }

        if (Step242SubsetDecoder.TryGetConstructor(source.Instance, "LINE") is { } line)
        {
            if (line.Arguments.ElementAtOrDefault(1) is not Step242EntityReference originReference
                || line.Arguments.ElementAtOrDefault(2) is not Step242EntityReference vectorReference)
                return Failure<DecodedPcurveGeometry>("Pcurve LINE requires point and vector references.", "Importer.Pcurve.Line");
            var origin = ReadUvPoint(document, originReference.TargetId);
            var vector = ReadUvVector(document, vectorReference.TargetId);
            if (!origin.IsSuccess) return KernelResult<DecodedPcurveGeometry>.Failure(origin.Diagnostics);
            if (!vector.IsSuccess) return KernelResult<DecodedPcurveGeometry>.Failure(vector.Diagnostics);
            var start = new SurfaceParameterPoint(origin.Value.U + vector.Value.U * domain.Start, origin.Value.V + vector.Value.V * domain.Start);
            var end = new SurfaceParameterPoint(origin.Value.U + vector.Value.U * domain.End, origin.Value.V + vector.Value.V * domain.End);
            return Success(PcurveGeometry.Line(domain, start, end));
        }

        if (Step242SubsetDecoder.TryGetConstructor(source.Instance, "POLYLINE") is { } polyline)
        {
            if (polyline.Arguments.ElementAtOrDefault(1) is not Step242ListValue pointReferences || pointReferences.Items.Count < 2)
                return Failure<DecodedPcurveGeometry>("Pcurve POLYLINE requires at least two points.", "Importer.Pcurve.Polyline");
            var points = new List<SurfaceParameterPoint>(pointReferences.Items.Count);
            foreach (var value in pointReferences.Items)
            {
                if (value is not Step242EntityReference pointReference)
                    return Failure<DecodedPcurveGeometry>("Pcurve POLYLINE point must be a reference.", "Importer.Pcurve.Polyline");
                var point = ReadUvPoint(document, pointReference.TargetId);
                if (!point.IsSuccess) return KernelResult<DecodedPcurveGeometry>.Failure(point.Diagnostics);
                points.Add(point.Value);
            }
            return Success(PcurveGeometry.Polyline(domain, points));
        }

        if (Step242SubsetDecoder.TryGetConstructor(source.Instance, "CIRCLE") is { } circle)
        {
            var conic = DecodeConic(document, circle, domain, circle: true);
            return conic.IsSuccess ? Success(conic.Value) : KernelResult<DecodedPcurveGeometry>.Failure(conic.Diagnostics);
        }
        if (Step242SubsetDecoder.TryGetConstructor(source.Instance, "ELLIPSE") is { } ellipse)
        {
            var conic = DecodeConic(document, ellipse, domain, circle: false);
            return conic.IsSuccess ? Success(conic.Value) : KernelResult<DecodedPcurveGeometry>.Failure(conic.Diagnostics);
        }

        var splineEntity = Step242Importer.ResolveBSplineCurveEntity(source);
        if (splineEntity is not null)
        {
            var spline = Step242SubsetDecoder.ReadBSplineCurveWithKnots(document, splineEntity, allowTwoDimensionalControlPoints: true);
            if (!spline.IsSuccess) return KernelResult<DecodedPcurveGeometry>.Failure(spline.Diagnostics);
            var splineDomain = new ParameterInterval(
                System.Math.Max(domain.Start, spline.Value.DomainStart),
                System.Math.Min(domain.End, spline.Value.DomainEnd));
            if (fallbackDomain.Equals(domain)) splineDomain = new ParameterInterval(spline.Value.DomainStart, spline.Value.DomainEnd);
            var rational = Step242SubsetDecoder.TryGetConstructor(source.Instance, "RATIONAL_B_SPLINE_CURVE");
            if (rational is null)
                return Success(PcurveGeometry.Polynomial(splineDomain, spline.Value));
            var weights = Step242SubsetDecoder.ReadRationalBSplineCurveWeights(new Step242ParsedEntity(source.Id, new Step242SimpleEntityInstance(rational)));
            if (!weights.IsSuccess) return KernelResult<DecodedPcurveGeometry>.Failure(weights.Diagnostics);
            try { return Success(PcurveGeometry.RationalPolynomial(splineDomain, spline.Value, weights.Value)); }
            catch (ArgumentException exception) { return Failure<DecodedPcurveGeometry>(exception.Message, "Importer.Pcurve.RationalBSpline"); }
        }

        return Failure<DecodedPcurveGeometry>($"Unsupported pcurve form '{DescribeCurveType(source)}'.", "Importer.Pcurve.UnsupportedCurve");

        KernelResult<DecodedPcurveGeometry> Success(PcurveGeometry geometry) =>
            KernelResult<DecodedPcurveGeometry>.Success(new(geometry, sameSense));
    }

    private static KernelResult<PcurveGeometry> DecodeConic(Step242ParsedDocument document, Step242EntityConstructor conic, ParameterInterval domain, bool circle)
    {
        if (conic.Arguments.ElementAtOrDefault(1) is not Step242EntityReference placementReference
            || conic.Arguments.ElementAtOrDefault(2) is not Step242NumberValue firstRadius)
            return Failure<PcurveGeometry>("Pcurve conic requires a 2D placement and radius.", "Importer.Pcurve.Conic");
        var placementResult = document.TryGetEntity(placementReference.TargetId, "AXIS2_PLACEMENT_2D");
        if (!placementResult.IsSuccess) return KernelResult<PcurveGeometry>.Failure(placementResult.Diagnostics);
        if (placementResult.Value.Arguments.ElementAtOrDefault(1) is not Step242EntityReference locationReference)
            return Failure<PcurveGeometry>("AXIS2_PLACEMENT_2D requires a location.", "Importer.Pcurve.Conic");
        var center = ReadUvPoint(document, locationReference.TargetId);
        if (!center.IsSuccess) return KernelResult<PcurveGeometry>.Failure(center.Diagnostics);
        var axis = new SurfaceParameterPoint(1d, 0d);
        if (placementResult.Value.Arguments.ElementAtOrDefault(2) is Step242EntityReference axisReference)
        {
            var direction = ReadUvDirection(document, axisReference.TargetId);
            if (!direction.IsSuccess) return KernelResult<PcurveGeometry>.Failure(direction.Diagnostics);
            axis = direction.Value;
        }
        var perpendicular = new SurfaceParameterPoint(-axis.V, axis.U);
        var secondRadius = circle ? firstRadius.Value
            : conic.Arguments.ElementAtOrDefault(3) is Step242NumberValue minor ? minor.Value : double.NaN;
        if (!double.IsFinite(secondRadius) || firstRadius.Value <= 0d || secondRadius <= 0d)
            return Failure<PcurveGeometry>("Pcurve conic radii must be finite and positive.", "Importer.Pcurve.Conic");
        // Use the coefficient form for circles too: unlike the compact Circle representation it
        // retains AXIS2_PLACEMENT_2D rotation exactly.
        return KernelResult<PcurveGeometry>.Success(PcurveGeometry.Ellipse(domain, center.Value,
            new SurfaceParameterPoint(axis.U * firstRadius.Value, axis.V * firstRadius.Value),
            new SurfaceParameterPoint(perpendicular.U * secondRadius, perpendicular.V * secondRadius)));
    }

    private static KernelResult<SurfaceParameterPoint> ReadUvPoint(Step242ParsedDocument document, int entityId)
    {
        var entity = document.TryGetEntity(entityId, "CARTESIAN_POINT");
        if (!entity.IsSuccess) return KernelResult<SurfaceParameterPoint>.Failure(entity.Diagnostics);
        if (entity.Value.Arguments.ElementAtOrDefault(1) is not Step242ListValue coordinates || coordinates.Items.Count != 2
            || coordinates.Items[0] is not Step242NumberValue u || coordinates.Items[1] is not Step242NumberValue v)
            return Failure<SurfaceParameterPoint>("Pcurve CARTESIAN_POINT must contain exactly two coordinates.", "Importer.Pcurve.Point2D");
        return KernelResult<SurfaceParameterPoint>.Success(new SurfaceParameterPoint(u.Value, v.Value));
    }

    private static KernelResult<SurfaceParameterPoint> ReadUvVector(Step242ParsedDocument document, int entityId)
    {
        var vector = document.TryGetEntity(entityId, "VECTOR");
        if (!vector.IsSuccess) return KernelResult<SurfaceParameterPoint>.Failure(vector.Diagnostics);
        if (vector.Value.Arguments.ElementAtOrDefault(1) is not Step242EntityReference directionReference
            || vector.Value.Arguments.ElementAtOrDefault(2) is not Step242NumberValue magnitude)
            return Failure<SurfaceParameterPoint>("Pcurve VECTOR requires direction and magnitude.", "Importer.Pcurve.Vector2D");
        var direction = ReadUvDirection(document, directionReference.TargetId);
        if (!direction.IsSuccess) return KernelResult<SurfaceParameterPoint>.Failure(direction.Diagnostics);
        return KernelResult<SurfaceParameterPoint>.Success(new(direction.Value.U * magnitude.Value, direction.Value.V * magnitude.Value));
    }

    private static KernelResult<SurfaceParameterPoint> ReadUvDirection(Step242ParsedDocument document, int entityId)
    {
        var entity = document.TryGetEntity(entityId, "DIRECTION");
        if (!entity.IsSuccess) return KernelResult<SurfaceParameterPoint>.Failure(entity.Diagnostics);
        if (entity.Value.Arguments.ElementAtOrDefault(1) is not Step242ListValue coordinates || coordinates.Items.Count != 2
            || coordinates.Items[0] is not Step242NumberValue u || coordinates.Items[1] is not Step242NumberValue v)
            return Failure<SurfaceParameterPoint>("Pcurve DIRECTION must contain exactly two ratios.", "Importer.Pcurve.Direction2D");
        var length = double.Sqrt(u.Value * u.Value + v.Value * v.Value);
        if (!double.IsFinite(length) || length <= 1e-15d)
            return Failure<SurfaceParameterPoint>("Pcurve DIRECTION is degenerate.", "Importer.Pcurve.Direction2D");
        return KernelResult<SurfaceParameterPoint>.Success(new(u.Value / length, v.Value / length));
    }

    private static bool TryReadTrim(Step242Value? value, out double parameter)
    {
        parameter = default;
        if (value is not Step242ListValue { Items.Count: 1 } list) return false;
        var item = list.Items[0];
        if (item is Step242TypedValue { Arguments.Count: 1 } typed) item = typed.Arguments[0];
        if (item is not Step242NumberValue number || !double.IsFinite(number.Value)) return false;
        parameter = number.Value;
        return true;
    }

    private static string DescribeCurveType(Step242ParsedEntity entity) => entity.Instance switch
    {
        Step242SimpleEntityInstance simple => simple.Constructor.Name,
        Step242ComplexEntityInstance complex => string.Join("+", complex.Constructors.Select(constructor => constructor.Name)),
        _ => entity.Name
    };

    private static KernelResult<T> Failure<T>(string message, string source) => KernelResult<T>.Failure([
        new KernelDiagnostic(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, message, source)]);

    private readonly record struct DecodedPcurveGeometry(PcurveGeometry Geometry, bool SameSense);
}
