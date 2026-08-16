using System.Diagnostics;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.SheetMetal;

public enum SemanticGeometryTargetKind
{
    RegionBoundary, ProfileMember, ProfileDeltaMember, ProfileCorner,
    AttachmentPath, BendTermination, Opening, Bend
}

public enum SemanticGeometryComparisonStatus
{
    Pass, PassWithKnownDifference, NeedsReview, MissingNative, ExtraNative,
    AmbiguousSource, UnsupportedComparison
}

public enum SemanticGeometryDifferenceClassification
{
    None, ParameterMismatch, MissingFeature, WrongProfileOperation, WrongAttachment,
    WrongTermination, WrongArcDirection, SegmentationOnly, ReferenceSurfaceDifference,
    SourceArtifact, Unknown
}

/// <summary>Observed source topology is kept separate from its engineer-facing interpretation.</summary>
public sealed record SemanticSourceGeometryEvidence(
    string SourceRegionId,
    IReadOnlyList<string> FlatSegmentIds,
    IReadOnlyList<int> SourceEdgeIds,
    string Observation,
    string Interpretation,
    bool InterpretationIsDerived);

public sealed record SemanticNativeGeometryEvidence(
    string SemanticPath,
    IReadOnlyList<string> FlatSegmentIds,
    string Provenance);

public sealed record SemanticLocalFrame(
    SheetPoint2 Origin, SheetPoint2 Along, SheetPoint2 Normal, string Description);

public sealed record SemanticAnalyticDifference(
    string SourceFamily,
    string NativeFamily,
    double? DirectionDegrees = null,
    double? Offset = null,
    double? Span = null,
    double? Center = null,
    double? Radius = null,
    double? DomainDegrees = null,
    double? StartEndpoint = null,
    double? EndEndpoint = null);

public sealed record SemanticLocalMetrics(
    SheetResidualStatistics SourceToNative,
    SheetResidualStatistics NativeToSource,
    double LengthResidual,
    double StartEndpointResidual,
    double EndEndpointResidual,
    int SourceCurveCount,
    int NativeCurveCount,
    IReadOnlyList<string> SourceFamilies,
    IReadOnlyList<string> NativeFamilies,
    SemanticAnalyticDifference? Analytic);

public sealed record SemanticGeometryTargetComparison(
    string SemanticPath,
    SemanticGeometryTargetKind GeometryKind,
    SemanticSourceGeometryEvidence SourceEvidence,
    SemanticNativeGeometryEvidence NativeEvidence,
    SemanticLocalFrame Frame,
    string Domain,
    string ExpectedRelation,
    SemanticLocalMetrics Metrics,
    SemanticGeometryDifferenceClassification Classification,
    SemanticGeometryComparisonStatus Status);

public sealed record SemanticFlatComparisonReport(
    SheetMetalComparisonStatus Status,
    double Tolerance,
    IReadOnlyList<SemanticGeometryTargetComparison> Targets,
    RecoveredFlatComparisonReport Global,
    TimeSpan TargetCreationTime,
    TimeSpan ComparisonTime,
    string DeterministicHash);

public sealed record SemanticFormedTerminationComparison(
    string SemanticPath,string SourceBendId,string NativeBendId,
    SheetBendTerminationTreatment Treatment,double AxialEndpointResidual,double? FlatResidual,
    SemanticGeometryDifferenceClassification Classification,SemanticGeometryComparisonStatus Status);

public sealed record SemanticFormedComparisonReport(
    SheetMetalComparisonStatus Status,double Tolerance,
    IReadOnlyList<SemanticFormedTerminationComparison> Terminations,
    IReadOnlyList<SheetBendComparison> Bends,IReadOnlyList<SheetFeatureComparison> Openings,
    TimeSpan ComparisonTime,string DeterministicHash);

/// <summary>
/// Compares recovered and native flat geometry by stable engineering identity. Source and
/// native curves may be split differently: local evidence is an ordered bounded chain and
/// distance is evaluated against analytic line/arc supports, never by entity ordinal.
/// </summary>
public static class SemanticSheetMetalComparer
{
    private const double SampleSpacing = .1d;
    private sealed record CurveRef(string Id, LineArcProfileCurve2D Curve, IReadOnlyList<int> SourceEdges);
    private sealed record RegionPair(FlatRegion2D Source, FlatRegion2D Native, string NativePath);

    public static SemanticFlatComparisonReport CompareFlat(
        RecoveredFlatReference source,
        SheetMetalPartIr nativePart,
        SheetMetalFlatPatternIr nativeFlat,
        double tolerance = .1d,
        SheetMetalPartIr? sourcePart = null)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(nativePart); ArgumentNullException.ThrowIfNull(nativeFlat);
        if (!double.IsFinite(tolerance) || tolerance <= 0d) throw new ArgumentOutOfRangeException(nameof(tolerance));
        var creation = Stopwatch.StartNew();
        var sourceOrigin = Origin(source.OuterAndInnerContours?.OuterLoop.Segments.Select(x => x.Geometry) ?? source.Regions.SelectMany(x => x.ExactContour?.OuterLoop.Segments.Select(y => y.Geometry) ?? []));
        var nativeOrigin = Origin(nativeFlat.ExactBlankContour?.OuterLoop.Segments.Select(x => x.Geometry) ?? nativeFlat.Regions2D.SelectMany(x => x.ExactContour?.OuterLoop.Segments.Select(y => y.Geometry) ?? []));
        var shift = new SheetPoint2(sourceOrigin.X - nativeOrigin.X, sourceOrigin.Y - nativeOrigin.Y);
        var pairs = PairRegions(source.Regions, nativeFlat.Regions2D, shift,sourcePart,nativePart);
        creation.Stop();

        var comparison = Stopwatch.StartNew();
        var targets = new List<SemanticGeometryTargetComparison>();
        foreach (var pair in pairs)
        {
            var sourceCurves = Curves(pair.Source.ExactContour, default);
            var sourceMap=source.RegionMap.First(x=>x.SourceRegionId==pair.Source.SourceRegionId);
            var nativeMap=nativeFlat.SourceToFlatMappings.First(x=>x.SourceRegionId==pair.Native.SourceRegionId);
            var nativeCurves = Curves(pair.Native.ExactContour, default).Select(x=>x with { Curve=Transform(x.Curve,nativeMap,sourceMap) }).ToArray();
            if (sourceCurves.Count == 0 || nativeCurves.Length == 0) continue;
            targets.Add(CompareTarget(pair.NativePath, SemanticGeometryTargetKind.RegionBoundary, pair.Source.SourceRegionId,
                sourceCurves, nativeCurves, source.SourceProvenance, tolerance,
                "complete recovered planar-region boundary", "native semantic planar-region boundary", false));

            foreach (var group in nativeCurves.Where(x => IsSemanticDescendant(x.Id)).GroupBy(x => SemanticPath(x.Id), StringComparer.Ordinal).OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var kind = Kind(group.Key, nativePart);
                var localSource = SelectBoundedSourceChain(sourceCurves, group.ToArray());
                targets.Add(CompareTarget(group.Key, kind, pair.Source.SourceRegionId, localSource, group.ToArray(),
                    source.SourceProvenance, tolerance, "bounded recovered contour chain selected by geometry",
                    "native semantic descendant chain", true));
            }
        }

        AddAttachmentTargets(targets, source, nativePart, nativeFlat, pairs, tolerance);
        AddTerminationTargets(targets,source,nativePart,nativeFlat,pairs,tolerance);
        AddOpeningTargets(targets, source, nativeFlat, pairs, tolerance);
        AddBendTargets(targets, source,sourcePart,nativePart, nativeFlat, pairs, shift, tolerance);
        comparison.Stop();
        targets = targets.GroupBy(x => (x.SemanticPath, x.GeometryKind)).Select(x => x.OrderBy(y => y.Metrics.SourceToNative.Maximum).First())
            .OrderByDescending(x => Worst(x.Metrics)).ThenBy(x => x.SemanticPath, StringComparer.Ordinal).ToList();
        var global = RecoveredFlatComparer.Compare(source, nativeFlat, tolerance);
        var status = targets.Any(x => x.Status is SemanticGeometryComparisonStatus.NeedsReview or SemanticGeometryComparisonStatus.MissingNative or SemanticGeometryComparisonStatus.ExtraNative)
            ? SheetMetalComparisonStatus.NeedsReview : targets.Any(x => x.Status == SemanticGeometryComparisonStatus.PassWithKnownDifference)
                ? SheetMetalComparisonStatus.PassWithKnownDifferences : SheetMetalComparisonStatus.Pass;
        var hash = SheetMetalRecognizer.StableHash(string.Join('|', tolerance.ToString("R"), targets.Select(x => $"{x.SemanticPath}:{x.GeometryKind}:{x.Status}:{x.Classification}:{Worst(x.Metrics):R}")));
        return new(status, tolerance, targets, global, creation.Elapsed, comparison.Elapsed, hash);
    }

    public static SemanticFormedComparisonReport CompareFormed(SheetMetalPartIr source,SheetMetalPartIr native,SemanticFlatComparisonReport? flat=null,double tolerance=.1d)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(native);var clock=Stopwatch.StartNew();var aggregate=SheetMetalIntentComparer.Compare(source,native);var terminations=new List<SemanticFormedTerminationComparison>();
        foreach(var nativeBend in native.Bends.OrderBy(x=>x.StableId,StringComparer.Ordinal))
        {
            var sourceBend=source.Bends.OrderBy(x=>AxisDistance(x,nativeBend)+Math.Abs(x.BendAngleRadians-nativeBend.BendAngleRadians)*10+Math.Abs(x.InsideRadius-nativeBend.InsideRadius)).FirstOrDefault();if(sourceBend is null)continue;
            var cylinder=source.Regions.FirstOrDefault(x=>x.StableId==sourceBend.StableId)?.Cylinder??source.Regions.Where(x=>x.Cylinder is not null).OrderBy(x=>(x.Cylinder!.AxisOrigin-sourceBend.AxisOrigin).Length).FirstOrDefault()?.Cylinder;if(cylinder is null)continue;
            var axis=sourceBend.AxisDirection.TryNormalize(out var normalized)?normalized:sourceBend.AxisDirection;var half=cylinder.AxisLength/2d;
            foreach(var termination in new[]{nativeBend.StartTermination,nativeBend.EndTermination}.OfType<SheetBendTerminationIr>())
            {
                var axial=(termination.RootPoint-sourceBend.AxisOrigin).Dot(axis);var formedResidual=Math.Min(Math.Abs(axial-half),Math.Abs(axial+half));var flatTarget=flat?.Targets.FirstOrDefault(x=>x.SemanticPath==termination.StableId);var flatResidual=flatTarget is null?(double?)null:Worst(flatTarget.Metrics);var pass=formedResidual<=tolerance&&(flatResidual is null||flatResidual<=tolerance);
                terminations.Add(new(termination.StableId,sourceBend.StableId,nativeBend.StableId,termination.ResolvedTreatment,formedResidual,flatResidual,pass?SemanticGeometryDifferenceClassification.None:SemanticGeometryDifferenceClassification.WrongTermination,pass?SemanticGeometryComparisonStatus.Pass:SemanticGeometryComparisonStatus.NeedsReview));
            }
        }
        clock.Stop();var status=terminations.Any(x=>x.Status==SemanticGeometryComparisonStatus.NeedsReview)||aggregate.Bends.Any(x=>x.Status==SheetMetalComparisonStatus.Fail)||aggregate.Features.Any(x=>x.Status==SheetMetalComparisonStatus.Fail)?SheetMetalComparisonStatus.NeedsReview:SheetMetalComparisonStatus.Pass;
        var hash=SheetMetalRecognizer.StableHash(string.Join('|',terminations.OrderBy(x=>x.SemanticPath,StringComparer.Ordinal).Select(x=>$"{x.SemanticPath}:{x.AxialEndpointResidual:R}:{x.FlatResidual:R}:{x.Status}")));
        return new(status,tolerance,terminations.OrderBy(x=>x.SemanticPath,StringComparer.Ordinal).ToArray(),aggregate.Bends,aggregate.Features,clock.Elapsed,hash);
    }

    private static IReadOnlyList<RegionPair> PairRegions(IReadOnlyList<FlatRegion2D> source, IReadOnlyList<FlatRegion2D> native, SheetPoint2 shift,SheetMetalPartIr? sourcePart,SheetMetalPartIr nativePart)
    {
        var s = source.Where(x => x.Kind == SheetRegionKind.Planar && x.ExactContour is not null).ToArray();
        var n = native.Where(x => x.Kind == SheetRegionKind.Planar && x.ExactContour is not null).ToArray();
        var candidates = (from a in s from b in n let score = sourcePart is null?RegionScore(Curves(a.ExactContour, default), Curves(b.ExactContour, shift)):FormedRegionScore(a,b,sourcePart,nativePart) select (a, b, score))
            .OrderBy(x => x.score).ThenBy(x => x.a.SourceRegionId, StringComparer.Ordinal).ThenBy(x => x.b.SourceRegionId, StringComparer.Ordinal).ToArray();
        var usedS = new HashSet<string>(StringComparer.Ordinal); var usedN = new HashSet<string>(StringComparer.Ordinal); var result = new List<RegionPair>();
        foreach (var item in candidates)
            if (usedS.Add(item.a.StableId) && usedN.Add(item.b.StableId)) result.Add(new(item.a, item.b, item.b.SourceRegionId));
        return result.OrderBy(x => x.NativePath, StringComparer.Ordinal).ToArray();
    }

    private static double FormedRegionScore(FlatRegion2D source,FlatRegion2D native,SheetMetalPartIr sourcePart,SheetMetalPartIr nativePart)
    {
        var a=sourcePart.Regions.First(x=>x.StableId==source.SourceRegionId);var b=nativePart.Regions.First(x=>x.StableId==native.SourceRegionId);
        var ac=a.Boundary3D.Count==0?Point3D.Origin:new(a.Boundary3D.Average(x=>x.X),a.Boundary3D.Average(x=>x.Y),a.Boundary3D.Average(x=>x.Z));
        var bc=b.Boundary3D.Count==0?Point3D.Origin:new(b.Boundary3D.Average(x=>x.X),b.Boundary3D.Average(x=>x.Y),b.Boundary3D.Average(x=>x.Z));
        var normal=a.Plane is null||b.Plane is null?0:1-Math.Abs(a.Plane.Normal.Dot(b.Plane.Normal));
        return (ac-bc).Length+Math.Abs(a.ApproximateArea-b.ApproximateArea)/Math.Max(1,Math.Sqrt(Math.Max(a.ApproximateArea,b.ApproximateArea)))+normal*100;
    }

    private static double RegionScore(IReadOnlyList<CurveRef> a, IReadOnlyList<CurveRef> b)
    {
        var ap = Sample(a, 2d); var bp = Sample(b, 2d);
        if (ap.Count == 0 || bp.Count == 0) return double.PositiveInfinity;
        var ac = new SheetPoint2(ap.Average(x => x.X), ap.Average(x => x.Y)); var bc = new SheetPoint2(bp.Average(x => x.X), bp.Average(x => x.Y));
        var extent = Math.Abs(Extent(ap, true) - Extent(bp, true)) + Math.Abs(Extent(ap, false) - Extent(bp, false));
        return Distance(ac, bc) + extent * .25d;
    }

    private static void AddOpeningTargets(List<SemanticGeometryTargetComparison> targets, RecoveredFlatReference source,
        SheetMetalFlatPatternIr native, IReadOnlyList<RegionPair> pairs, double tolerance)
    {
        var remaining = source.InnerContours.Where(x => x.ExactContour is not null).ToList();
        foreach (var cut in native.CutLoops.Where(x => x.ExactContour is not null).OrderBy(x => x.FeatureId, StringComparer.Ordinal))
        {
            var pair = pairs.FirstOrDefault(x => x.Native.SourceRegionId == cut.SourceRegionId);
            if(pair is null)continue;
            var sourceMap=source.RegionMap.First(x=>x.SourceRegionId==pair.Source.SourceRegionId);var nativeMap=native.SourceToFlatMappings.First(x=>x.SourceRegionId==pair.Native.SourceRegionId);
            var nc = Curves(cut.ExactContour, default).Select(x=>x with { Curve=Transform(x.Curve,nativeMap,sourceMap) }).ToArray(); var center = BoundsCenter(Sample(nc, 2));
            var candidates = pair is null ? remaining : remaining.Where(x => x.SourceRegionId == pair.Source.SourceRegionId).ToList();
            var match = candidates.OrderBy(x => Distance(center, BoundsCenter(Sample(Curves(x.ExactContour, default), 2)))).FirstOrDefault();
            if (match is null) continue; remaining.Remove(match);
            targets.Add(CompareTarget(cut.FeatureId, SemanticGeometryTargetKind.Opening, match.SourceRegionId,
                Curves(match.ExactContour, default), nc, source.SourceProvenance, tolerance,
                "recovered source inner contour", "native semantic opening", false));
        }
    }

    private static void AddBendTargets(List<SemanticGeometryTargetComparison> targets, RecoveredFlatReference source,SheetMetalPartIr? sourcePart,
        SheetMetalPartIr part, SheetMetalFlatPatternIr native, IReadOnlyList<RegionPair> pairs, SheetPoint2 shift, double tolerance)
    {
        var remaining = source.BendLines.ToList();
        foreach (var bend in native.BendLines.OrderBy(x => x.BendId, StringComparer.Ordinal))
        {
            LineArcProfileCurve2D nativeCurve=new LineArcLineSegment2D((bend.Start.X+shift.X,bend.Start.Y+shift.Y),(bend.End.X+shift.X,bend.End.Y+shift.Y));
            var bendIr=part.Bends.FirstOrDefault(x=>x.StableId==bend.BendId);var pair=bendIr is null?null:pairs.FirstOrDefault(x=>x.Native.SourceRegionId==bendIr.AdjacentRegionA)??pairs.FirstOrDefault(x=>x.Native.SourceRegionId==bendIr.AdjacentRegionB);
            if(pair is not null){var sm=source.RegionMap.First(x=>x.SourceRegionId==pair.Source.SourceRegionId);var nm=native.SourceToFlatMappings.First(x=>x.SourceRegionId==pair.Native.SourceRegionId);nativeCurve=Transform(new LineArcLineSegment2D((bend.Start.X,bend.Start.Y),(bend.End.X,bend.End.Y)),nm,sm);}
            var nc = new[] { new CurveRef(bend.BendId,nativeCurve,[]) };
            FlatBendLine? match=null;
            if(sourcePart is not null&&bendIr is not null)
            {
                var sourceBend=sourcePart.Bends.OrderBy(x=>AxisDistance(x,bendIr)+Math.Abs(x.BendAngleRadians-bendIr.BendAngleRadians)*10+Math.Abs(x.InsideRadius-bendIr.InsideRadius)).FirstOrDefault();
                if(sourceBend is not null)match=remaining.FirstOrDefault(x=>x.BendId==sourceBend.StableId);
            }
            match??=remaining.OrderBy(x => Distance(Mid(x.Start, x.End), Mid(Start(nativeCurve),End(nativeCurve)))).FirstOrDefault();
            if (match is null) continue; remaining.Remove(match);
            var sc = new[] { new CurveRef(match.BendId, new LineArcLineSegment2D((match.Start.X, match.Start.Y), (match.End.X, match.End.Y)), []) };
            var result = CompareTarget(bend.BendId, SemanticGeometryTargetKind.Bend, match.BendId, sc, nc, source.SourceProvenance,
                tolerance, "recovered bend line and analytic bend parameters", "native semantic bend", false);
            var parameterMismatch = Math.Abs(match.BendAngleRadians - bend.BendAngleRadians) * 180 / Math.PI > .05 || Math.Abs(match.InsideRadius - bend.InsideRadius) > .05;
            targets.Add(parameterMismatch ? result with { Status = SemanticGeometryComparisonStatus.NeedsReview, Classification = SemanticGeometryDifferenceClassification.ParameterMismatch } : result);
        }
    }

    private static void AddTerminationTargets(List<SemanticGeometryTargetComparison> targets,RecoveredFlatReference source,SheetMetalPartIr part,SheetMetalFlatPatternIr flat,IReadOnlyList<RegionPair> pairs,double tolerance)
    {
        foreach(var termination in part.Bends.SelectMany(x=>new[]{x.StartTermination,x.EndTermination}).OfType<SheetBendTerminationIr>().OrderBy(x=>x.StableId,StringComparer.Ordinal))
        {
            var pair=pairs.FirstOrDefault(x=>x.Native.SourceRegionId==termination.AdjacentRegionId);if(pair is null)continue;
            var nativeMap=flat.SourceToFlatMappings.First(x=>x.SourceRegionId==pair.Native.SourceRegionId);var sourceMap=source.RegionMap.First(x=>x.SourceRegionId==pair.Source.SourceRegionId);
            var d=termination.RootPoint-nativeMap.PlaneOrigin;var flatPoint=new SheetPoint2(nativeMap.FlatOrigin.X+nativeMap.FlatU.X*d.Dot(nativeMap.SourceU)+nativeMap.FlatV.X*d.Dot(nativeMap.SourceV),nativeMap.FlatOrigin.Y+nativeMap.FlatU.Y*d.Dot(nativeMap.SourceU)+nativeMap.FlatV.Y*d.Dot(nativeMap.SourceV));
            var mapped=MapPoint(flatPoint,nativeMap,sourceMap);var bend=part.Bends.First(x=>x.StableId==termination.BendId);var tangent=bend.AxisDirection.TryNormalize(out var axis)?axis:new Vector3D(1,0,0);var probe3=termination.RootPoint+tangent*1e-4;var pd=probe3-nativeMap.PlaneOrigin;var probeFlat=new SheetPoint2(nativeMap.FlatOrigin.X+nativeMap.FlatU.X*pd.Dot(nativeMap.SourceU)+nativeMap.FlatV.X*pd.Dot(nativeMap.SourceV),nativeMap.FlatOrigin.Y+nativeMap.FlatU.Y*pd.Dot(nativeMap.SourceU)+nativeMap.FlatV.Y*pd.Dot(nativeMap.SourceV));var probe=MapPoint(probeFlat,nativeMap,sourceMap);
            var nativeCurve=new[]{new CurveRef(termination.StableId,new LineArcLineSegment2D((mapped.X,mapped.Y),(probe.X,probe.Y)),[])};var sourceCurves=source.OuterAndInnerContours is null?Curves(pair.Source.ExactContour,default):source.OuterAndInnerContours.OuterLoop.Segments.Select(x=>new CurveRef(x.StableId,x.Geometry,[])).ToArray();var selected=SelectBoundedSourceChain(sourceCurves,nativeCurve);
            targets.Add(CompareTarget(termination.StableId,SemanticGeometryTargetKind.BendTermination,pair.Source.SourceRegionId,selected,nativeCurve,source.SourceProvenance,tolerance,
                $"accepted stitched recovered blank at root endpoint near {termination.NeighborBoundary}",$"native {termination.ResolvedTreatment} termination; setback={termination.Setback:R}; depth={termination.Depth:R}",true));
        }
    }

    private static void AddAttachmentTargets(List<SemanticGeometryTargetComparison> targets, RecoveredFlatReference source,
        SheetMetalPartIr part, SheetMetalFlatPatternIr flat, IReadOnlyList<RegionPair> pairs, double tolerance)
    {
        foreach (var path in (part.AttachmentPaths ?? []).OrderBy(x => x.StableId, StringComparer.Ordinal))
        {
            var mapping = flat.SourceToFlatMappings.FirstOrDefault(x => x.SourceRegionId == path.OwningRegionId); if (mapping is null) continue;
            var pair = pairs.FirstOrDefault(x => x.Native.SourceRegionId == path.OwningRegionId); if (pair is null) continue;
            SheetPoint2 Map(Point3D point) { var d = point - mapping.PlaneOrigin; return new(mapping.FlatOrigin.X + mapping.FlatU.X * d.Dot(mapping.SourceU) + mapping.FlatV.X * d.Dot(mapping.SourceV), mapping.FlatOrigin.Y + mapping.FlatU.Y * d.Dot(mapping.SourceU) + mapping.FlatV.Y * d.Dot(mapping.SourceV)); }
            var a = Map(path.Start); var b = Map(path.End);var sourceMap=source.RegionMap.First(x=>x.SourceRegionId==pair.Source.SourceRegionId);var nativeCurve=Transform(new LineArcLineSegment2D((a.X,a.Y),(b.X,b.Y)),mapping,sourceMap); var nc = new[] { new CurveRef(path.StableId,nativeCurve,[]) };
            var sc = SelectBoundedSourceChain(Curves(pair.Source.ExactContour, default), nc);
            targets.Add(CompareTarget(path.StableId, SemanticGeometryTargetKind.AttachmentPath, pair.Source.SourceRegionId, sc, nc,
                source.SourceProvenance, tolerance, "recovered physical carrier interpreted as bounded attachment land", "native AttachmentPath", true));
        }
    }

    private static SemanticGeometryTargetComparison CompareTarget(string path, SemanticGeometryTargetKind kind, string sourceRegion,
        IReadOnlyList<CurveRef> source, IReadOnlyList<CurveRef> native, IReadOnlyList<RecoveredFlatSegmentProvenance> provenance,
        double tolerance, string observation, string nativeProvenance, bool derived)
    {
        var sp = Sample(source, SampleSpacing); var np = Sample(native, SampleSpacing);
        var s2n = Stats(sp.Select(x => Nearest(x, native))); var n2s = Stats(np.Select(x => Nearest(x, source)));
        var closed=kind is SemanticGeometryTargetKind.RegionBoundary or SemanticGeometryTargetKind.Opening;var endpoints = closed?(Start:0d,End:0d):EndpointResidual(source, native); var lengthResidual = Math.Abs(Length(source) - Length(native));
        var analytic = Analytic(source, native); var sourceFamilies = source.Select(x => Family(x.Curve)).Distinct().Order(StringComparer.Ordinal).ToArray(); var nativeFamilies = native.Select(x => Family(x.Curve)).Distinct().Order(StringComparer.Ordinal).ToArray();
        var metrics = new SemanticLocalMetrics(s2n, n2s, lengthResidual, endpoints.Start, endpoints.End, source.Count, native.Count, sourceFamilies, nativeFamilies, analytic);
        var pass = Worst(metrics) <= tolerance;
        var segmentation = pass && (source.Count != native.Count || !sourceFamilies.SequenceEqual(nativeFamilies));
        var status = pass ? segmentation ? SemanticGeometryComparisonStatus.PassWithKnownDifference : SemanticGeometryComparisonStatus.Pass : SemanticGeometryComparisonStatus.NeedsReview;
        var classification = pass ? segmentation ? SemanticGeometryDifferenceClassification.SegmentationOnly : SemanticGeometryDifferenceClassification.None
            : kind == SemanticGeometryTargetKind.AttachmentPath ? SemanticGeometryDifferenceClassification.WrongAttachment
            : kind == SemanticGeometryTargetKind.BendTermination ? SemanticGeometryDifferenceClassification.WrongTermination
            : sourceFamilies.SequenceEqual(nativeFamilies) ? SemanticGeometryDifferenceClassification.ParameterMismatch
            : SemanticGeometryDifferenceClassification.WrongProfileOperation;
        var ids = source.Select(x => x.Id).Distinct().Order(StringComparer.Ordinal).ToArray();
        var sourceEdges = ids.SelectMany(id => provenance.Where(x => x.FlatSegmentId == id || id.Contains(x.FlatSegmentId, StringComparison.Ordinal)).SelectMany(x => x.SourceEdgeIds)).Distinct().Order().ToArray();
        var origin = np.Count == 0 ? default : np[0]; var end = np.Count < 2 ? new SheetPoint2(origin.X + 1, origin.Y) : np[^1]; var along = Normalize(new(end.X - origin.X, end.Y - origin.Y));
        return new(path, kind, new(sourceRegion, ids, sourceEdges, observation, $"{sourceRegion} -> {path}", derived),
            new(path, native.Select(x => x.Id).Distinct().Order(StringComparer.Ordinal).ToArray(), nativeProvenance),
            new(origin, along, new(-along.Y, along.X), "semantic chain start; along-chain/profile-normal axes"),
            "bounded flat contour", "engineering geometry parity independent of BRep segmentation", metrics, classification, status);
    }

    private static IReadOnlyList<CurveRef> SelectBoundedSourceChain(IReadOnlyList<CurveRef> source, IReadOnlyList<CurveRef> target)
    {
        if (source.Count == 0 || target.Count == 0) return [];
        var start = Start(target[0].Curve); var end = End(target[^1].Curve);
        var samples = new List<(SheetPoint2 Point, int Curve, double T)>();
        for (var i = 0; i < source.Count; i++)
        {
            var count=Math.Max(2,(int)Math.Ceiling(CurveLength(source[i].Curve)/SampleSpacing)+1);
            for(var j=0;j<count;j++)samples.Add((PointAt(source[i].Curve,j/(double)(count-1)),i,j/(double)(count-1)));
        }
        var si = Enumerable.Range(0, samples.Count).MinBy(i => Distance(samples[i].Point, start));
        var ei = Enumerable.Range(0, samples.Count).MinBy(i => Distance(samples[i].Point, end));
        IReadOnlyList<int> Walk(int a, int b) { var list = new List<int>(); for (var i = a; ; i = (i + 1) % samples.Count) { list.Add(i); if (i == b || list.Count > samples.Count) break; } return list; }
        var forward = Walk(si, ei); var reverse = Walk(ei, si).Reverse().ToArray();
        double Score(IReadOnlyList<int> indices) => indices.Average(i => Nearest(samples[i].Point, target)) + Math.Abs(PathLength(indices.Select(i => samples[i].Point).ToArray()) - Length(target)) * .05;
        var selected = Score(forward) <= Score(reverse) ? forward : reverse;
        var result=new List<CurveRef>();var selectedSamples=selected.Select(i=>samples[i]).ToArray();
        for(var first=0;first<selectedSamples.Length;)
        {
            var last=first;while(last+1<selectedSamples.Length&&selectedSamples[last+1].Curve==selectedSamples[first].Curve)last++;
            var original=source[selectedSamples[first].Curve];result.Add(original with { Curve=Trim(original.Curve,selectedSamples[first].T,selectedSamples[last].T) });first=last+1;
        }
        return result;
    }

    private static SemanticAnalyticDifference? Analytic(IReadOnlyList<CurveRef> source, IReadOnlyList<CurveRef> native)
    {
        if (source.Count == 1 && native.Count == 1)
        {
            if (source[0].Curve is LineArcLineSegment2D a && native[0].Curve is LineArcLineSegment2D b)
            {
                var av = Normalize(new(a.End.X - a.Start.X, a.End.Y - a.Start.Y)); var bv = Normalize(new(b.End.X - b.Start.X, b.End.Y - b.Start.Y));
                var angle = Math.Acos(Math.Clamp(Math.Abs(Dot(av, bv)), -1, 1)) * 180 / Math.PI;
                return new("Line", "Line", angle, PointLineDistance(new(b.Start.X, b.Start.Y), a), Math.Abs(CurveLength(a) - CurveLength(b)), StartEndpoint: EndpointResidual(source, native).Start, EndEndpoint: EndpointResidual(source, native).End);
            }
            if (source[0].Curve is LineArcCircularArc2D aa && native[0].Curve is LineArcCircularArc2D ba)
                return new("Arc", "Arc", Center: Distance(new(aa.Center.X, aa.Center.Y), new(ba.Center.X, ba.Center.Y)), Radius: Math.Abs(aa.Radius - ba.Radius), DomainDegrees: Math.Abs(Math.Abs(aa.SweepAngleRadians) - Math.Abs(ba.SweepAngleRadians)) * 180 / Math.PI, StartEndpoint: EndpointResidual(source, native).Start, EndEndpoint: EndpointResidual(source, native).End);
            if (source[0].Curve is LineArcFullCircle2D ac && native[0].Curve is LineArcFullCircle2D bc)
                return new("Circle", "Circle", Center: Distance(new(ac.Center.X, ac.Center.Y), new(bc.Center.X, bc.Center.Y)), Radius: Math.Abs(ac.Radius - bc.Radius));
        }
        return new(string.Join('+', source.Select(x => Family(x.Curve)).Distinct()), string.Join('+', native.Select(x => Family(x.Curve)).Distinct()));
    }

    private static SemanticGeometryTargetKind Kind(string path, SheetMetalPartIr part)
    {
        if (part.Bends.SelectMany(x => new[] { x.StartTermination, x.EndTermination }).OfType<SheetBendTerminationIr>().Any(x => path.StartsWith(x.StableId, StringComparison.Ordinal))) return SemanticGeometryTargetKind.BendTermination;
        if ((part.AttachmentPaths ?? []).Any(x => path.StartsWith(x.StableId, StringComparison.Ordinal))) return SemanticGeometryTargetKind.AttachmentPath;
        if ((part.Correspondence ?? []).Any(x => x.Kind == "ProfileCorner" && path.StartsWith(x.SemanticId, StringComparison.Ordinal))) return SemanticGeometryTargetKind.ProfileCorner;
        return path.Contains("Profile", StringComparison.Ordinal) || path.Contains("Termination", StringComparison.Ordinal) ? SemanticGeometryTargetKind.ProfileDeltaMember : SemanticGeometryTargetKind.ProfileMember;
    }

    private static bool IsSemanticDescendant(string id) => !id.Contains("Carrier", StringComparison.Ordinal) && (id.Contains(".curve", StringComparison.Ordinal) || id.Contains("Corner", StringComparison.Ordinal) || id.Contains("Termination", StringComparison.Ordinal));
    private static string SemanticPath(string id)
    {
        var value = id.StartsWith("flat-", StringComparison.Ordinal) ? id[5..] : id;
        var curve = value.LastIndexOf(".curve", StringComparison.Ordinal); if (curve >= 0 && int.TryParse(value[(curve + 6)..], out _)) value = value[..curve];
        return value;
    }
    private static IReadOnlyList<CurveRef> Curves(PlanarContour2? contour, SheetPoint2 shift) => contour?.OuterLoop.Segments.Select(x => new CurveRef(x.StableId, Translate(x.Geometry, shift), [])).ToArray() ?? [];
    private static LineArcProfileCurve2D Translate(LineArcProfileCurve2D curve, SheetPoint2 s) => curve switch
    {
        LineArcLineSegment2D x => new LineArcLineSegment2D((x.Start.X + s.X, x.Start.Y + s.Y), (x.End.X + s.X, x.End.Y + s.Y)),
        LineArcCircularArc2D x => x with { Center = (x.Center.X + s.X, x.Center.Y + s.Y) },
        LineArcFullCircle2D x => x with { Center = (x.Center.X + s.X, x.Center.Y + s.Y) },
        _ => curve
    };
    private static LineArcProfileCurve2D Transform(LineArcProfileCurve2D curve,SourceToFlatMapping from,SourceToFlatMapping to)
    {
        SheetPoint2 Map(SheetPoint2 point)=>MapPoint(point,from,to);
        var o=Map(default);var ux=Map(new(1,0));var vy=Map(new(0,1));var orientation=Math.Sign((ux.X-o.X)*(vy.Y-o.Y)-(ux.Y-o.Y)*(vy.X-o.X));
        return curve switch
        {
            LineArcLineSegment2D line=>ToLine(Map(new(line.Start.X,line.Start.Y)),Map(new(line.End.X,line.End.Y))),
            LineArcCircularArc2D arc=>ToArc(arc,Map(new(arc.Center.X,arc.Center.Y)),Map(PointAt(arc,0)),orientation),
            LineArcFullCircle2D circle=>new LineArcFullCircle2D(ToTuple(Map(new(circle.Center.X,circle.Center.Y))),circle.Radius),
            _=>curve
        };
        static LineArcLineSegment2D ToLine(SheetPoint2 a,SheetPoint2 b)=>new((a.X,a.Y),(b.X,b.Y));
        static LineArcCircularArc2D ToArc(LineArcCircularArc2D arc,SheetPoint2 center,SheetPoint2 start,double orientation)=>new((center.X,center.Y),arc.Radius,Math.Atan2(start.Y-center.Y,start.X-center.X),arc.SweepAngleRadians*orientation);
        static (double X,double Y) ToTuple(SheetPoint2 p)=>(p.X,p.Y);
    }
    private static SheetPoint2 MapPoint(SheetPoint2 point,SourceToFlatMapping from,SourceToFlatMapping to)
    {
        var d=new SheetPoint2(point.X-from.FlatOrigin.X,point.Y-from.FlatOrigin.Y);var u=Dot(d,from.FlatU);var v=Dot(d,from.FlatV);var world=from.PlaneOrigin+from.SourceU*u+from.SourceV*v;var q=world-to.PlaneOrigin;
        return new(to.FlatOrigin.X+to.FlatU.X*q.Dot(to.SourceU)+to.FlatV.X*q.Dot(to.SourceV),to.FlatOrigin.Y+to.FlatU.Y*q.Dot(to.SourceU)+to.FlatV.Y*q.Dot(to.SourceV));
    }
    private static LineArcProfileCurve2D Trim(LineArcProfileCurve2D curve,double t0,double t1)
    {
        t0=Math.Clamp(t0,0,1);t1=Math.Clamp(t1,0,1);
        return curve switch
        {
            LineArcLineSegment2D=>new LineArcLineSegment2D((PointAt(curve,t0).X,PointAt(curve,t0).Y),(PointAt(curve,t1).X,PointAt(curve,t1).Y)),
            LineArcCircularArc2D arc=>new LineArcCircularArc2D(arc.Center,arc.Radius,arc.StartAngleRadians+arc.SweepAngleRadians*t0,arc.SweepAngleRadians*(t1-t0)),
            LineArcFullCircle2D circle=>new LineArcCircularArc2D(circle.Center,circle.Radius,2*Math.PI*t0,2*Math.PI*(t1-t0)),
            _=>curve
        };
    }
    private static SheetPoint2 Origin(IEnumerable<LineArcProfileCurve2D> curves) { var p = curves.SelectMany(x => Sample([new CurveRef("", x, [])], 2)).ToArray(); return p.Length == 0 ? default : new(p.Min(x => x.X), p.Min(x => x.Y)); }
    private static IReadOnlyList<SheetPoint2> Sample(IReadOnlyList<CurveRef> curves, double spacing) => curves.SelectMany(x => SampleCurve(x.Curve, spacing)).ToArray();
    private static IReadOnlyList<SheetPoint2> SampleCurve(LineArcProfileCurve2D curve, double spacing)
    {
        var count = Math.Max(2, (int)Math.Ceiling(CurveLength(curve) / spacing) + 1); var closed = curve is LineArcFullCircle2D;
        return Enumerable.Range(0, closed ? count - 1 : count).Select(i => PointAt(curve, i / (double)(count - 1))).ToArray();
    }
    private static double Nearest(SheetPoint2 point, IReadOnlyList<CurveRef> curves) => curves.Count == 0 ? double.PositiveInfinity : curves.Min(x => DistanceToCurve(point, x.Curve));
    private static double DistanceToCurve(SheetPoint2 p, LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => PointSegmentDistance(p, new(line.Start.X, line.Start.Y), new(line.End.X, line.End.Y)),
        LineArcCircularArc2D arc => PointArcDistance(p, arc),
        LineArcFullCircle2D circle => Math.Abs(Distance(p, new(circle.Center.X, circle.Center.Y)) - circle.Radius),
        _ => double.PositiveInfinity
    };
    private static double PointArcDistance(SheetPoint2 p, LineArcCircularArc2D arc)
    {
        var angle = Math.Atan2(p.Y - arc.Center.Y, p.X - arc.Center.X); if (AngleInSweep(angle, arc.StartAngleRadians, arc.SweepAngleRadians)) return Math.Abs(Distance(p, new(arc.Center.X, arc.Center.Y)) - arc.Radius);
        return Math.Min(Distance(p, PointAt(arc, 0)), Distance(p, PointAt(arc, 1)));
    }
    private static bool AngleInSweep(double angle, double start, double sweep) { var d = NormalizeAngle(angle - start); return sweep >= 0 ? d <= sweep + 1e-10 : NormalizeAngle(start - angle) <= -sweep + 1e-10; }
    private static double NormalizeAngle(double a) { while (a < 0) a += 2 * Math.PI; while (a >= 2 * Math.PI) a -= 2 * Math.PI; return a; }
    private static double PointSegmentDistance(SheetPoint2 p, SheetPoint2 a, SheetPoint2 b) { var ab = new SheetPoint2(b.X - a.X, b.Y - a.Y); var d = Dot(ab, ab); var t = d <= 1e-20 ? 0 : Math.Clamp(Dot(new(p.X - a.X, p.Y - a.Y), ab) / d, 0, 1); return Distance(p, new(a.X + ab.X * t, a.Y + ab.Y * t)); }
    private static double PointLineDistance(SheetPoint2 p, LineArcLineSegment2D line) { var a = new SheetPoint2(line.Start.X, line.Start.Y); var b = new SheetPoint2(line.End.X, line.End.Y); var v = new SheetPoint2(b.X - a.X, b.Y - a.Y); return Math.Abs((p.X - a.X) * v.Y - (p.Y - a.Y) * v.X) / Math.Max(1e-20, Math.Sqrt(Dot(v, v))); }
    private static SheetResidualStatistics Stats(IEnumerable<double> values) { var v = values.Where(double.IsFinite).Order().ToArray(); return v.Length == 0 ? new(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, 0) : new(Math.Sqrt(v.Average(x => x * x)), v[Math.Max(0, (int)Math.Ceiling(.95 * v.Length) - 1)], v[^1], v.Length); }
    private static (double Start, double End) EndpointResidual(IReadOnlyList<CurveRef> a, IReadOnlyList<CurveRef> b) { if (a.Count == 0 || b.Count == 0) return (double.PositiveInfinity, double.PositiveInfinity); var direct = (Distance(Start(a[0].Curve), Start(b[0].Curve)), Distance(End(a[^1].Curve), End(b[^1].Curve))); var reverse = (Distance(Start(a[0].Curve), End(b[^1].Curve)), Distance(End(a[^1].Curve), Start(b[0].Curve))); return direct.Item1 + direct.Item2 <= reverse.Item1 + reverse.Item2 ? direct : reverse; }
    private static double Length(IReadOnlyList<CurveRef> curves) => curves.Sum(x => CurveLength(x.Curve));
    private static double CurveLength(LineArcProfileCurve2D curve) => curve switch { LineArcLineSegment2D x => Math.Sqrt((x.End.X - x.Start.X) * (x.End.X - x.Start.X) + (x.End.Y - x.Start.Y) * (x.End.Y - x.Start.Y)), LineArcCircularArc2D x => Math.Abs(x.SweepAngleRadians) * x.Radius, LineArcFullCircle2D x => 2 * Math.PI * x.Radius, _ => 0 };
    private static SheetPoint2 PointAt(LineArcProfileCurve2D curve, double t) => curve switch { LineArcLineSegment2D x => new(x.Start.X + (x.End.X - x.Start.X) * t, x.Start.Y + (x.End.Y - x.Start.Y) * t), LineArcCircularArc2D x => new(x.Center.X + x.Radius * Math.Cos(x.StartAngleRadians + x.SweepAngleRadians * t), x.Center.Y + x.Radius * Math.Sin(x.StartAngleRadians + x.SweepAngleRadians * t)), LineArcFullCircle2D x => new(x.Center.X + x.Radius * Math.Cos(2 * Math.PI * t), x.Center.Y + x.Radius * Math.Sin(2 * Math.PI * t)), _ => default };
    private static SheetPoint2 Start(LineArcProfileCurve2D curve) => PointAt(curve, 0); private static SheetPoint2 End(LineArcProfileCurve2D curve) => PointAt(curve, 1);
    private static string Family(LineArcProfileCurve2D curve) => curve switch { LineArcLineSegment2D => "Line", LineArcCircularArc2D => "Arc", LineArcFullCircle2D => "Circle", _ => "Unsupported" };
    private static double Worst(SemanticLocalMetrics x) => new[] { x.SourceToNative.Maximum, x.NativeToSource.Maximum, x.StartEndpointResidual, x.EndEndpointResidual }.Where(double.IsFinite).DefaultIfEmpty(double.PositiveInfinity).Max();
    private static SheetPoint2 Center(IReadOnlyList<SheetPoint2> p) => p.Count == 0 ? default : new(p.Average(x => x.X), p.Average(x => x.Y));
    private static SheetPoint2 BoundsCenter(IReadOnlyList<SheetPoint2> p) => p.Count == 0 ? default : new((p.Min(x=>x.X)+p.Max(x=>x.X))/2,(p.Min(x=>x.Y)+p.Max(x=>x.Y))/2);
    private static SheetPoint2 Mid(SheetPoint2 a, SheetPoint2 b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);
    private static double Extent(IReadOnlyList<SheetPoint2> p, bool x) => p.Count == 0 ? 0 : x ? p.Max(q => q.X) - p.Min(q => q.X) : p.Max(q => q.Y) - p.Min(q => q.Y);
    private static double PathLength(IReadOnlyList<SheetPoint2> p) => Enumerable.Range(1, p.Count - 1).Sum(i => Distance(p[i - 1], p[i]));
    private static double Distance(SheetPoint2 a, SheetPoint2 b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static double Dot(SheetPoint2 a, SheetPoint2 b) => a.X * b.X + a.Y * b.Y;
    private static SheetPoint2 Normalize(SheetPoint2 p) { var l = Math.Sqrt(Dot(p, p)); return l <= 1e-20 ? new(1, 0) : new(p.X / l, p.Y / l); }
    private static double AxisDistance(SheetBendIr a,SheetBendIr b){var u=a.AxisDirection.TryNormalize(out var au)?au:a.AxisDirection;var v=b.AxisDirection.TryNormalize(out var bv)?bv:b.AxisDirection;var cross=u.Cross(v);return cross.TryNormalize(out var n)?Math.Abs((b.AxisOrigin-a.AxisOrigin).Dot(n)):((b.AxisOrigin-a.AxisOrigin).Cross(u)).Length;}
}
