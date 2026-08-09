using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public static class SteppedHoleExecutionArchitectureLab
{
    public static SteppedHoleExecutionArchitectureLabResult RunCanonicalSteppedScenario()
    {
        var diagnostics = new List<string> { "BREP-BOOLEAN-STACK-A3 started." };
        var plan = (HoleRecoveryPlan)new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildCanonicalStepped())).Plan!;
        var results = new List<SteppedArchitectureStrategyResult>
        {
            ExecuteRepeatedSubtract("repeated-subtract-small-medium-large", ["small","medium","large"]),
            ExecuteRepeatedSubtract("repeated-subtract-large-medium-small", ["large","medium","small"]),
            ExecuteRepeatedSubtract("repeated-subtract-medium-large-small", ["medium","large","small"]),
            ExecuteUnionTool(),
            new("n-level-builder-analysis", SteppedArchitectureStrategyStatus.Deferred, null, "n-level-builder-not-implemented", "analysis", ["Two-level builder exists; no N-level builder available in current API."], true, false, false, false, [], false, false, false, 0, 0, 0, 0, 0, "introduce internal N-level builder"),
            new("profile-stack-tool-builder-analysis", SteppedArchitectureStrategyStatus.Deferred, null, "profile-stack-tool-builder-missing", "analysis", ["ProfileStack has sufficient tiers; no reusable profile-stack tool builder exists."], true, false, false, false, [], false, false, false, 0, 0, 0, 0, 0, "build reusable axial profile-stack tool builder"),
            ExecuteDeferredBaseline(plan),
            ExecuteCounterboreBaseline(),
            ExecuteInvalidNonCoaxialControl()
        };

        return new(results, ChooseRecommendation(results), diagnostics);
    }

    private static SteppedArchitectureStrategyResult ExecuteRepeatedSubtract(string name, IReadOnlyList<string> order)
    {
        var box = BrepPrimitives.CreateBox(30, 30, 20); if (!box.IsSuccess) return Failed(name, "primitive-box-failed", "primitive", []);
        var body = box.Value;
        foreach (var step in order)
        {
            var tool = CreateTool(step); if (!tool.IsSuccess) return Failed(name, $"primitive-{step}-failed", "primitive", []);
            var sub = BrepBoolean.Subtract(body, tool.Value);
            if (!sub.IsSuccess || sub.Value is null) return Failed(name, $"subtract-{step}-failed", "boolean-subtract", sub.Diagnostics.Select(d => d.Code.ToString()).ToList());
            body = sub.Value;
        }
        return Success(name, body);
    }

    private static SteppedArchitectureStrategyResult ExecuteUnionTool()
    {
        var box = BrepPrimitives.CreateBox(30, 30, 20); var s = CreateTool("small"); var m = CreateTool("medium"); var l = CreateTool("large");
        if (!box.IsSuccess || !s.IsSuccess || !m.IsSuccess || !l.IsSuccess) return Failed("unioned-tool-single-subtract", "primitive-failed", "primitive", []);
        var u1 = BrepBoolean.Union(s.Value, m.Value); if (!u1.IsSuccess || u1.Value is null) return Failed("unioned-tool-single-subtract", "union-small-medium-failed", "boolean-union", u1.Diagnostics.Select(d => d.Code.ToString()).ToList());
        var u2 = BrepBoolean.Union(u1.Value, l.Value); if (!u2.IsSuccess || u2.Value is null) return Failed("unioned-tool-single-subtract", "union-tool-failed", "boolean-union", u2.Diagnostics.Select(d => d.Code.ToString()).ToList());
        var sub = BrepBoolean.Subtract(box.Value, u2.Value); if (!sub.IsSuccess || sub.Value is null) return Failed("unioned-tool-single-subtract", "subtract-tool-failed", "boolean-subtract", sub.Diagnostics.Select(d => d.Code.ToString()).ToList());
        return Success("unioned-tool-single-subtract", sub.Value);
    }

    private static SteppedArchitectureStrategyResult ExecuteDeferredBaseline(HoleRecoveryPlan plan)
    {
        var exec = HoleRecoveryExecutor.Execute(plan);
        return new("deferred-baseline-current-production", exec.Status == HoleRecoveryExecutionStatus.UnsupportedPlan ? SteppedArchitectureStrategyStatus.Deferred : SteppedArchitectureStrategyStatus.Failed, exec.Body, "stepped-execution-deferred", "executor", exec.Diagnostics, true, true, false, false, [], false, false, false, 0, 0, 0, 0, 0, "keep deferred");
    }

    private static SteppedArchitectureStrategyResult ExecuteCounterboreBaseline()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(new SdfSubtractNode(new SdfSubtractNode(new SdfBoxNode(30,30,20), new SdfCylinderNode(2,30)), new SdfTransformNode(new SdfCylinderNode(4,4), Transform3D.CreateTranslation(new Vector3D(0,0,-8))))));
        var exec = HoleRecoveryExecutor.Execute((HoleRecoveryPlan)eval.Plan!);
        return exec.Body is null ? Failed("counterbore-baseline", "counterbore-baseline-failed", "baseline", exec.Diagnostics.ToList()) : Success("counterbore-baseline", exec.Body);
    }

    private static SteppedArchitectureStrategyResult ExecuteInvalidNonCoaxialControl()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildNonCoaxialStepped()));
        var diags = eval.Diagnostics.Concat(eval.RejectionReasons).ToList();
        return new("invalid-control-non-coaxial", eval.Admissible ? SteppedArchitectureStrategyStatus.Failed : SteppedArchitectureStrategyStatus.Skipped, null, eval.Admissible ? "invalid-control-unexpected-admission" : "unsupported-non-coaxial", "admission", diags, eval.Admissible, false, false, false, [], false, false, false, 0, 0, 0, 0, 0, "keep guardrails");
    }

    private static string ChooseRecommendation(IReadOnlyList<SteppedArchitectureStrategyResult> results)
    {
        if (results.Any(r => r.Strategy == "unioned-tool-single-subtract" && r.Status == SteppedArchitectureStrategyStatus.Succeeded)) return "unioned-tool-production";
        if (results.Any(r => r.Strategy.Contains("repeated-subtract", StringComparison.Ordinal) && r.Status == SteppedArchitectureStrategyStatus.Succeeded)) return "repeated-subtract-production";
        var engine = new JudgmentEngine<int>();
        var pick = engine.Evaluate(1, [new("profile-stack-tool-builder-production", _ => true, _ => 0.9), new("n-level-builder-production", _ => true, _ => 0.8), new("keep-deferred", _ => true, _ => 0.1)]);
        return pick.Selection?.Candidate.Name ?? "keep-deferred";
    }

    private static Aetheris.Kernel.Core.Results.KernelResult<BrepBody> CreateTool(string step)
    {
        return step switch
        {
            "small" => BrepPrimitives.CreateCylinder(2, 30),
            "medium" => Translate(BrepPrimitives.CreateCylinder(3, 8), 6),
            "large" => Translate(BrepPrimitives.CreateCylinder(4, 4), 8),
            _ => BrepPrimitives.CreateCylinder(1, 1)
        };
    }

    private static Aetheris.Kernel.Core.Results.KernelResult<BrepBody> Translate(Aetheris.Kernel.Core.Results.KernelResult<BrepBody> source, double z)
    {
        if (!source.IsSuccess) return source;
        var t = new Vector3D(0,0,z);
        var body = source.Value;
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
        foreach (var sf in body.Geometry.Surfaces)
        {
            var gs = sf.Value;
            g.AddSurface(sf.Key, gs.Kind switch
            {
                Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Plane => Aetheris.Kernel.Core.Geometry.SurfaceGeometry.FromPlane(new Aetheris.Kernel.Core.Geometry.Surfaces.PlaneSurface(gs.Plane!.Value.Origin + t, gs.Plane.Value.Normal, gs.Plane.Value.UAxis)),
                Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Cylinder => Aetheris.Kernel.Core.Geometry.SurfaceGeometry.FromCylinder(new Aetheris.Kernel.Core.Geometry.Surfaces.CylinderSurface(gs.Cylinder!.Value.Origin + t, gs.Cylinder.Value.Axis, gs.Cylinder.Value.Radius, gs.Cylinder.Value.XAxis)),
                _ => gs
            });
        }
        return Aetheris.Kernel.Core.Results.KernelResult<BrepBody>.Success(new BrepBody(body.Topology, g, body.Bindings, copy, body.SafeBooleanComposition?.Translate(t)));
    }

    private static SteppedArchitectureStrategyResult Success(string name, BrepBody body)
    {
        var step = Step242Exporter.ExportBody(body);
        var text = step.IsSuccess ? step.Value : string.Empty;
        var markers = new[] { "ISO-10303-21", "MANIFOLD_SOLID_BREP", "ADVANCED_FACE", "CYLINDRICAL_SURFACE" }.Where(m => text.Contains(m, StringComparison.Ordinal)).ToArray();
        return new(name, SteppedArchitectureStrategyStatus.Succeeded, body, "none", "none", [], true, true, true, step.IsSuccess, markers, body.SafeBooleanComposition is not null, text.Contains("MANIFOLD_SOLID_BREP", StringComparison.Ordinal), text.Contains("BREP_WITH_VOIDS", StringComparison.Ordinal), body.Topology.Faces.Count(), body.Topology.Loops.Count(), body.Topology.Edges.Count(), body.Topology.Coedges.Count(), body.Topology.Vertices.Count(), "candidate");
    }

    private static SteppedArchitectureStrategyResult Failed(string name, string code, string stage, IReadOnlyList<string> diagnostics)
        => new(name, SteppedArchitectureStrategyStatus.Failed, null, code, stage, diagnostics, true, true, false, false, [], false, false, false, 0, 0, 0, 0, 0, "investigate blocker");

    private static SdfNode BuildCanonicalStepped() => new SdfSubtractNode(new SdfSubtractNode(new SdfSubtractNode(new SdfBoxNode(30,30,20), new SdfCylinderNode(2,30)), new SdfTransformNode(new SdfCylinderNode(3,8), Transform3D.CreateTranslation(new Vector3D(0,0,-6)))), new SdfTransformNode(new SdfCylinderNode(4,4), Transform3D.CreateTranslation(new Vector3D(0,0,-8))));
    private static SdfNode BuildNonCoaxialStepped() => new SdfSubtractNode(new SdfSubtractNode(new SdfSubtractNode(new SdfBoxNode(30,30,20), new SdfCylinderNode(2,30)), new SdfTransformNode(new SdfCylinderNode(3,8), Transform3D.CreateTranslation(new Vector3D(1,0,-6)))), new SdfTransformNode(new SdfCylinderNode(4,4), Transform3D.CreateTranslation(new Vector3D(0,0,-8))));
}
