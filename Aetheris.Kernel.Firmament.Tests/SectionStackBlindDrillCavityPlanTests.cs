using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SectionStackBlindDrillCavityPlanTests
{
    [Fact]
    public void ComposeSource_ConstructionPlaneBlindDrill_UsesFullRadiusClearanceAndExportsOneBlindCavity()
    {
        var source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/FirmamentV2/ProfileComposition/valid/construction-plane-blind-drill-clearance.firmament"));
        var output = Path.Combine(Path.GetTempPath(), "aetheris-compose-blind-drill-" + Guid.NewGuid().ToString("N") + ".step");
        try
        {
            var exported = FirmamentBuildAndExport.Run(source, output);
            Assert.True(exported.IsSuccess, string.Join(" | ", exported.Diagnostics.Select(x => x.Message)));
            var imported = Step242Importer.ImportBody(File.ReadAllText(output));
            Assert.True(imported.IsSuccess, string.Join(" | ", imported.Diagnostics.Select(x => x.Message)));
            var mass = BrepMassProperties.Evaluate(imported.Value);
            Assert.True(mass.IsEnclosed, string.Join(" | ", mass.Diagnostics));
            Assert.True(mass.IsOrientationConsistent, string.Join(" | ", mass.Diagnostics));
            Assert.Contains(imported.Value.Geometry.Surfaces.Select(x => x.Value), x => x.Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Cylinder);
            Assert.Contains(imported.Value.Geometry.Surfaces.Select(x => x.Value), x => x.Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Cone);
        }
        finally
        {
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Fact]
    public void ComposeSource_MouthCrossingInternalSlabIsRejectedBeforeTopologyMaterialization()
    {
        var source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/FirmamentV2/ProfileComposition/invalid/construction-plane-blind-drill-mouth-crosses-slab.firmament"));

        var exported = FirmamentBuildAndExport.Run(source, Path.Combine(Path.GetTempPath(), "aetheris-unreachable.step"));

        Assert.False(exported.IsSuccess);
        Assert.Contains(exported.Diagnostics, x => x.Message == "SectionStackBlindDrillMouthCrossesHostPlanningPartition");
    }

    [Fact]
    public void ProvenPlanarMouth_ReplacesHostFaceAndIntegratesOneBlindCavityShell()
    {
        var stack = Stack(); var host = PrismaticSectionStackEmitter.TryPlan(stack).Plan!;
        var placement = new AirConstructionPlaneHolePlacement("construction:+X", "concept:+X", new Point3D(-20, 0, 2),
            Direction3D.Create(new Vector3D(0, 1, 0)), Direction3D.Create(new Vector3D(0, 0, 1)), Direction3D.Create(new Vector3D(1, 0, 0)), 0, 0, "test", "test");
        var feature = AirHoleFeature.CreateConstructionPlaneSimpleShaft("SideBlind", "Host.SideBlind", "Host", placement, new AirHoleShaft(2),
            new AirHoleEndCondition.ShaftDepth(10), new AirProvenance("test", "test", "SideBlind", "Host.SideBlind", "test", AirSelectionClass.None, AirRuleKind.None, "test", true, []), new AirHoleTermination.DrillPoint());
        var corridor = TransverseBlindDrillToolCorridor.Prove(feature, stack, placement);
        Assert.Equal(BlindDrillToolCorridorClassification.CorridorProven, corridor.Classification);
        Assert.Equal(BlindDrillClearancePolicy.FullRadiusThroughTotalDepth, corridor.ValidationPolicy);
        Assert.All(corridor.ShaftSliceProofs, proof => Assert.Equal("FullRadiusClearance", proof.ToolPart));
        Assert.Empty(corridor.ConeSliceProofs);
        var mouthFace = host.TopologyPlan!.FaceMappings.Single(mapping => mapping.Kind == "PrismaticSide" && mapping.SourceStableId.Contains("West", StringComparison.Ordinal) && mapping.SlabFrom == 0d).FaceId;

        var cavity = SectionStackBlindDrillCavityPlanner.TryPlan(new(stack, host, feature, placement, corridor, mouthFace), out var diagnostics);

        Assert.NotNull(cavity); Assert.DoesNotContain(diagnostics, x => x.StartsWith("SectionStackBlindDrillMouth", StringComparison.Ordinal) || x.EndsWith("NotProven", StringComparison.Ordinal));
        Assert.Single(cavity!.FaceReplacements);
        var finalPlan = cavity.ReplacementHostPlan.TopologyPlan!;
        var materialized = PrismaticSectionStackBrepMaterializer.TryMaterialize(finalPlan);
        Assert.NotNull(materialized.Body); Assert.Empty(materialized.Diagnostics);
        var body = materialized.Body!;
        var mass = BrepMassProperties.Evaluate(body);
        Assert.True(mass.IsEnclosed, string.Join(" | ", mass.Diagnostics));
        Assert.True(mass.IsOrientationConsistent, string.Join(" | ", mass.Diagnostics));
        var expectedVolume = stack.AnalyticVolume - (Math.PI * feature.Shaft.Radius * feature.Shaft.Radius * corridor.ShaftDepth)
            - ((Math.PI * feature.Shaft.Radius * feature.Shaft.Radius * corridor.TipLength) / 3d);
        Assert.InRange(Math.Abs(mass.AbsoluteVolume - expectedVolume), 0d, mass.ErrorBound ?? 0d);
        Assert.Single(finalPlan.Correspondence.Descendants.Where(x => x.Role == SemanticTopologyRole.HoleEntryLoop));
        Assert.Single(finalPlan.Correspondence.Descendants.Where(x => x.Role == SemanticTopologyRole.HoleTipVertex));
        Assert.Empty(finalPlan.Correspondence.Descendants.Where(x => x.Role == SemanticTopologyRole.HoleExitLoop));
        Assert.Contains("SectionStackBlindDrillNoInternalCaps", cavity.Diagnostics);
        var step = Step242Exporter.ExportBody(body, new Step242ExportOptions { BrepExportPreflightMode = BrepExportPreflightMode.Enforce });
        Assert.True(step.IsSuccess, string.Join(" | ", step.Diagnostics.Select(x => x.Message)));
        var reimported = Step242Importer.ImportBody(step.Value);
        Assert.True(reimported.IsSuccess, string.Join(" | ", reimported.Diagnostics.Select(x => x.Message)));
        var reimportedMass = BrepMassProperties.Evaluate(reimported.Value);
        Assert.True(reimportedMass.IsEnclosed);
        Assert.InRange(Math.Abs(reimportedMass.AbsoluteVolume - expectedVolume), 0d, reimportedMass.ErrorBound ?? 0d);
    }

    private static PrismaticSectionStackConstruction Stack()
    {
        const string source = """
            Concept Struct Layout On XY { Rect2 Guide { Center: [0mm, 0mm]; Size: [40mm, 20mm] } }
            Profile Stock Using Layout { Loop Outer {
                Segment South { Trace: Guide.Bottom; From: Guide.BottomLeft; To: Guide.BottomRight }
                Segment East { Trace: Guide.Right; From: Guide.BottomRight; To: Guide.TopRight }
                Segment North { Trace: Guide.Top; From: Guide.TopRight; To: Guide.TopLeft }
                Segment West { Trace: Guide.Left; From: Guide.TopLeft; To: Guide.BottomLeft }
            } }
            Struct Composition { Compose Host { Base Lower { Profile: Stock; From: 0mm; To: 5mm } Add Upper { Profile: Stock; From: 5mm; To: 10mm } } }
            """;
        var parsed = PrismaticProfileCompositionParser.Parse(source);
        return Assert.IsType<PrismaticSectionStackConstruction>(PrismaticSectionStackCompiler.Normalize(parsed, out var diagnostics));
    }
}
