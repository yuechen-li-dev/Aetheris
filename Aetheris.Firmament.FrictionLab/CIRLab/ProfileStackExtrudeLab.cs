using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public sealed record ProfileStackLayer(double ZMin, double ZMax, double? InnerCircleRadius, string RoleName);
public sealed record ProfileStackExtrudeSpec(double Width, double Depth, double ZMin, double ZMax, IReadOnlyList<ProfileStackLayer> Layers, string ScenarioName);
public sealed record ProfileStackScenarioResult(string Scenario, bool Success, string Status, BrepBody? Body, IReadOnlyList<string> Diagnostics, IReadOnlyList<string> SemanticRoles, IReadOnlyList<string> StepMarkers, bool HasBrepWithVoids, int FaceCount);
public sealed record ProfileStackExtrudeLabResult(IReadOnlyList<ProfileStackScenarioResult> Scenarios, string Recommendation, IReadOnlyList<string> BoundaryNotes);

public static class ProfileStackExtrudeLab
{
    public static ProfileStackExtrudeLabResult Run()
    {
        var scenarios = new[] { BuildStepped(), BuildThrough(), BuildBlind(), BuildCounterbore() }.Select(RunScenario).ToArray();
        return new(scenarios, scenarios.Any(s => s.Success) ? "profile-stack-extrude-feasible-for-coaxial-z-hole-family" : "blocked-needs-topology-builder", [
            "applies: axis-aligned coaxial hole families (through/blind/counterbore/stepped).",
            "possible extension: multi-hole and slot/keyway via 2D region stacks.",
            "does-not-solve: oblique/cross-axis interactions, non-sweep topology, generalized 3D intersections.",
            "fallback: retain 3D boolean for non-sweepable or interacting solids."
        ]);
    }

    private static ProfileStackScenarioResult RunScenario(ProfileStackExtrudeSpec spec)
    {
        var diagnostics = new List<string> { $"scenario:{spec.ScenarioName}" };
        if (!TryBuildComposition(spec, out var composition, out var roles, diagnostics))
            return new(spec.ScenarioName, false, "blocker:invalid-profile-stack", null, diagnostics, roles, [], false, 0);

        var built = BrepBooleanBoxCylinderHoleBuilder.BuildComposition(composition, ToleranceContext.Default);
        if (!built.IsSuccess || built.Value is null)
            return new(spec.ScenarioName, false, "blocker:composition-build-failed", null, diagnostics.Concat(built.Diagnostics.Select(d => d.Message)).ToArray(), roles, [], false, 0);

        var step = Step242Exporter.ExportBody(built.Value);
        var text = step.IsSuccess ? step.Value : string.Empty;
        var markers = new[] { "ISO-10303-21", "MANIFOLD_SOLID_BREP", "ADVANCED_FACE", "CYLINDRICAL_SURFACE" }.Where(m => text.Contains(m, StringComparison.Ordinal)).ToArray();
        return new(spec.ScenarioName, step.IsSuccess, step.IsSuccess ? "success" : "blocker:step-export-failed", built.Value,
            diagnostics, roles, markers, text.Contains("BREP_WITH_VOIDS", StringComparison.Ordinal), built.Value.Topology.Faces.Count());
    }

    private static bool TryBuildComposition(ProfileStackExtrudeSpec spec, out SafeBooleanComposition composition, out List<string> roles, List<string> diagnostics)
    {
        roles = ["TopFace", "BottomFace", "OuterWall_PosX", "OuterWall_NegX", "OuterWall_PosY", "OuterWall_NegY"];
        var layers = spec.Layers.OrderBy(l => l.ZMin).ToArray();
        if (layers.Length == 0 || layers.Any(l => l.ZMax <= l.ZMin)) { composition = default!; return false; }

        var holes = new List<SupportedBooleanHole>();
        var zAxis = Direction3D.Create(new Vector3D(0,0,1));
        var xAxis = Direction3D.Create(new Vector3D(1,0,0));
        for (var i = 0; i < layers.Length; i++)
        {
            var l = layers[i];
            if (l.InnerCircleRadius is null || l.InnerCircleRadius <= 0) continue;
            var span = l.ZMin <= spec.ZMin + 1e-9 && l.ZMax >= spec.ZMax - 1e-9 ? SupportedBooleanHoleSpanKind.Through : (Math.Abs(l.ZMax - spec.ZMax) < 1e-9 ? SupportedBooleanHoleSpanKind.BlindFromTop : SupportedBooleanHoleSpanKind.Contained);
            var start = new Point3D(0,0,l.ZMin);
            var end = new Point3D(0,0,l.ZMax);
            var cyl = new RecognizedCylinder(new Point3D(0,0,0), zAxis, l.InnerCircleRadius.Value, l.ZMin, l.ZMax);
            holes.Add(new SupportedBooleanHole(l.RoleName, new AnalyticSurface(AnalyticSurfaceKind.Cylinder, Cylinder: cyl), 0, 0, start, end, zAxis, xAxis, l.InnerCircleRadius.Value, l.InnerCircleRadius.Value, span, l.ZMin, l.ZMax));
            roles.Add($"InnerWall_Radius{l.InnerCircleRadius.Value:0.###}");
            if (i > 0 && layers[i-1].InnerCircleRadius.HasValue && Math.Abs(layers[i-1].InnerCircleRadius.Value - l.InnerCircleRadius.Value) > 1e-9)
                roles.Add($"Shoulder_R{layers[i-1].InnerCircleRadius.Value:0.###}_To_R{l.InnerCircleRadius.Value:0.###}");
            if (i > 0 && !layers[i-1].InnerCircleRadius.HasValue) roles.Add("BlindBottomCap");
        }

        composition = new SafeBooleanComposition(new AxisAlignedBoxExtents(-spec.Width/2, spec.Width/2, -spec.Depth/2, spec.Depth/2, spec.ZMin, spec.ZMax), holes, SafeBooleanRootDescriptor.FromBox(new AxisAlignedBoxExtents(-spec.Width/2, spec.Width/2, -spec.Depth/2, spec.Depth/2, spec.ZMin, spec.ZMax)));
        diagnostics.Add($"holes:{holes.Count}");
        return true;
    }

    private static ProfileStackExtrudeSpec BuildStepped() => new(30,30,-10,10,[new(-10,2,2,"small"),new(2,6,3,"medium"),new(6,10,4,"large")],"stepped-hole");
    private static ProfileStackExtrudeSpec BuildThrough() => new(30,30,-10,10,[new(-10,10,2,"through")],"through-hole");
    private static ProfileStackExtrudeSpec BuildBlind() => new(30,30,-10,10,[new(-10,2,null,"solid"),new(2,10,2,"blind")],"blind-hole");
    private static ProfileStackExtrudeSpec BuildCounterbore() => new(30,30,-10,10,[new(-10,10,2,"through"),new(6,10,4,"counterbore")],"counterbore");
}
