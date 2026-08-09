using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public static class GenericCirBrepExecutorLab
{
    public static GenericCirBrepScenarioReport RunScenarioMatrix()
    {
        var scenarios = new[]
        {
            Execute("A-ThroughHole", BuildThroughHole()),
            Execute("B-BlindHole", BuildBlindHole()),
            Execute("C-Counterbore", BuildCounterbore()),
            Execute("D-Countersink", BuildCountersink()),
            Execute("E-SteppedHole", BuildStepped()),
            Execute("F-BoxMinusSphere", new SdfSubtractNode(new SdfBoxNode(20,20,20), new SdfSphereNode(4))),
            Execute("F-BoxMinusTorus", new SdfSubtractNode(new SdfBoxNode(20,20,20), new SdfTorusNode(5,2))),
            Execute("F-UnsupportedTransform", new SdfTransformNode(new SdfBoxNode(10,10,10), Transform3D.CreateRotationZ(double.Pi/4d))),
        };

        return new GenericCirBrepScenarioReport(scenarios, ["CIR-BREP-X8 matrix executed."]);
    }

    public static GenericCirBrepExecutorLabResult Execute(string scenario, SdfNode node)
    {
        var diagnostics = new List<string>();
        var bools = new List<string>();
        var result = TryExecuteNode(node, diagnostics, bools);
        if (!result.IsSuccess || result.Body is null)
        {
            return new(scenario, result.IsUnsupported ? GenericCirBrepLabStatus.Unsupported : GenericCirBrepLabStatus.Failed, null, result.FailureCode, diagnostics, bools, false, false, [], false, "n/a", 0);
        }

        var step = Step242Exporter.ExportBody(result.Body);
        var stepText = step.IsSuccess ? step.Value : string.Empty;
        var markers = new List<string>
        {
            stepText.Contains("ISO-10303-21", StringComparison.Ordinal) ? "ISO-10303-21" : "missing:ISO-10303-21",
            stepText.Contains("ADVANCED_FACE", StringComparison.Ordinal) ? "ADVANCED_FACE" : "missing:ADVANCED_FACE",
            stepText.Contains("MANIFOLD_SOLID_BREP", StringComparison.Ordinal) ? "MANIFOLD_SOLID_BREP" : (stepText.Contains("BREP_WITH_VOIDS", StringComparison.Ordinal) ? "BREP_WITH_VOIDS" : "missing:solid-root"),
            stepText.Contains("CYLINDRICAL_SURFACE", StringComparison.Ordinal) ? "CYLINDRICAL_SURFACE" : "missing:CYLINDRICAL_SURFACE",
            stepText.Contains("CONICAL_SURFACE", StringComparison.Ordinal) ? "CONICAL_SURFACE" : "missing:CONICAL_SURFACE",
        };

        return new(scenario, GenericCirBrepLabStatus.Succeeded, result.Body, "none", diagnostics, bools, true, step.IsSuccess, markers, result.Body.SafeBooleanComposition is not null, stepText.Contains("MANIFOLD_SOLID_BREP", StringComparison.Ordinal) ? "MANIFOLD_SOLID_BREP" : "BREP_WITH_VOIDS", result.Body.Topology.Faces.Count());
    }

    private static (bool IsSuccess, bool IsUnsupported, BrepBody? Body, string FailureCode) TryExecuteNode(SdfNode node, List<string> diagnostics, List<string> bools)
    {
        switch (node)
        {
            case SdfBoxNode box:
                var b = BrepPrimitives.CreateBox(box.Width, box.Height, box.Depth);
                return b.IsSuccess ? (true, false, b.Value, "none") : (false, false, null, "primitive-box-failed");
            case SdfCylinderNode cyl:
                var c = BrepPrimitives.CreateCylinder(cyl.Radius, cyl.Height);
                return c.IsSuccess ? (true, false, c.Value, "none") : (false, false, null, "primitive-cylinder-failed");
            case SdfConeNode cone:
                diagnostics.Add("Cone primitive mapping unavailable via public BrepPrimitives API in lab scope.");
                return (false, true, null, "primitive-cone-unsupported");
            case SdfSphereNode sph:
                var sp = BrepPrimitives.CreateSphere(sph.Radius);
                return sp.IsSuccess ? (true, false, sp.Value, "none") : (false, true, null, "primitive-sphere-unsupported");
            case SdfTorusNode tor:
                var tr = BrepPrimitives.CreateTorus(tor.MajorRadius, tor.MinorRadius);
                return tr.IsSuccess ? (true, false, tr.Value, "none") : (false, true, null, "primitive-torus-unsupported");
            case SdfTransformNode tx:
                if (!TryExtractTranslation(tx.Transform, out var t)) return (false, true, null, "transform-non-translation-unsupported");
                var child = TryExecuteNode(tx.Child, diagnostics, bools);
                if (!child.IsSuccess || child.Body is null) return child;
                return (true, false, t == Vector3D.Zero ? child.Body : Translate(child.Body, t), "none");
            case SdfSubtractNode sub:
                return ExecBool(sub.Left, sub.Right, BrepBoolean.Subtract, "Subtract", diagnostics, bools);
            case SdfUnionNode un:
                return ExecBool(un.Left, un.Right, BrepBoolean.Union, "Union", diagnostics, bools);
            case SdfIntersectNode it:
                return ExecBool(it.Left, it.Right, BrepBoolean.Intersect, "Intersect", diagnostics, bools);
            default:
                return (false, true, null, "node-kind-unsupported");
        }
    }

    private static (bool IsSuccess, bool IsUnsupported, BrepBody? Body, string FailureCode) ExecBool(SdfNode left, SdfNode right, Func<BrepBody, BrepBody, Aetheris.Kernel.Core.Results.KernelResult<BrepBody>> op, string label, List<string> diagnostics, List<string> bools)
    {
        var l = TryExecuteNode(left, diagnostics, bools);
        if (!l.IsSuccess || l.Body is null) return l;
        var r = TryExecuteNode(right, diagnostics, bools);
        if (!r.IsSuccess || r.Body is null) return r;
        bools.Add(label);
        var br = op(l.Body, r.Body);
        if (!br.IsSuccess || br.Value is null)
        {
            diagnostics.AddRange(br.Diagnostics.Select(d => d.Code.ToString()));
            return (false, false, null, $"boolean-{label.ToLowerInvariant()}-failed");
        }
        diagnostics.Add($"{label} success; safeComposition={(br.Value.SafeBooleanComposition is not null)}");
        return (true, false, br.Value, "none");
    }

    private static BrepBody Translate(BrepBody body, Vector3D t)
    {
        var copy = new Dictionary<Aetheris.Kernel.Core.Topology.VertexId, Point3D>();
        foreach (var v in body.Topology.Vertices)
        {
            if (body.TryGetVertexPoint(v.Id, out var point)) copy[v.Id] = point + t;
        }
        var g = new Aetheris.Kernel.Core.Brep.BrepGeometryStore();
        foreach (var c in body.Geometry.Curves)
        {
            var gc = c.Value;
            g.AddCurve(c.Key, gc.Kind switch
            {
                Aetheris.Kernel.Core.Geometry.CurveGeometryKind.Line3 => Aetheris.Kernel.Core.Geometry.CurveGeometry.FromLine(new Aetheris.Kernel.Core.Geometry.Curves.Line3Curve(gc.Line3!.Value.Origin + t, gc.Line3.Value.Direction)),
                Aetheris.Kernel.Core.Geometry.CurveGeometryKind.Circle3 => Aetheris.Kernel.Core.Geometry.CurveGeometry.FromCircle(new Aetheris.Kernel.Core.Geometry.Curves.Circle3Curve(gc.Circle3!.Value.Center + t, gc.Circle3.Value.Normal, gc.Circle3.Value.Radius, gc.Circle3.Value.XAxis)),
                _ => gc
            });
        }
        foreach (var s in body.Geometry.Surfaces)
        {
            var gs = s.Value;
            g.AddSurface(s.Key, gs.Kind switch
            {
                Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Plane => Aetheris.Kernel.Core.Geometry.SurfaceGeometry.FromPlane(new Aetheris.Kernel.Core.Geometry.Surfaces.PlaneSurface(gs.Plane!.Value.Origin + t, gs.Plane.Value.Normal, gs.Plane.Value.UAxis)),
                Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Cylinder => Aetheris.Kernel.Core.Geometry.SurfaceGeometry.FromCylinder(new Aetheris.Kernel.Core.Geometry.Surfaces.CylinderSurface(gs.Cylinder!.Value.Origin + t, gs.Cylinder.Value.Axis, gs.Cylinder.Value.Radius, gs.Cylinder.Value.XAxis)),
                Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Cone => Aetheris.Kernel.Core.Geometry.SurfaceGeometry.FromCone(new Aetheris.Kernel.Core.Geometry.Surfaces.ConeSurface(gs.Cone!.Value.PlacementOrigin + t, gs.Cone.Value.Axis, gs.Cone.Value.PlacementRadius, gs.Cone.Value.SemiAngleRadians, gs.Cone.Value.ReferenceAxis)),
                Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Torus => Aetheris.Kernel.Core.Geometry.SurfaceGeometry.FromTorus(new Aetheris.Kernel.Core.Geometry.Surfaces.TorusSurface(gs.Torus!.Value.Center + t, gs.Torus.Value.Axis, gs.Torus.Value.MajorRadius, gs.Torus.Value.MinorRadius, gs.Torus.Value.XAxis)),
                Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Sphere => Aetheris.Kernel.Core.Geometry.SurfaceGeometry.FromSphere(new Aetheris.Kernel.Core.Geometry.Surfaces.SphereSurface(gs.Sphere!.Value.Center + t, gs.Sphere.Value.Axis, gs.Sphere.Value.Radius, gs.Sphere.Value.XAxis)),
                _ => gs
            });
        }
        return new BrepBody(body.Topology, g, body.Bindings, copy, body.SafeBooleanComposition?.Translate(t));
    }

    private static bool TryExtractTranslation(Transform3D transform, out Vector3D translation)
    {
        var o = transform.Apply(Point3D.Origin); var x = transform.Apply(new Point3D(1, 0, 0)); var y = transform.Apply(new Point3D(0, 1, 0)); var z = transform.Apply(new Point3D(0, 0, 1));
        const double eps = 1e-9;
        if (Math.Abs((x - o).X - 1d) > eps || Math.Abs((x - o).Y) > eps || Math.Abs((x - o).Z) > eps
            || Math.Abs((y - o).Y - 1d) > eps || Math.Abs((z - o).Z - 1d) > eps)
        { translation = Vector3D.Zero; return false; }
        translation = o - Point3D.Origin;
        return true;
    }

    private static SdfNode BuildThroughHole() => new SdfSubtractNode(new SdfBoxNode(30, 30, 20), new SdfCylinderNode(4, 30));
    private static SdfNode BuildBlindHole() => new SdfSubtractNode(new SdfBoxNode(30, 30, 20), new SdfTransformNode(new SdfCylinderNode(4, 8), Transform3D.CreateTranslation(new Vector3D(0,0,6))));
    private static SdfNode BuildCounterbore() => new SdfSubtractNode(new SdfSubtractNode(new SdfBoxNode(30,30,20), new SdfCylinderNode(3,30)), new SdfTransformNode(new SdfCylinderNode(5,4), Transform3D.CreateTranslation(new Vector3D(0,0,8))));
    private static SdfNode BuildCountersink() => new SdfSubtractNode(new SdfSubtractNode(new SdfBoxNode(30,30,20), new SdfCylinderNode(3,30)), new SdfTransformNode(new SdfConeNode(5,3,4), Transform3D.CreateTranslation(new Vector3D(0,0,8))));
    private static SdfNode BuildStepped() => new SdfSubtractNode(new SdfSubtractNode(new SdfSubtractNode(new SdfBoxNode(30,30,20), new SdfCylinderNode(2,30)), new SdfTransformNode(new SdfCylinderNode(3,8), Transform3D.CreateTranslation(new Vector3D(0,0,6)))), new SdfTransformNode(new SdfCylinderNode(4,4), Transform3D.CreateTranslation(new Vector3D(0,0,8))));

    public static HoleRecoveryExecutionResult RunSemantic(HoleRecoveryPlan plan) => HoleRecoveryExecutor.Execute(plan);
}
