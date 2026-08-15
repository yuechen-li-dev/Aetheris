using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Surfacing;

namespace Aetheris.SheetMetal;

public sealed record SheetMetalRecognitionOptions(
    double LinearTolerance = 0.01d,
    double AngularToleranceRadians = 0.001d,
    double MaximumThicknessFraction = 0.1d,
    SheetMetalFlattenPolicy? FlattenPolicy = null)
{
    public SheetMetalFlattenPolicy EffectiveFlattenPolicy => FlattenPolicy ?? SheetMetalFlattenPolicy.Default;
}

/// <summary>
/// Bounded recognizer for ordinary exact B-reps whose sheet skins are paired planes and
/// coaxial cylinders. Source topology remains authoritative; this produces an interpretation.
/// </summary>
public static class SheetMetalRecognizer
{
    private sealed record FaceFacts(
        FaceId Id,
        SurfaceGeometry Surface,
        bool SameSense,
        IReadOnlyList<Point3D> OuterBoundary,
        IReadOnlyList<IReadOnlyList<Point3D>> InnerBoundaries,
        double Area,
        Point3D Centroid,
        IReadOnlyList<EdgeId> Edges);

    private sealed record PairCandidate(string Family, FaceFacts A, FaceFacts B, double Separation, double Weight);
    private sealed record ThicknessCluster(double Nominal, IReadOnlyList<PairCandidate> Pairs, double Score, double Spread);

    public static SheetMetalRecognitionResult RecognizeStep(string path, SheetMetalRecognitionOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var importClock = Stopwatch.StartNew();
        var text = File.ReadAllText(fullPath);
        var import = Step242Importer.ImportBody(text);
        importClock.Stop();
        if (!import.IsSuccess || import.Value is null)
        {
            var diagnostics = import.Diagnostics.Select(d => new SheetMetalDiagnostic(
                "sheetmetal-step-import-failed", SheetMetalDiagnosticSeverity.Error, d.Message)).ToArray();
            return new(null, new(false, null, options?.LinearTolerance ?? 0.01d, [], [], [], []), diagnostics, importClock.Elapsed, TimeSpan.Zero);
        }

        var recognitionClock = Stopwatch.StartNew();
        var result = Recognize(import.Value, fullPath, options);
        recognitionClock.Stop();
        return result with { ImportTime = importClock.Elapsed, RecognitionTime = recognitionClock.Elapsed };
    }

    public static SheetMetalRecognitionResult Recognize(BrepBody body, string sourcePath = "BRep", SheetMetalRecognitionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(body);
        options ??= new();
        var facts = ExtractFacts(body);
        var thickness = RecognizeThickness(facts, options);
        var diagnostics = new List<SheetMetalDiagnostic>();
        if (!thickness.IsPlausible || thickness.NominalThickness is null)
        {
            diagnostics.Add(new(SheetMetalDiagnosticCodes.NonConstantThickness, SheetMetalDiagnosticSeverity.Error,
                "No tolerance-bounded nominal thickness was supported by opposing planar or coaxial cylindrical face pairs."));
            return new(null, thickness, diagnostics, TimeSpan.Zero, TimeSpan.Zero);
        }

        var t = thickness.NominalThickness.Value;
        var admitted = thickness.SourcePairs.Where(p => p.Admitted).ToArray();
        var planarPairs = SelectDisjointPairs(
            admitted.Where(p => p.Family == "Planar").Select(p => MakeCandidate(p, facts)).Where(p => p is not null).Cast<PairCandidate>(),
            thickness.Tolerance);
        var cylinderPairs = SelectDisjointPairs(
            admitted.Where(p => p.Family == "Cylindrical").Select(p => MakeCandidate(p, facts)).Where(p => p is not null).Cast<PairCandidate>(),
            thickness.Tolerance);

        var regions = new List<SheetRegionIr>();
        foreach (var pair in planarPairs.OrderBy(p => Math.Min(p.A.Id.Value, p.B.Id.Value)))
            regions.Add(CreatePlanarRegion(pair, t, sourcePath));
        foreach (var pair in cylinderPairs.OrderBy(p => Math.Min(p.A.Id.Value, p.B.Id.Value)))
            regions.Add(CreateCylindricalRegion(pair, t, sourcePath));

        var planarRegions = regions.Where(r => r.Kind == SheetRegionKind.Planar).ToArray();
        var cylinderRegions = regions.Where(r => r.Kind == SheetRegionKind.CylindricalBend).ToArray();
        var adjacency = FaceAdjacency(body);
        var bends = new List<SheetBendIr>();
        foreach (var bendRegion in cylinderRegions)
        {
            var adjacentRegions = planarRegions.Where(region =>
                bendRegion.Source.FaceIds.Any(cylinderFace => adjacency.TryGetValue(cylinderFace, out var adjacent)
                    && region.Source.FaceIds.Any(adjacent.Contains)))
                .OrderBy(region => region.StableId, StringComparer.Ordinal)
                .ToArray();
            if (adjacentRegions.Length != 2 || bendRegion.Cylinder is null)
            {
                diagnostics.Add(new(SheetMetalDiagnosticCodes.UnsupportedBendTopology, SheetMetalDiagnosticSeverity.Warning,
                    $"Cylindrical region '{bendRegion.StableId}' touches {adjacentRegions.Length} recovered planar regions; exactly two are required for a bend.", bendRegion.Source.FaceIds));
                continue;
            }

            var c = bendRegion.Cylinder;
            var direction = InferBendDirection(adjacentRegions[0], adjacentRegions[1], c.AxisDirection);
            bends.Add(new(
                bendRegion.StableId.Replace("region", "bend", StringComparison.Ordinal),
                c.AxisOrigin, CanonicalDirection(c.AxisDirection), c.AngularSpanRadians, c.InsideRadius, t, direction,
                adjacentRegions[0].StableId, adjacentRegions[1].StableId,
                SheetNeutralAxisPolicy.KFactorPolicy(options.EffectiveFlattenPolicy.KFactor),
                bendRegion.Source,
                [new(SheetEvidenceKind.ToleranceBounded, "planar-cylinder-planar", "Paired coaxial cylinder touches exactly two recovered planar skin pairs.", c.AngularSpanRadians, options.AngularToleranceRadians, bendRegion.Source.FaceIds)]));
        }

        var baseRegion = planarRegions.OrderByDescending(r => r.ApproximateArea).ThenBy(r => r.StableId, StringComparer.Ordinal).FirstOrDefault();
        if (baseRegion is null)
        {
            diagnostics.Add(new(SheetMetalDiagnosticCodes.UnsupportedBendTopology, SheetMetalDiagnosticSeverity.Error, "No planar base region could be recovered."));
            return new(null, thickness, diagnostics, TimeSpan.Zero, TimeSpan.Zero);
        }

        var features = RecoverFeatures(body, planarPairs, planarRegions, sourcePath, options.LinearTolerance);
        var connected = ConnectedRegionIds(baseRegion.StableId, bends);
        var disconnected = planarRegions.Where(r => !connected.Contains(r.StableId)).Select(r => r.StableId).ToArray();
        if (disconnected.Length > 0)
            diagnostics.Add(new(SheetMetalDiagnosticCodes.DisconnectedGraph, SheetMetalDiagnosticSeverity.Warning,
                $"{disconnected.Length} recovered planar regions are outside the base-region bend component and will not be flattened."));

        var pairedFaceIds = regions.SelectMany(r => r.Source.FaceIds).ToHashSet();
        var unsupported = facts.Where(f => !pairedFaceIds.Contains(f.Id.Value)).Select(f => f.Id.Value).Order().ToArray();
        if (unsupported.Length > 0)
            diagnostics.Add(new(SheetMetalDiagnosticCodes.UnpairedFaces, SheetMetalDiagnosticSeverity.Warning,
                $"{unsupported.Length} source faces are boundary/cut/support faces rather than recovered sheet reference skins.", unsupported));

        var status = bends.Count > 0 && disconnected.Length == 0
            ? (unsupported.Length == 0 ? SheetMetalRecognitionStatus.Complete : SheetMetalRecognitionStatus.Partial)
            : regions.Count > 0 ? SheetMetalRecognitionStatus.Partial : SheetMetalRecognitionStatus.Unsupported;
        var stableId = $"sheetmetal-import-{StableHash(sourcePath + "|" + t.ToString("R", System.Globalization.CultureInfo.InvariantCulture))[..16]}";
        var part = new SheetMetalPartIr(
            stableId, t, null, baseRegion.StableId, regions.OrderBy(r => r.StableId, StringComparer.Ordinal).ToArray(),
            bends.OrderBy(b => b.StableId, StringComparer.Ordinal).ToArray(), features, options.EffectiveFlattenPolicy, status,
            "Recognized imported BRep interpretation; source STEP/BRep remains formed-geometry authority.",
            [new(SheetEvidenceKind.DeterministicHeuristic, "base-region", "Largest recovered planar area, stable-ID tie break.", baseRegion.ApproximateArea, null, baseRegion.Source.FaceIds),
             new(SheetEvidenceKind.ToleranceBounded, "constant-thickness", "Dominant opposing-support cluster selected by JudgmentEngine.", t, options.LinearTolerance, thickness.SourcePairs.Where(p => p.Admitted).SelectMany(p => new[]{p.FaceA,p.FaceB}).Distinct().Order().ToArray())],
            diagnostics, body);
        return new(part, thickness, diagnostics, TimeSpan.Zero, TimeSpan.Zero);

        PairCandidate? MakeCandidate(SheetThicknessPairEvidence p, IReadOnlyList<FaceFacts> all)
        {
            var a = all.FirstOrDefault(f => f.Id.Value == p.FaceA);
            var b = all.FirstOrDefault(f => f.Id.Value == p.FaceB);
            return a is null || b is null ? null : new(p.Family, a, b, p.Separation, 1d);
        }
    }

    private static SheetThicknessRecognition RecognizeThickness(IReadOnlyList<FaceFacts> facts, SheetMetalRecognitionOptions options)
    {
        var bounds = facts.SelectMany(f => f.OuterBoundary).ToArray();
        var ranges = bounds.Length == 0 ? new[] { 100d } : new[]
        {
            bounds.Max(p => p.X)-bounds.Min(p => p.X), bounds.Max(p => p.Y)-bounds.Min(p => p.Y), bounds.Max(p => p.Z)-bounds.Min(p => p.Z)
        }.Where(v => v > options.LinearTolerance).ToArray();
        var maximumThickness = Math.Max(options.LinearTolerance * 2d, (ranges.Length == 0 ? 100d : ranges.Min()) * options.MaximumThicknessFraction);

        var candidates = new List<PairCandidate>();
        // Imported placements often carry harmless producer-specific direction noise. The
        // fixture-scale bounded scan (62 planes in CTC-03) avoids brittle quantized buckets;
        // the overlap predicate rejects unrelated parallel supports before clustering.
        var planeFaces=facts.Where(f=>f.Surface.Plane is not null).OrderBy(f=>f.Id.Value).ToArray();
        {
            var faces = planeFaces;
            for (var i = 0; i < faces.Length; i++) for (var j = i + 1; j < faces.Length; j++)
            {
                var a = faces[i]; var b = faces[j];
                if (!OpposingSense(a, b)) continue;
                var separation = Math.Abs((b.Surface.Plane!.Value.Origin-a.Surface.Plane!.Value.Origin).Dot(a.Surface.Plane.Value.Normal.ToVector()));
                if (separation <= options.LinearTolerance || separation > maximumThickness || !PlanarOverlap(a,b)) continue;
                candidates.Add(new("Planar", a, b, separation, 1d + Math.Log10(1d + Math.Min(a.Area,b.Area))));
            }
        }
        foreach (var group in facts.Where(f => f.Surface.Cylinder is not null).GroupBy(f => CylinderAxisKey(f.Surface.Cylinder!.Value, options.LinearTolerance)))
        {
            var faces = group.OrderBy(f => f.Id.Value).ToArray();
            for (var i = 0; i < faces.Length; i++) for (var j = i + 1; j < faces.Length; j++)
            {
                var a=faces[i]; var b=faces[j];
                if (!OpposingSense(a,b)) continue;
                var separation=Math.Abs(a.Surface.Cylinder!.Value.Radius-b.Surface.Cylinder!.Value.Radius);
                if(separation<=options.LinearTolerance||separation>maximumThickness||!AxialOverlap(a,b,a.Surface.Cylinder.Value))continue;
                candidates.Add(new("Cylindrical",a,b,separation,4d+Math.Log10(1d+Math.Min(a.Area,b.Area))));
            }
        }

        var clusters = candidates.GroupBy(c => Math.Round(c.Separation / options.LinearTolerance, MidpointRounding.AwayFromZero))
            .Select(group =>
            {
                var ordered=group.OrderBy(c=>c.Separation).ThenBy(c=>c.A.Id.Value).ThenBy(c=>c.B.Id.Value).ToArray();
                var nominal=WeightedMedian(ordered);
                var spread=ordered.Max(c=>Math.Abs(c.Separation-nominal));
                var score=ordered.Sum(c=>c.Weight)-spread/Math.Max(options.LinearTolerance,1e-12);
                return new ThicknessCluster(nominal,ordered,score,spread);
            }).OrderBy(c=>c.Nominal).ToArray();
        if(clusters.Length==0)return new(false,null,options.LinearTolerance,[],facts.Select(f=>f.Id.Value).Order().ToArray(),[],[]);

        var engine=new JudgmentEngine<ThicknessCluster[]>();
        var judgment=engine.Evaluate(clusters,clusters.Select((cluster,index)=>new JudgmentCandidate<ThicknessCluster[]>(
            cluster.Nominal.ToString("R",System.Globalization.CultureInfo.InvariantCulture),
            _=>cluster.Pairs.Count>0&&cluster.Spread<=options.LinearTolerance*1.5d,
            _=>cluster.Score,
            _=>$"Spread {cluster.Spread:G6} exceeds the bounded tolerance cluster.",index)).ToArray());
        if(!judgment.IsSuccess)return new(false,null,options.LinearTolerance,[],facts.Select(f=>f.Id.Value).Order().ToArray(),[],[]);
        var selected=clusters.Single(c=>c.Nominal.ToString("R",System.Globalization.CultureInfo.InvariantCulture)==judgment.Selection!.Value.Candidate.Name);
        var allEvidence=candidates.OrderBy(c=>c.Family).ThenBy(c=>c.A.Id.Value).ThenBy(c=>c.B.Id.Value)
            .Select(c=>new SheetThicknessPairEvidence(c.Family,c.A.Id.Value,c.B.Id.Value,c.Separation,Math.Abs(c.Separation-selected.Nominal),Math.Abs(c.Separation-selected.Nominal)<=options.LinearTolerance)).ToArray();
        var supported=allEvidence.Where(e=>e.Admitted).SelectMany(e=>new[]{e.FaceA,e.FaceB}).ToHashSet();
        return new(true,selected.Nominal,options.LinearTolerance,allEvidence,
            facts.Where(f=>!supported.Contains(f.Id.Value)).Select(f=>f.Id.Value).Order().ToArray(),[],
            [new(SheetEvidenceKind.ToleranceBounded,"nominal-thickness",$"JudgmentEngine selected dominant support cluster from {clusters.Length} bounded candidates.",selected.Nominal,options.LinearTolerance,supported.Order().ToArray())]);
    }

    private static IReadOnlyList<PairCandidate> SelectDisjointPairs(IEnumerable<PairCandidate> pairs, double tolerance)
    {
        var used=new HashSet<int>();var selected=new List<PairCandidate>();
        foreach(var pair in pairs.OrderBy(p=>Math.Abs(p.Separation)).ThenByDescending(p=>Math.Min(p.A.Area,p.B.Area)).ThenBy(p=>p.A.Id.Value).ThenBy(p=>p.B.Id.Value))
            if(used.Add(pair.A.Id.Value)){if(used.Add(pair.B.Id.Value))selected.Add(pair);else used.Remove(pair.A.Id.Value);}
        return selected;
    }

    private static SheetRegionIr CreatePlanarRegion(PairCandidate pair,double thickness,string sourcePath)
    {
        var plane=pair.A.Surface.Plane!.Value;var n=plane.Normal.ToVector();
        var toward=(pair.B.Centroid-pair.A.Centroid).Dot(n)>=0d?n:-n;
        var origin=plane.Origin+toward*(pair.Separation/2d);
        var u=CanonicalDirection(plane.UAxis.ToVector());var v=n.Cross(u);if(!v.TryNormalize(out v))v=plane.VAxis.ToVector();
        var boundary=pair.A.OuterBoundary.Select(p=>p+toward*((pair.Separation/2d)-(p-origin).Dot(toward))).ToArray();
        var ids=new[]{pair.A.Id.Value,pair.B.Id.Value}.Order().ToArray();
        return new($"region-p-{ids[0]:D4}-{ids[1]:D4}",SheetRegionKind.Planar,
            new(DevelopabilityKind.Developable,"analytic plane",0d,0,"Planes have zero Gaussian curvature."),
            new(origin,n,u,v,pair.A.SameSense),null,boundary,Math.Max(pair.A.Area,pair.B.Area),
            new("STEP/BRep faces","formed geometry evidence",ids,pair.A.Edges.Concat(pair.B.Edges).Select(e=>e.Value).Distinct().Order().ToArray(),sourcePath),
            [new(SheetEvidenceKind.ToleranceBounded,"parallel-plane-pair","Parallel opposing supports with overlapping projected bounds.",pair.Separation,thickness==0?null:Math.Abs(pair.Separation-thickness),ids),
             new(SheetEvidenceKind.Derived,"sheet-mid-surface","Midpoint plane; distinct from manufacturing neutral axis.",pair.Separation/2d,null,ids)]);
    }

    private static SheetRegionIr CreateCylindricalRegion(PairCandidate pair,double thickness,string sourcePath)
    {
        var ca=pair.A.Surface.Cylinder!.Value;var cb=pair.B.Surface.Cylinder!.Value;
        var inner=ca.Radius<=cb.Radius?ca:cb;var span=Math.Min(AngularSpan(pair.A,ca),AngularSpan(pair.B,cb));
        var axis=CanonicalDirection(inner.Axis.ToVector());var axial=pair.A.OuterBoundary.Concat(pair.B.OuterBoundary).Select(p=>(p-inner.Origin).Dot(axis)).ToArray();var axialMin=axial.Length==0?0:axial.Min();var axialMax=axial.Length==0?0:axial.Max();var length=axialMax-axialMin;var centeredAxisOrigin=inner.Origin+axis*((axialMin+axialMax)/2d);var ids=new[]{pair.A.Id.Value,pair.B.Id.Value}.Order().ToArray();
        return new($"region-c-{ids[0]:D4}-{ids[1]:D4}",SheetRegionKind.CylindricalBend,
            new(DevelopabilityKind.Developable,"analytic cylinder",0d,0,"Cylinders are analytically developable."),null,
            new(centeredAxisOrigin,axis,(ca.Radius+cb.Radius)/2d,inner.Radius,span,length,inner.Radius==ca.Radius?pair.A.SameSense:pair.B.SameSense),
            pair.A.OuterBoundary.Concat(pair.B.OuterBoundary).ToArray(),span*((ca.Radius+cb.Radius)/2d)*length,
            new("STEP/BRep faces","formed geometry evidence",ids,pair.A.Edges.Concat(pair.B.Edges).Select(e=>e.Value).Distinct().Order().ToArray(),sourcePath),
            [new(SheetEvidenceKind.ToleranceBounded,"coaxial-cylinder-pair","Coaxial opposing supports; radius difference agrees with nominal thickness.",Math.Abs(ca.Radius-cb.Radius),thickness==0?null:Math.Abs(Math.Abs(ca.Radius-cb.Radius)-thickness),ids),
             new(SheetEvidenceKind.Derived,"geometric-mid-cylinder","Arithmetic mid-radius; manufacturing neutral radius is policy-derived separately.",(ca.Radius+cb.Radius)/2d,null,ids)]);
    }

    private static IReadOnlyList<SheetFeatureIr> RecoverFeatures(BrepBody body,IReadOnlyList<PairCandidate> pairs,IReadOnlyList<SheetRegionIr> regions,string sourcePath,double tolerance)
    {
        var result=new List<SheetFeatureIr>();
        foreach(var pair in pairs)
        {
            var region=regions.FirstOrDefault(r=>r.Source.FaceIds.Contains(pair.A.Id.Value)&&r.Source.FaceIds.Contains(pair.B.Id.Value));
            if(region?.Plane is null)continue;
            foreach(var face in new[]{pair.A,pair.B})foreach(var loopId in body.GetLoopIds(face.Id).Skip(1))
            {
                var edges=body.GetCoedgeIds(loopId).Select(body.GetCoedgeEdgeId).ToArray();
                if(TryCircularLoop(body,edges,tolerance,out var circle))
                {
                    var center=ProjectToPlane(circle.Center,region.Plane);
                    var id=$"feature-hole-f{face.Id.Value:D4}-e{edges.Min(e=>e.Value):D4}";
                    if(result.Any(f=>(f.Center-center).Length<=tolerance&&Math.Abs((f.Diameter??0)-2d*circle.Radius)<=tolerance))continue;
                    result.Add(new(id,SheetFeatureKind.CircularHole,region.StableId,center,2d*circle.Radius,[],
                        new("STEP/BRep loop","formed cut-boundary evidence",[face.Id.Value],edges.Select(e=>e.Value).ToArray(),sourcePath),
                        [new(SheetEvidenceKind.Exact,"circular-inner-loop",edges.Length==1?"Single closed circular edge on a recovered planar sheet skin.":"Coaxial circular arcs form one closed circular inner loop on a recovered planar sheet skin.",2d*circle.Radius,null,[face.Id.Value])]));
                }
                else
                {
                    var boundary=edges.SelectMany(e=>body.GetVertices(e)).Distinct().Select(v=>body.TryGetVertexPoint(v,out var p)?(Point3D?)ProjectToPlane(p,region.Plane):null).Where(p=>p.HasValue).Select(p=>p!.Value).ToArray();
                    var center=Centroid(boundary);if(result.Any(f=>f.OwningRegionId==region.StableId&&(f.Center-center).Length<=tolerance))continue;
                    var kinds=edges.Select(e=>body.TryGetEdgeCurve(e,out var curve)?curve?.Kind:CurveGeometryKind.Unsupported).ToHashSet();var featureKind=kinds.Contains(CurveGeometryKind.Circle3)&&kinds.Contains(CurveGeometryKind.Line3)?SheetFeatureKind.Slot:SheetFeatureKind.ProfileHole;
                    if(boundary.Length>=3)result.Add(new($"feature-profile-f{face.Id.Value:D4}-l{loopId.Value:D4}",featureKind,region.StableId,center,null,boundary,
                        new("STEP/BRep loop","formed cut-boundary evidence",[face.Id.Value],edges.Select(e=>e.Value).ToArray(),sourcePath),
                        [new(SheetEvidenceKind.Exact,"planar-inner-loop","Closed inner boundary on a recovered planar sheet skin.",null,null,[face.Id.Value])]));
                }
            }
        }
        return result.OrderBy(f=>f.StableId,StringComparer.Ordinal).ToArray();
    }

    private static bool TryCircularLoop(BrepBody body,IReadOnlyList<EdgeId> edges,double tolerance,out Circle3Curve circle)
    {
        circle=default;if(edges.Count==0)return false;
        var circles=new List<Circle3Curve>(edges.Count);
        foreach(var edge in edges)
        {
            if(!body.TryGetEdgeCurve(edge,out var curve)||curve?.Circle3 is not Circle3Curve candidate)return false;
            circles.Add(candidate);
        }
        var first=circles[0];var axis=first.Normal.ToVector();
        if(circles.Skip(1).Any(x=>(x.Center-first.Center).Length>tolerance||Math.Abs(x.Radius-first.Radius)>tolerance||Math.Abs(Math.Abs(x.Normal.ToVector().Dot(axis))-1)>1e-8))return false;
        circle=first;return true;
    }

    private static IReadOnlyList<FaceFacts> ExtractFacts(BrepBody body)
    {
        var result=new List<FaceFacts>();
        foreach(var face in body.Topology.Faces.OrderBy(f=>f.Id.Value))
        {
            if(!body.TryGetFaceSurface(face.Id,out var surface)||surface is null)continue;
            var loops=face.LoopIds.Select(id=>LoopPoints(body,id)).Where(p=>p.Count>0).ToArray();
            if(loops.Length==0)continue;
            var measured=loops.Select(points=>(Points:points,Area:LoopArea(points,surface))).OrderByDescending(x=>x.Area).ToArray();
            var outer=measured[0].Points;var inner=measured.Skip(1).Select(x=>x.Points).ToArray();
            var sameSense=!body.Bindings.TryGetFaceBinding(face.Id,out var binding)||binding.SameSense;
            result.Add(new(face.Id,surface,sameSense,outer,inner,measured[0].Area,Centroid(outer),body.GetEdges(face.Id)));
        }
        return result;
    }

    private static IReadOnlyList<Point3D> LoopPoints(BrepBody body,LoopId loopId)
    {
        var points=new List<Point3D>();
        foreach(var coedgeId in body.GetCoedgeIds(loopId))
        {
            var coedge=body.Topology.GetCoedge(coedgeId);var edge=body.Topology.GetEdge(coedge.EdgeId);
            var vertex=coedge.IsReversed?edge.EndVertexId:edge.StartVertexId;
            if(body.TryGetVertexPoint(vertex,out var p))points.Add(p);
        }
        return points;
    }

    private static double LoopArea(IReadOnlyList<Point3D> points,SurfaceGeometry surface)
    {
        if(points.Count<3)return surface.Cylinder is { } c?AxisSpan(points,c)*c.Radius*Math.Max(AngularSpan(points,c),1e-9):0d;
        Vector3D u,v;
        if(surface.Plane is { } p){u=p.UAxis.ToVector();v=p.VAxis.ToVector();}
        else { var n=Newell(points);u=Perpendicular(n);v=n.Cross(u); }
        double area=0;for(var i=0;i<points.Count;i++){var a=points[i];var b=points[(i+1)%points.Count];area+=a.XYZDot(u)*b.XYZDot(v)-b.XYZDot(u)*a.XYZDot(v);}return Math.Abs(area)*.5d;
    }

    private static Dictionary<int,HashSet<int>> FaceAdjacency(BrepBody body)
    {
        var edgeFaces=new Dictionary<int,List<int>>();
        foreach(var face in body.Topology.Faces)foreach(var edge in body.GetEdges(face.Id)){if(!edgeFaces.TryGetValue(edge.Value,out var list))edgeFaces[edge.Value]=list=[];list.Add(face.Id.Value);}
        var map=body.Topology.Faces.ToDictionary(f=>f.Id.Value,_=>new HashSet<int>());
        foreach(var faces in edgeFaces.Values)foreach(var a in faces)foreach(var b in faces)if(a!=b)map[a].Add(b);
        return map;
    }

    private static bool OpposingSense(FaceFacts a,FaceFacts b)
    {
        // STEP producers legitimately encode the same material-facing orientation either by
        // reversing the plane placement or by ADVANCED_FACE.SAME_SENSE. Pairing therefore
        // treats parallel support as geometric evidence and retains orientation separately.
        if(a.Surface.Plane is { } pa&&b.Surface.Plane is { } pb)return Math.Abs(pa.Normal.ToVector().Dot(pb.Normal.ToVector()))>0.999d;
        return a.SameSense!=b.SameSense;
    }
    private static bool PlanarOverlap(FaceFacts a,FaceFacts b)
    {
        var p=a.Surface.Plane!.Value;var u=p.UAxis.ToVector();var v=p.VAxis.ToVector();
        var aa=Bounds2(a.OuterBoundary,u,v);var bb=Bounds2(b.OuterBoundary,u,v);var x=Math.Max(0,Math.Min(aa.maxX,bb.maxX)-Math.Max(aa.minX,bb.minX));var y=Math.Max(0,Math.Min(aa.maxY,bb.maxY)-Math.Max(aa.minY,bb.minY));
        return x*y>=.05d*Math.Max(1e-12,Math.Min((aa.maxX-aa.minX)*(aa.maxY-aa.minY),(bb.maxX-bb.minX)*(bb.maxY-bb.minY)));
    }
    private static bool AxialOverlap(FaceFacts a,FaceFacts b,CylinderSurface c){var axis=c.Axis.ToVector();var aa=a.OuterBoundary.Select(p=>(p-c.Origin).Dot(axis)).ToArray();var bb=b.OuterBoundary.Select(p=>(p-c.Origin).Dot(axis)).ToArray();return aa.Length>0&&bb.Length>0&&Math.Min(aa.Max(),bb.Max())-Math.Max(aa.Min(),bb.Min())>1e-6;}
    private static string PlaneOrientationKey(Vector3D n){n=CanonicalDirection(n);return $"{Math.Round(n.X,3)}|{Math.Round(n.Y,3)}|{Math.Round(n.Z,3)}";}
    private static string CylinderAxisKey(CylinderSurface c,double tol){var axis=CanonicalDirection(c.Axis.ToVector());var closest=c.Origin-axis*(c.Origin-Point3D.Origin).Dot(axis);double q(double x)=>Math.Round(x/Math.Max(tol,1e-6));return $"{Math.Round(axis.X,3)}|{Math.Round(axis.Y,3)}|{Math.Round(axis.Z,3)}|{q(closest.X)}|{q(closest.Y)}|{q(closest.Z)}";}
    private static double WeightedMedian(IReadOnlyList<PairCandidate> pairs){var ordered=pairs.OrderBy(p=>p.Separation).ToArray();var half=ordered.Sum(p=>p.Weight)/2d;double sum=0;foreach(var p in ordered){sum+=p.Weight;if(sum>=half)return p.Separation;}return ordered[^1].Separation;}
    private static (double minX,double minY,double maxX,double maxY) Bounds2(IEnumerable<Point3D> points,Vector3D u,Vector3D v){var p=points.Select(x=>(x.XYZDot(u),x.XYZDot(v))).ToArray();return(p.Min(x=>x.Item1),p.Min(x=>x.Item2),p.Max(x=>x.Item1),p.Max(x=>x.Item2));}
    private static double AngularSpan(FaceFacts face,CylinderSurface c)=>AngularSpan(face.OuterBoundary,c);
    private static double AngularSpan(IReadOnlyList<Point3D> points,CylinderSurface c){if(points.Count<2)return 2d*Math.PI;var angles=points.Select(p=>{var d=p-c.Origin;var axial=c.Axis.ToVector()*d.Dot(c.Axis.ToVector());var r=d-axial;var a=Math.Atan2(r.Dot(c.YAxis.ToVector()),r.Dot(c.XAxis.ToVector()));return a<0?a+2*Math.PI:a;}).DistinctBy(a=>Math.Round(a,9)).Order().ToArray();if(angles.Length<2)return 2d*Math.PI;var gap=0d;for(var i=0;i<angles.Length;i++){var next=i==angles.Length-1?angles[0]+2*Math.PI:angles[i+1];gap=Math.Max(gap,next-angles[i]);}return 2*Math.PI-gap;}
    private static double AxisSpan(FaceFacts f,CylinderSurface c)=>AxisSpan(f.OuterBoundary,c);
    private static double AxisSpan(IReadOnlyList<Point3D> points,CylinderSurface c){var p=points.Select(x=>(x-c.Origin).Dot(c.Axis.ToVector())).ToArray();return p.Length==0?0:p.Max()-p.Min();}
    private static SheetBendDirection InferBendDirection(SheetRegionIr a,SheetRegionIr b,Vector3D axis){if(a.Plane is null||b.Plane is null)return SheetBendDirection.Unknown;var sign=axis.Dot(a.Plane.Normal.Cross(b.Plane.Normal));return Math.Abs(sign)<1e-9?SheetBendDirection.Unknown:sign>0?SheetBendDirection.Up:SheetBendDirection.Down;}
    private static HashSet<string> ConnectedRegionIds(string root,IReadOnlyList<SheetBendIr> bends){var result=new HashSet<string>(StringComparer.Ordinal){root};var changed=true;while(changed){changed=false;foreach(var b in bends)if(result.Contains(b.AdjacentRegionA)&&result.Add(b.AdjacentRegionB)||result.Contains(b.AdjacentRegionB)&&result.Add(b.AdjacentRegionA))changed=true;}return result;}
    private static Point3D ProjectToPlane(Point3D p,SheetPlaneReference plane)=>p-plane.Normal*((p-plane.Origin).Dot(plane.Normal));
    private static Point3D Centroid(IReadOnlyList<Point3D> p)=>p.Count==0?Point3D.Origin:new(p.Average(x=>x.X),p.Average(x=>x.Y),p.Average(x=>x.Z));
    private static Vector3D Newell(IReadOnlyList<Point3D> p){var n=Vector3D.Zero;for(var i=0;i<p.Count;i++){var a=p[i];var b=p[(i+1)%p.Count];n+=new Vector3D((a.Y-b.Y)*(a.Z+b.Z),(a.Z-b.Z)*(a.X+b.X),(a.X-b.X)*(a.Y+b.Y));}return n.TryNormalize(out var q)?q:new(0,0,1);}
    private static Vector3D Perpendicular(Vector3D n){var refv=Math.Abs(n.X)<.8?new Vector3D(1,0,0):new Vector3D(0,1,0);var p=n.Cross(refv);return p.TryNormalize(out p)?p:new(0,0,1);}
    private static Vector3D CanonicalDirection(Vector3D v){if(!v.TryNormalize(out v))return v;return v.X< -1e-12||(Math.Abs(v.X)<=1e-12&&v.Y< -1e-12)||(Math.Abs(v.X)<=1e-12&&Math.Abs(v.Y)<=1e-12&&v.Z<0)?-v:v;}
    internal static string StableHash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static double XYZDot(this Point3D p,Vector3D v)=>p.X*v.X+p.Y*v.Y+p.Z*v.Z;
}
