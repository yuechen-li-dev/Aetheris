using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ChamferedEntryHoleVariantAndExecutorTests
{
    [Fact]
    public void ChamferedEntryVariant_AdmitsSimpleBoxChamferedThroughHolePlan()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildChamfered()));
        Assert.Contains("selected-variant:ChamferedEntryHoleVariant", eval.Evidence);
        var plan = Assert.IsType<HoleRecoveryPlan>(eval.Plan);
        Assert.Equal(HoleKind.ChamferedEntry, plan.HoleKind);
        Assert.Equal(HoleProfileSegmentKind.Conical, plan.ProfileStack[0].SegmentKind);
        Assert.Equal(HoleProfileSegmentKind.Cylindrical, plan.ProfileStack[1].SegmentKind);
        Assert.Contains(plan.ExpectedSurfacePatches, p => p.Role == HoleSurfacePatchRole.ChamferedEntryWall);
    }
    
    [Fact]
    public void ChamferedEntry_BottomEntry_AdmitsPlan()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildChamferedBottom()));
        Assert.Contains("selected-variant:ChamferedEntryHoleVariant", eval.Evidence);
        var plan = Assert.IsType<HoleRecoveryPlan>(eval.Plan);
        Assert.Equal(HoleKind.ChamferedEntry, plan.HoleKind);
        Assert.Equal(HoleEntryFeatureKind.Chamfer, plan.EntryFeature);
        Assert.Contains(eval.EvaluationsFor(nameof(ChamferedEntryHoleVariant)), d => d.Contains("bottom(-Z)", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ChamferedEntryVariant_RejectsCountersinkSizedCone()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildCountersink()));
        Assert.Contains("selected-variant:CountersinkVariant", eval.Evidence);
        Assert.Contains(eval.Diagnostics, d => d.Contains("Variant rejected: ChamferedEntryHoleVariant", StringComparison.Ordinal));
    }

    [Fact]
    public void CountersinkVariant_RejectsChamferSizedCone()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildChamfered()));
        Assert.Contains("selected-variant:ChamferedEntryHoleVariant", eval.Evidence);
        Assert.Contains(eval.Diagnostics, d => d.Contains("Variant rejected: CountersinkVariant", StringComparison.Ordinal));
    }

    [Fact]
    public void ChamferedEntryVariant_RejectsNonCoaxialConeCylinder()
    {
        var cone = new SdfTransformNode(new SdfConeNode(2d, 2.8d, 1d), Transform3D.CreateTranslation(new Vector3D(1d, 0d, 4.5d)));
        var root = new SdfSubtractNode(new SdfSubtractNode(new SdfBoxNode(20d,20d,10d), new SdfCylinderNode(2d,20d)), cone);
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root));
        Assert.Contains(eval.EvaluationsFor(nameof(ChamferedEntryHoleVariant)), d => d.Contains("not coaxial", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ChamferedEntryExecutor_CanonicalChamferedEntry_ProducesBrepBody()
    {
        var plan = Assert.IsType<HoleRecoveryPlan>(new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildChamfered())).Plan);
        var result = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, result.Status);
        Assert.NotNull(result.Body);
        AssertAllFaceLoopsAreTopologicallyClosed(result.Body!);
        var inwardFaces = result.Body!.Bindings.FaceBindings
            .Where(binding => result.Body.Geometry.GetSurface(binding.SurfaceGeometryId).Kind is
                Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Cone or
                Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Cylinder)
            .ToArray();
        Assert.Equal(2, inwardFaces.Length);
        Assert.All(inwardFaces, binding => Assert.False(binding.SameSense));
        AssertConeBoundaryVerticesLieOnSupport(result.Body!);
        Assert.Contains(result.Diagnostics, d => d.Contains("cylinder subtract invoked", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Diagnostics, d => d.Contains("chamfer cone subtract invoked", StringComparison.OrdinalIgnoreCase));
    }
    
    [Fact]
    public void ChamferedEntry_BottomEntry_ProducesBrepBody()
    {
        var plan = Assert.IsType<HoleRecoveryPlan>(new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildChamferedBottom())).Plan);
        var result = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, result.Status);
        Assert.NotNull(result.Body);
        Assert.Contains(result.Diagnostics, d => d.Contains("cylinder subtract invoked", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Diagnostics, d => d.Contains("chamfer cone subtract invoked", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Diagnostics, d => d.Contains("bottom(-Z)", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ChamferedEntryStepSmoke_CanonicalChamferedEntry_ExportsStep()
    {
        var plan = Assert.IsType<HoleRecoveryPlan>(new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildChamfered())).Plan);
        var exec = HoleRecoveryExecutor.Execute(plan);
        var step = Step242Exporter.ExportBody(exec.Body!);
        Assert.True(step.IsSuccess);
        Assert.Contains("ISO-10303-21", step.Value);
        Assert.Contains("MANIFOLD_SOLID_BREP", step.Value);
        Assert.Contains("ADVANCED_FACE", step.Value);
        Assert.Contains("CONICAL_SURFACE", step.Value);
        Assert.Contains("CYLINDRICAL_SURFACE", step.Value);
        Assert.DoesNotContain("BREP_WITH_VOIDS", step.Value);
    }
    
    [Fact]
    public void ChamferedEntry_BottomEntry_ExportsStep()
    {
        var plan = Assert.IsType<HoleRecoveryPlan>(new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildChamferedBottom())).Plan);
        var exec = HoleRecoveryExecutor.Execute(plan);
        var step = Step242Exporter.ExportBody(exec.Body!);
        Assert.True(step.IsSuccess);
        Assert.Contains("ISO-10303-21", step.Value);
        Assert.Contains("MANIFOLD_SOLID_BREP", step.Value);
        Assert.Contains("ADVANCED_FACE", step.Value);
        Assert.Contains("CONICAL_SURFACE", step.Value);
        Assert.Contains("CYLINDRICAL_SURFACE", step.Value);
        Assert.DoesNotContain("BREP_WITH_VOIDS", step.Value);
    }

    [Fact]
    public void ChamferedEntry_BottomEntry_DoesNotStealCountersink()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildCountersinkBottom()));
        Assert.Contains("selected-variant:CountersinkVariant", eval.Evidence);
        Assert.Contains(eval.Diagnostics, d => d.Contains("Variant rejected: ChamferedEntryHoleVariant", StringComparison.Ordinal));
    }

    [Fact]
    public void Countersink_DoesNotStealBottomChamfer()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildChamferedBottom()));
        Assert.Contains("selected-variant:ChamferedEntryHoleVariant", eval.Evidence);
        Assert.Contains(eval.Diagnostics, d => d.Contains("Variant rejected: CountersinkVariant", StringComparison.Ordinal));
    }

    [Fact]
    public void ChamferedEntry_RejectsInvalidBottomConeOrientation()
    {
        var cone = new SdfTransformNode(new SdfConeNode(2d, 2.8d, 1d), Transform3D.CreateTranslation(new Vector3D(0d, 0d, -4.5d)));
        var root = new SdfSubtractNode(new SdfSubtractNode(new SdfBoxNode(20d, 20d, 10d), new SdfCylinderNode(2d, 20d)), cone);
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root));
        Assert.Contains(eval.EvaluationsFor(nameof(ChamferedEntryHoleVariant)), d => d.Contains("radius ordering invalid", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("selected-variant:ChamferedEntryHoleVariant", eval.Evidence);
    }

    [Fact]
    public void ChamferedEntry_Unsupported_DoesNotFalseSucceed()
    {
        var cone = new SdfTransformNode(new SdfConeNode(2d, 2.8d, 1d), Transform3D.CreateTranslation(new Vector3D(0d, 0d, 1.5d)));
        var root = new SdfSubtractNode(new SdfSubtractNode(new SdfBoxNode(20d,20d,10d), new SdfCylinderNode(2d,20d)), cone);
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root));
        Assert.False(eval.Admissible);
    }

    private static SdfNode BuildChamfered()
        => new SdfSubtractNode(new SdfSubtractNode(new SdfBoxNode(20d,20d,10d), new SdfCylinderNode(2d,20d)), new SdfTransformNode(new SdfConeNode(2d, 2.8d, 1d), Transform3D.CreateTranslation(new Vector3D(0d, 0d, 4.5d))));
    private static SdfNode BuildCountersink()
        => new SdfSubtractNode(new SdfSubtractNode(new SdfBoxNode(20d,20d,10d), new SdfCylinderNode(2d,20d)), new SdfTransformNode(new SdfConeNode(2d, 4d, 4d), Transform3D.CreateTranslation(new Vector3D(0d, 0d, 3d))));
    private static SdfNode BuildChamferedBottom()
        => new SdfSubtractNode(new SdfSubtractNode(new SdfBoxNode(20d,20d,10d), new SdfCylinderNode(2d,20d)), new SdfTransformNode(new SdfConeNode(2.8d, 2d, 1d), Transform3D.CreateTranslation(new Vector3D(0d, 0d, -4.5d))));
    private static SdfNode BuildCountersinkBottom()
        => new SdfSubtractNode(new SdfSubtractNode(new SdfBoxNode(20d,20d,10d), new SdfCylinderNode(2d,20d)), new SdfTransformNode(new SdfConeNode(4d, 2d, 4d), Transform3D.CreateTranslation(new Vector3D(0d, 0d, -3d))));

    private static void AssertAllFaceLoopsAreTopologicallyClosed(Aetheris.Kernel.Core.Brep.BrepBody body)
    {
        foreach (var loop in body.Topology.Loops)
        {
            var coedges = loop.CoedgeIds.Select(body.Topology.GetCoedge).ToArray();
            for (var i = 0; i < coedges.Length; i++)
            {
                var current = coedges[i];
                var next = coedges[(i + 1) % coedges.Length];
                var currentEdge = body.Topology.GetEdge(current.EdgeId);
                var nextEdge = body.Topology.GetEdge(next.EdgeId);
                var currentEnd = current.IsReversed ? currentEdge.StartVertexId : currentEdge.EndVertexId;
                var nextStart = next.IsReversed ? nextEdge.EndVertexId : nextEdge.StartVertexId;
                Assert.True(currentEnd == nextStart,
                    $"Loop {loop.Id.Value} is disconnected between coedges {current.Id.Value} and {next.Id.Value}: " +
                    $"vertex {currentEnd.Value} != {nextStart.Value}.");
            }
        }
    }

    private static void AssertConeBoundaryVerticesLieOnSupport(Aetheris.Kernel.Core.Brep.BrepBody body)
    {
        var binding = Assert.Single(body.Bindings.FaceBindings, candidate =>
            body.Geometry.GetSurface(candidate.SurfaceGeometryId).Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Cone);
        var cone = body.Geometry.GetSurface(binding.SurfaceGeometryId).Cone!.Value;
        var face = body.Topology.GetFace(binding.FaceId);
        foreach (var vertexId in face.LoopIds
                     .Select(body.Topology.GetLoop)
                     .SelectMany(loop => loop.CoedgeIds)
                     .Select(body.Topology.GetCoedge)
                     .SelectMany(coedge =>
                     {
                         var edge = body.Topology.GetEdge(coedge.EdgeId);
                         return new[] { edge.StartVertexId, edge.EndVertexId };
                     })
                     .Distinct())
        {
            Assert.True(body.TryGetVertexPoint(vertexId, out var point));
            var fromApex = point - cone.Apex;
            var axial = fromApex.Dot(cone.Axis.ToVector());
            var radialSquared = Math.Max(0d, fromApex.Dot(fromApex) - axial * axial);
            Assert.Equal(axial * Math.Tan(cone.SemiAngleRadians), Math.Sqrt(radialSquared), 9);
        }
    }
}
