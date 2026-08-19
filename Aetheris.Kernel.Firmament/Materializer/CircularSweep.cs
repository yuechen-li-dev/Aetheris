using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.StandardLibrary.Materials;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>Semantic AIR for X0's bounded constant circular section Sweep.</summary>
public sealed record CircularSweepFeatureAir(
    string Name,
    ResolvedConceptPath2D Path,
    double Diameter,
    string MaterialReference,
    ResolvedMaterial Material,
    double? MinimumGap,
    IReadOnlyList<string> Provenance)
{
    public double Radius => Diameter / 2d;
}

public sealed record CircularSweepBuildResult(
    CircularSweepFeatureAir Feature,
    BrepBody Body,
    double CenterlineLength,
    double Volume,
    double MassKilograms,
    IReadOnlyList<double> Bounds,
    IReadOnlyList<string> Diagnostics);

/// <summary>Parser/binder for the public Sweep declaration. Path geometry remains owned by Concept Path.</summary>
public static class CircularSweepAuthoring
{
    private static readonly Regex Declaration = new(@"\bSweep\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant);

    public static bool IsSweepSource(string source) => Declaration.IsMatch(source);

    public static KernelResult<CircularSweepFeatureAir> Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var declaration = Declaration.Match(source);
        if (!declaration.Success) return Failure("firmament-sweep-declaration-missing", "No Sweep declaration was found.");
        var close = MatchingBrace(source, source.IndexOf('{', declaration.Index));
        if (close < 0) return Failure("firmament-sweep-declaration-malformed", $"Sweep '{declaration.Groups["name"].Value}' has no closing brace.");
        var body = source[(source.IndexOf('{', declaration.Index) + 1)..close];
        var name = declaration.Groups["name"].Value;
        var pathName = Property(body, "Path");
        if (pathName is null) return Failure("firmament-sweep-path-missing", $"Sweep '{name}' requires Path.");
        var pathHeader = Regex.Match(source, $@"\bConcept\s+Path\s+{Regex.Escape(pathName)}(?:\s+On\s+(?<plane>[A-Za-z_]\w*))?\s*\{{", RegexOptions.CultureInvariant);
        if (pathHeader.Success && pathHeader.Groups["plane"].Success && !string.Equals(pathHeader.Groups["plane"].Value, "XY", StringComparison.Ordinal))
            return Failure("firmament-sweep-path-nonplanar", $"Sweep path '{pathName}' uses plane '{pathHeader.Groups["plane"].Value}'; X0 admits planar XY Concept Paths only.");
        if (pathHeader.Success)
        {
            var pathOpen = source.IndexOf('{', pathHeader.Index); var pathClose = MatchingBrace(source, pathOpen);
            if (pathClose > pathOpen && Regex.IsMatch(source[(pathOpen + 1)..pathClose], @"\b(?:Line|Arc)\s+[A-Za-z_]\w*\s*\{[^}]*\bFrom\s*:\s*Point2\s*\(", RegexOptions.CultureInvariant))
                return Failure("firmament-sweep-path-disconnected", $"Sweep path '{pathName}' contains an explicit segment origin; Concept Path segments must continue from the preceding endpoint.");
        }
        var diameterText = Property(body, "Diameter");
        if (!TryMillimeters(diameterText, out var diameter) || diameter <= 0d)
            return Failure("firmament-sweep-section-invalid", $"Sweep '{name}' Diameter must be a finite length greater than zero.");
        var materialReference = (Property(body, "Material") ?? "Standard.Materials.StainlessSteel.304_Annealed").Trim('"');
        var path = ProfileAuthoringParser.ResolveConceptPath(source, pathName, out var pathDiagnostics);
        if (path is null || pathDiagnostics.Count > 0)
            return Failure(pathDiagnostics.Count == 0 ? "firmament-sweep-path-unresolved" : pathDiagnostics[0],
                pathDiagnostics.Count == 0 ? $"Sweep '{name}' could not resolve Concept Path '{pathName}'." : PathMessage(pathDiagnostics[0]));
        var resolution = new MaterialResolver().Resolve(materialReference);
        if (!resolution.IsSuccess || resolution.Material is null)
            return Failure("firmament-sweep-material-unresolved", resolution.Message ?? $"Material '{materialReference}' could not be resolved.");
        double? minimumGap = null;
        var gapText = Property(body, "MinimumGap");
        if (gapText is not null)
        {
            if (!TryMillimeters(gapText, out var gap) || gap < 0d)
                return Failure("firmament-sweep-minimum-gap-invalid", $"Sweep '{name}' MinimumGap must be a finite non-negative length.");
            minimumGap = gap;
        }
        return KernelResult<CircularSweepFeatureAir>.Success(new(name, path, diameter, materialReference,
            resolution.Material, minimumGap, [path.StableId, "CircularSection", "PlanarXY", "ConstantSection"]));
    }

    private static string? Property(string body, string name)
    {
        var match = Regex.Match(body, $@"\b{Regex.Escape(name)}\s*:\s*(?<value>[^;\r\n}}]+)", RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    private static bool TryMillimeters(string? text, out double value)
    {
        value = default;
        if (text is null) return false;
        var match = Regex.Match(text, @"^(?<value>[-+.\deE]+)mm$", RegexOptions.CultureInvariant);
        return match.Success && double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    }

    private static int MatchingBrace(string source, int open)
    {
        var depth = 0;
        for (var i = open; i >= 0 && i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0) return i;
        }
        return -1;
    }

    private static string PathMessage(string diagnostic)
    {
        var parts = diagnostic.Split(':');
        return parts[0] switch
        {
            "concept-path-zero-length" => $"Sweep path segment '{parts.ElementAtOrDefault(2)}' has zero length.",
            "concept-path-arc-invalid" => $"Sweep path arc segment '{parts.ElementAtOrDefault(2)}' is degenerate or has invalid endpoints.",
            _ => diagnostic,
        };
    }

    private static KernelResult<CircularSweepFeatureAir> Failure(string code, string message) =>
        KernelResult<CircularSweepFeatureAir>.Failure([new(Core.Diagnostics.KernelDiagnosticCode.ValidationFailed,
            Core.Diagnostics.KernelDiagnosticSeverity.Error, $"{code}: {message}", "FirmamentV2.Sweep")]);
}

/// <summary>Validates and materializes an open planar line/arc tube as one analytic enclosed BRep.</summary>
public static class CircularSweepBRepMaterializer
{
    private static readonly ToleranceContext Tolerance = ToleranceContext.Default;

    public static KernelResult<CircularSweepBuildResult> Build(CircularSweepFeatureAir feature)
    {
        ArgumentNullException.ThrowIfNull(feature);
        var diagnostics = Validate(feature);
        if (diagnostics.Count > 0)
            return KernelResult<CircularSweepBuildResult>.Failure(diagnostics.Select(Error).ToArray());

        try
        {
            var segments = feature.Path.Segments;
            var builder = new TopologyBuilder();
            var geometry = new BrepGeometryStore();
            var bindings = new BrepBindingModel();
            var points = new Dictionary<VertexId, Point3D>();
            var ringVertices = new VertexId[segments.Count + 1];
            var ringEdges = new EdgeId[segments.Count + 1];
            var ringCenters = segments.Select(segment => Start(segment.Geometry)).Append(End(segments[^1].Geometry)).ToArray();
            var tangents = segments.Select(segment => StartTangent(segment.Geometry)).Append(EndTangent(segments[^1].Geometry)).ToArray();
            var z = Direction3D.Create(new Vector3D(0, 0, 1));
            var nextCurve = 1;
            var nextSurface = 1;

            for (var i = 0; i < ringCenters.Length; i++)
            {
                ringVertices[i] = builder.AddVertex();
                points[ringVertices[i]] = P(ringCenters[i], feature.Radius);
                ringEdges[i] = builder.AddEdge(ringVertices[i], ringVertices[i]);
                var normal = Direction3D.Create(new Vector3D(tangents[i].X, tangents[i].Y, 0));
                var curveId = new CurveGeometryId(nextCurve++);
                geometry.AddCurve(curveId, CurveGeometry.FromCircle(new Circle3Curve(P(ringCenters[i], 0), normal, feature.Radius, z)));
                bindings.AddEdgeBinding(new EdgeGeometryBinding(ringEdges[i], curveId, new ParameterInterval(0, 2 * Math.PI)));
            }

            var faces = new List<FaceId>();
            for (var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                var seam = builder.AddEdge(ringVertices[i], ringVertices[i + 1]);
                var seamCurve = new CurveGeometryId(nextCurve++);
                var surfaceId = new SurfaceGeometryId(nextSurface++);
                switch (segment.Geometry)
                {
                    case LineArcLineSegment2D line:
                    {
                        var a = P(line.Start, feature.Radius); var b = P(line.End, feature.Radius);
                        geometry.AddCurve(seamCurve, CurveGeometry.FromLine(new Line3Curve(a, Direction3D.Create(b - a))));
                        bindings.AddEdgeBinding(new EdgeGeometryBinding(seam, seamCurve, new ParameterInterval(0, (b - a).Length)));
                        geometry.AddSurface(surfaceId, SurfaceGeometry.FromCylinder(new CylinderSurface(P(line.Start, 0), Direction3D.Create(b - a), feature.Radius, z)));
                        break;
                    }
                    case LineArcCircularArc2D arc:
                    {
                        var radial = Direction3D.Create(new Vector3D(Math.Cos(arc.StartAngleRadians), Math.Sin(arc.StartAngleRadians), 0));
                        var sweepNormal = Direction3D.Create(new Vector3D(0, 0, Math.Sign(arc.SweepAngleRadians)));
                        geometry.AddCurve(seamCurve, CurveGeometry.FromCircle(new Circle3Curve(P(arc.Center, feature.Radius), sweepNormal, arc.Radius, radial)));
                        bindings.AddEdgeBinding(new EdgeGeometryBinding(seam, seamCurve, new ParameterInterval(0, Math.Abs(arc.SweepAngleRadians))));
                        geometry.AddSurface(surfaceId, SurfaceGeometry.FromTorus(new TorusSurface(P(arc.Center, 0), z, arc.Radius, feature.Radius, radial)));
                        break;
                    }
                    default: throw new InvalidOperationException("Sweep materializer received a non-line/arc path segment.");
                }
                var side = AddFaceWithLoop(builder, [(seam, false), (ringEdges[i + 1], false), (seam, true), (ringEdges[i], true)]);
                bindings.AddFaceBinding(new FaceGeometryBinding(side, surfaceId));
                faces.Add(side);
            }

            var startSurface = new SurfaceGeometryId(nextSurface++);
            geometry.AddSurface(startSurface, SurfaceGeometry.FromPlane(new PlaneSurface(P(ringCenters[0], 0),
                Direction3D.Create(new Vector3D(-tangents[0].X, -tangents[0].Y, 0)), z)));
            var startFace = AddFaceWithLoop(builder, [(ringEdges[0], false)]);
            bindings.AddFaceBinding(new FaceGeometryBinding(startFace, startSurface));
            faces.Add(startFace);

            var endSurface = new SurfaceGeometryId(nextSurface++);
            geometry.AddSurface(endSurface, SurfaceGeometry.FromPlane(new PlaneSurface(P(ringCenters[^1], 0),
                Direction3D.Create(new Vector3D(tangents[^1].X, tangents[^1].Y, 0)), z)));
            var endFace = AddFaceWithLoop(builder, [(ringEdges[^1], true)]);
            bindings.AddFaceBinding(new FaceGeometryBinding(endFace, endSurface));
            faces.Add(endFace);

            var shell = builder.AddShell(faces); builder.AddBody([shell]);
            var body = new BrepBody(builder.Model, geometry, bindings, points);
            var bindingValidation = BrepBindingValidator.Validate(body, true);
            if (!bindingValidation.IsSuccess) return KernelResult<CircularSweepBuildResult>.Failure(bindingValidation.Diagnostics);
            var preflight = BrepExportPreflight.Validate(body);
            if (!preflight.IsValid) return KernelResult<CircularSweepBuildResult>.Failure(preflight.Diagnostics.Where(d => d.Severity == BrepExportPreflightSeverity.Error)
                .Select(d => Error($"firmament-sweep-brep-invalid: {d.Code}: {d.Message}")).ToArray());

            var length = segments.Sum(segment => Length(segment.Geometry));
            var volume = Math.PI * feature.Radius * feature.Radius * length;
            var density = feature.Material.Structural!.Density.SiValue;
            var mass = volume * 1e-9 * density;
            var samples = segments.SelectMany(segment => Sample(segment.Geometry, 64)).ToArray();
            var bounds = new[] { samples.Min(p => p.X) - feature.Radius, samples.Min(p => p.Y) - feature.Radius, -feature.Radius,
                samples.Max(p => p.X) + feature.Radius, samples.Max(p => p.Y) + feature.Radius, feature.Radius };
            var evidence = new[] { "firmament-sweep-path-continuous", "firmament-sweep-path-tangent", "firmament-sweep-self-intersection-clear",
                "firmament-sweep-analytic-brep", "firmament-sweep-open-path-capped" };
            return KernelResult<CircularSweepBuildResult>.Success(new(feature, body, length, volume, mass, bounds, evidence));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return KernelResult<CircularSweepBuildResult>.Failure([Error("firmament-sweep-brep-construction-failed: " + exception.Message)]);
        }
    }

    public static IReadOnlyList<string> Validate(CircularSweepFeatureAir feature)
    {
        var result = new List<string>();
        if (!double.IsFinite(feature.Diameter) || feature.Diameter <= Tolerance.Linear)
            result.Add("firmament-sweep-section-invalid: Diameter must be finite and greater than zero.");
        if (feature.Path.Segments.Count == 0) result.Add("firmament-sweep-path-empty: Sweep path contains no segments.");
        for (var i = 0; i < feature.Path.Segments.Count; i++)
        {
            var segment = feature.Path.Segments[i];
            if (segment.Geometry is not (LineArcLineSegment2D or LineArcCircularArc2D))
                result.Add($"firmament-sweep-path-unsupported-segment: segment {i + 1} '{segment.Name}' is not Line or Arc.");
            if (Length(segment.Geometry) <= Tolerance.Linear)
                result.Add($"firmament-sweep-path-degenerate: segment {i + 1} '{segment.Name}' has zero length.");
            if (segment.Geometry is LineArcCircularArc2D arc && arc.Radius <= feature.Radius + Tolerance.Linear)
                result.Add($"firmament-sweep-bend-radius-too-small: segment {i + 1} '{segment.Name}' bend radius {arc.Radius:G6} mm must exceed section radius {feature.Radius:G6} mm.");
            if (i == 0) continue;
            var previous = feature.Path.Segments[i - 1];
            if (Distance(End(previous.Geometry), Start(segment.Geometry)) > Tolerance.Linear)
                result.Add($"firmament-sweep-path-disconnected: Sweep path is disconnected between segment {i} '{previous.Name}' and segment {i + 1} '{segment.Name}'.");
            var dot = Dot(EndTangent(previous.Geometry), StartTangent(segment.Geometry));
            if (dot < 1d - Tolerance.Angular)
                result.Add($"firmament-sweep-path-not-tangent: segment {i} '{previous.Name}' and segment {i + 1} '{segment.Name}' meet at a sharp corner.");
        }
        var clearance = feature.Diameter + (feature.MinimumGap ?? 0d);
        for (var i = 0; i < feature.Path.Segments.Count; i++)
            for (var j = i + 2; j < feature.Path.Segments.Count; j++)
            {
                var distance = ConservativeDistance(feature.Path.Segments[i].Geometry, feature.Path.Segments[j].Geometry);
                if (distance + Tolerance.Linear < clearance)
                    result.Add($"firmament-sweep-self-intersection: nonadjacent segments {i + 1} '{feature.Path.Segments[i].Name}' and {j + 1} '{feature.Path.Segments[j].Name}' are {distance:G6} mm apart; required centerline clearance is {clearance:G6} mm.");
            }
        return result;
    }

    // X0's bounded clearance test uses exact line/line distance and a deterministic conservative
    // chord witness for arc participation. It intentionally claims obvious-overlap coverage, not
    // a complete general curve/curve separation proof.
    private static double ConservativeDistance(LineArcProfileCurve2D a, LineArcProfileCurve2D b)
    {
        if (a is LineArcLineSegment2D la && b is LineArcLineSegment2D lb) return SegmentDistance(la.Start, la.End, lb.Start, lb.End);
        var sa = Sample(a, 96); var sb = Sample(b, 96); var best = double.PositiveInfinity;
        for (var i = 0; i < sa.Count - 1; i++) for (var j = 0; j < sb.Count - 1; j++)
            best = Math.Min(best, SegmentDistance(sa[i], sa[i + 1], sb[j], sb[j + 1]));
        return best;
    }

    private static double SegmentDistance((double X,double Y) a, (double X,double Y) b, (double X,double Y) c, (double X,double Y) d)
    {
        if (Intersects(a,b,c,d)) return 0;
        return Math.Min(Math.Min(PointSegment(a,c,d), PointSegment(b,c,d)), Math.Min(PointSegment(c,a,b), PointSegment(d,a,b)));
    }
    private static bool Intersects((double X,double Y) a,(double X,double Y)b,(double X,double Y)c,(double X,double Y)d)
    { static double O((double X,double Y)p,(double X,double Y)q,(double X,double Y)r)=>(q.X-p.X)*(r.Y-p.Y)-(q.Y-p.Y)*(r.X-p.X); return O(a,b,c)*O(a,b,d)<=0&&O(c,d,a)*O(c,d,b)<=0; }
    private static double PointSegment((double X,double Y) p,(double X,double Y)a,(double X,double Y)b)
    { var dx=b.X-a.X;var dy=b.Y-a.Y;var l=dx*dx+dy*dy;if(l<=0)return Distance(p,a);var t=Math.Clamp(((p.X-a.X)*dx+(p.Y-a.Y)*dy)/l,0,1);return Distance(p,(a.X+t*dx,a.Y+t*dy)); }
    private static IReadOnlyList<(double X,double Y)> Sample(LineArcProfileCurve2D curve,int count)=>curve switch
    { LineArcLineSegment2D l=>[l.Start,l.End], LineArcCircularArc2D a=>Enumerable.Range(0,count+1).Select(i=>{var t=a.StartAngleRadians+a.SweepAngleRadians*i/count;return(a.Center.X+a.Radius*Math.Cos(t),a.Center.Y+a.Radius*Math.Sin(t));}).ToArray(), _=>[] };
    private static double Length(LineArcProfileCurve2D c)=>c switch { LineArcLineSegment2D l=>Distance(l.Start,l.End),LineArcCircularArc2D a=>a.Radius*Math.Abs(a.SweepAngleRadians),_=>0 };
    private static (double X,double Y) Start(LineArcProfileCurve2D c)=>c switch { LineArcLineSegment2D l=>l.Start,LineArcCircularArc2D a=>(a.Center.X+a.Radius*Math.Cos(a.StartAngleRadians),a.Center.Y+a.Radius*Math.Sin(a.StartAngleRadians)),_=>default };
    private static (double X,double Y) End(LineArcProfileCurve2D c)=>c switch { LineArcLineSegment2D l=>l.End,LineArcCircularArc2D a=>(a.Center.X+a.Radius*Math.Cos(a.StartAngleRadians+a.SweepAngleRadians),a.Center.Y+a.Radius*Math.Sin(a.StartAngleRadians+a.SweepAngleRadians)),_=>default };
    private static (double X,double Y) StartTangent(LineArcProfileCurve2D c)=>Tangent(c,false);
    private static (double X,double Y) EndTangent(LineArcProfileCurve2D c)=>Tangent(c,true);
    private static (double X,double Y) Tangent(LineArcProfileCurve2D c,bool end){var v=c switch{LineArcLineSegment2D l=>(l.End.X-l.Start.X,l.End.Y-l.Start.Y),LineArcCircularArc2D a=>ArcTangent(a,end),_=>(0d,0d)};var n=Math.Sqrt(v.Item1*v.Item1+v.Item2*v.Item2);return(v.Item1/n,v.Item2/n);}
    private static (double,double) ArcTangent(LineArcCircularArc2D a,bool end){var t=a.StartAngleRadians+(end?a.SweepAngleRadians:0);var s=Math.Sign(a.SweepAngleRadians);return(-Math.Sin(t)*s,Math.Cos(t)*s);}
    private static double Dot((double X,double Y)a,(double X,double Y)b)=>a.X*b.X+a.Y*b.Y;
    private static double Distance((double X,double Y)a,(double X,double Y)b)=>Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));
    private static Point3D P((double X,double Y)p,double z)=>new(p.X,p.Y,z);
    private static FaceId AddFaceWithLoop(TopologyBuilder builder,IReadOnlyList<(EdgeId Edge,bool Reversed)> uses){var loop=builder.AllocateLoopId();var ids=uses.Select(_=>builder.AllocateCoedgeId()).ToArray();for(var i=0;i<uses.Count;i++)builder.AddCoedge(new Coedge(ids[i],uses[i].Edge,loop,ids[(i+1)%ids.Length],ids[(i+ids.Length-1)%ids.Length],uses[i].Reversed));builder.AddLoop(new Loop(loop,ids));return builder.AddFace([loop]);}
    private static Core.Diagnostics.KernelDiagnostic Error(string message)=>new(Core.Diagnostics.KernelDiagnosticCode.ValidationFailed,Core.Diagnostics.KernelDiagnosticSeverity.Error,message,"FirmamentV2.Sweep");
}
