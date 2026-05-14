using System.Text;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242ImporterTests
{
    [Fact]
    public void ImportBody_KnownGoodM22SubsetText_ReturnsValidatedBody()
    {
        var fixtureText = Step242FixtureCorpus.CanonicalBoxGolden;

        var import = Step242Importer.ImportBody(fixtureText);

        Assert.True(import.IsSuccess);
        var validation = BrepBindingValidator.Validate(import.Value, requireAllEdgeAndFaceBindings: true);
        Assert.True(validation.IsSuccess);

        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);
        Assert.True(tessellation.IsSuccess);
        Assert.NotEmpty(tessellation.Value.FacePatches);
    }

    [Fact]
    public void ExportImportRoundTrip_BoxSubset_PreservesBasicTopologyInvariants()
    {
        var boxResult = BrepPrimitives.CreateBox(4d, 6d, 8d);
        Assert.True(boxResult.IsSuccess);

        var export = Step242Exporter.ExportBody(boxResult.Value);
        Assert.True(export.IsSuccess);

        var import = Step242Importer.ImportBody(export.Value);

        Assert.True(import.IsSuccess);
        Assert.Equal(boxResult.Value.Topology.Vertices.Count(), import.Value.Topology.Vertices.Count());
        Assert.Equal(boxResult.Value.Topology.Edges.Count(), import.Value.Topology.Edges.Count());
        Assert.Equal(boxResult.Value.Topology.Faces.Count(), import.Value.Topology.Faces.Count());

        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);
        Assert.True(tessellation.IsSuccess);
    }

    [Fact]
    public void ImportBody_MalformedStep_ReturnsDiagnosticWithoutThrowing()
    {
        var malformed = Step242FixtureCorpus.MalformedMissingParen;

        var import = Step242Importer.ImportBody(malformed);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.InvalidArgument, diagnostic.Code);
        Assert.Equal(KernelDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.StartsWith("Parser", diagnostic.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportBody_ToroidalSurfaceEntity_IsNoLongerRejectedAsUnsupportedFamily()
    {
        var toroidalOnly = Step242FixtureCorpus.UnsupportedToroidalSurface;

        var import = Step242Importer.ImportBody(toroidalOnly);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal(KernelDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Importer.TopologyRoot", diagnostic.Source);
        Assert.DoesNotContain("TOROIDAL_SURFACE", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImportBody_MissingSolidRoot_ReturnsDeterministicTopologyRootDiagnostic()
    {
        const string noRoot = "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n#1=CARTESIAN_POINT($,(0,0,0));\nENDSEC;\nEND-ISO-10303-21;";

        var import = Step242Importer.ImportBody(noRoot);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Importer.TopologyRoot", diagnostic.Source);
        Assert.StartsWith("Missing MANIFOLD_SOLID_BREP or BREP_WITH_VOIDS", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportBody_MultipleSolidRoots_ClassifiesInputAsAssemblyLike()
    {
        const string multiRoot = "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n#1=MANIFOLD_SOLID_BREP('a',#3);\n#2=MANIFOLD_SOLID_BREP('b',#3);\n#3=CLOSED_SHELL($,());\nENDSEC;\nEND-ISO-10303-21;";

        var import = Step242Importer.ImportBody(multiRoot);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Importer.AssemblyLike.StepMultiRoot", diagnostic.Source);
        Assert.Contains("assembly-like", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("detected 2 exact BRep rigid roots", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportBody_TessellatedSolidWithComplexTriangulatedFace_ImportsWithoutManifoldSolidBrep()
    {
        const string tessellated = "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n#1=TESSELLATED_SOLID('part',(#2));\n#2=TESSELLATED_SHELL('shell',(#3));\n#3=COMPLEX_TRIANGULATED_FACE('',#10,3,((0,0,1)),#20,(1,2,3),((1,2,3)),());\n#10=COORDINATES_LIST('',3,((0,0,0),(1,0,0),(0,1,0)));\n#20=PLANE('',#30);\n#30=AXIS2_PLACEMENT_3D('',#31,#32,#33);\n#31=CARTESIAN_POINT('',(0,0,0));\n#32=DIRECTION('',(0,0,1));\n#33=DIRECTION('',(1,0,0));\nENDSEC;\nEND-ISO-10303-21;";

        var import = Step242Importer.ImportBody(tessellated);

        Assert.True(import.IsSuccess);
        Assert.Single(import.Value.Topology.Faces);
        Assert.DoesNotContain(import.Diagnostics, d => string.Equals(d.Source, "Importer.TopologyRoot", StringComparison.Ordinal));
    }

    [Fact]
    public void NistFtc08Tg_TessellatedVariant_IsClassifiedAsUnsupportedForExactBrepCorpus()
    {
        var text = LoadFixture("testdata/step242/nist/FTC/nist_ftc_08_asme1_ap242-e1-tg.stp");
        var parse = Step242SubsetParser.Parse(text);
        Assert.True(parse.IsSuccess);

        var import = Step242ExactBRepImportLane.ImportExactBrep(parse.Value);
        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Importer.TopologyRoot", diagnostic.Source);
        Assert.StartsWith("Missing MANIFOLD_SOLID_BREP or BREP_WITH_VOIDS", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportBody_DegenerateDirection_ReturnsDiagnosticWithoutThrowing()
    {
        const string degenerateDirection = "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n#1=MANIFOLD_SOLID_BREP('solid',#2);\n#2=CLOSED_SHELL($,(#3));\n#3=ADVANCED_FACE((#4),#5,.T.);\n#4=FACE_OUTER_BOUND($,#6,.T.);\n#5=PLANE($,#20);\n#6=EDGE_LOOP($,(#7));\n#7=ORIENTED_EDGE($,$,$,#8,.T.);\n#8=EDGE_CURVE($,#9,#10,#11,.T.);\n#9=VERTEX_POINT($,#12);\n#10=VERTEX_POINT($,#13);\n#11=LINE($,#12,#14);\n#12=CARTESIAN_POINT($,(0,0,0));\n#13=CARTESIAN_POINT($,(1,0,0));\n#14=VECTOR($,#15,1.0);\n#15=DIRECTION($,(1,0,0));\n#20=AXIS2_PLACEMENT_3D($,#12,#21,#22);\n#21=DIRECTION($,(0,0,0));\n#22=DIRECTION($,(1,0,0));\nENDSEC;\nEND-ISO-10303-21;";

        var import = Step242Importer.ImportBody(degenerateDirection);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Importer.Geometry.Direction", diagnostic.Source);
        Assert.StartsWith("Degenerate direction vector", diagnostic.Message, StringComparison.Ordinal);
    }


    [Fact]
    public void Step242_AdvancedFace_Bounds_SingleAggregate_ConsumesCorrectly()
    {
        var text = LoadFixture("testdata/step242/syntax-robustness/advanced-face-bounds-single-aggregate.step");

        var import = Step242Importer.ImportBody(text);

        Assert.True(import.IsSuccess);
        Assert.DoesNotContain(import.Diagnostics, d => d.Message.Contains("ADVANCED_FACE surface: expected entity reference or inline entity constructor.", StringComparison.Ordinal));
    }

    [Fact]
    public void Step242_AdvancedFace_Bounds_MultiAggregate_ConsumesCorrectly()
    {
        var text = LoadFixture("testdata/step242/syntax-robustness/advanced-face-bounds-multi-aggregate.step");

        var import = Step242Importer.ImportBody(text);

        Assert.DoesNotContain(import.Diagnostics, d => d.Message.Contains("ADVANCED_FACE surface: expected entity reference or inline entity constructor.", StringComparison.Ordinal));
        Assert.DoesNotContain(import.Diagnostics, d => string.Equals(d.Source, "Importer.StepSyntax.AdvancedFaceBounds", StringComparison.Ordinal));
    }

    [Fact]
    public void Step242_FreeCAD_Repro_ImportsPastAdvancedFaceBounds()
    {
        var text = LoadFixture("testdata/step242/syntax-robustness/freecad-pad-repro.step");

        var import = Step242Importer.ImportBody(text);

        Assert.True(import.IsSuccess);
        Assert.DoesNotContain(import.Diagnostics, d => d.Message.Contains("ADVANCED_FACE surface", StringComparison.Ordinal));
    }

    [Fact]
    public void Step242_AdvancedFace_Surface_AllowsInlinePlaneConstructor()
    {
        var text = LoadFixture("testdata/step242/syntax-robustness/advanced-face-inline-plane.step");

        var parseResult = Step242SubsetParser.Parse(text);
        Assert.True(parseResult.IsSuccess);

        var import = Step242Importer.ImportBody(text);

        Assert.True(import.IsSuccess);
        Assert.Single(import.Value.Topology.Faces);
        Assert.DoesNotContain(import.Diagnostics, d => d.Message.Contains("ADVANCED_FACE surface: expected entity reference argument.", StringComparison.Ordinal));
    }

    [Fact]
    public void Step242_AdvancedFace_Surface_AllowsEntityRefPlaneConstructor()
    {
        var text = LoadFixture("testdata/step242/syntax-robustness/advanced-face-ref-plane.step");

        var parseResult = Step242SubsetParser.Parse(text);
        Assert.True(parseResult.IsSuccess);

        var import = Step242Importer.ImportBody(text);

        Assert.True(import.IsSuccess);
        Assert.Single(import.Value.Topology.Faces);
    }

    [Fact]
    public void Step242_AdvancedFace_Surface_AllowsInlineCylindricalSurfaceConstructor()
    {
        var text = LoadFixture("testdata/step242/syntax-robustness/advanced-face-inline-cylinder.step");

        var parseResult = Step242SubsetParser.Parse(text);
        Assert.True(parseResult.IsSuccess);

        var import = Step242Importer.ImportBody(text);

        Assert.DoesNotContain(import.Diagnostics, d => d.Source is not null && d.Source.StartsWith("Parser", StringComparison.Ordinal));
        Assert.DoesNotContain(import.Diagnostics, d => string.Equals(d.Source, "Importer.StepSyntax.InlineEntity", StringComparison.Ordinal));

        if (!import.IsSuccess)
        {
            Assert.NotEmpty(import.Diagnostics);
            return;
        }

        Assert.Contains(import.Value.Geometry.Surfaces, s => s.Value.Kind == SurfaceGeometryKind.Cylinder);
    }

    [Fact]
    public void Step242_AdvancedFace_Surface_RejectsInlineCylinderMalformedArgs()
    {
        var text = LoadFixture("testdata/step242/syntax-robustness/advanced-face-inline-cylinder-malformed.step");

        var parseResult = Step242SubsetParser.Parse(text);
        Assert.True(parseResult.IsSuccess);

        var import = Step242Importer.ImportBody(text);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Importer.StepSyntax.InlineEntity", diagnostic.Source);
        Assert.StartsWith("Inline ADVANCED_FACE.surface constructor 'CYLINDRICAL_SURFACE' has unsupported argument shape.", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Step242_AdvancedFace_Surface_AllowsInlineConicalSurfaceConstructor()
    {
        var text = LoadFixture("testdata/step242/syntax-robustness/advanced-face-inline-cone-valid.step");

        var parseResult = Step242SubsetParser.Parse(text);
        Assert.True(parseResult.IsSuccess);

        var import = Step242Importer.ImportBody(text);

        Assert.DoesNotContain(import.Diagnostics, d => d.Source is not null && d.Source.StartsWith("Parser", StringComparison.Ordinal));
        Assert.DoesNotContain(import.Diagnostics, d => string.Equals(d.Source, "Importer.StepSyntax.InlineEntity", StringComparison.Ordinal));

        if (!import.IsSuccess)
        {
            Assert.NotEmpty(import.Diagnostics);
            return;
        }

        Assert.Contains(import.Value.Geometry.Surfaces, s => s.Value.Kind == SurfaceGeometryKind.Cone);
    }

    [Fact]
    public void Step242_AdvancedFace_Surface_RejectsInlineConicalSurface_MalformedArgs()
    {
        var text = LoadFixture("testdata/step242/syntax-robustness/advanced-face-inline-cone-malformed.step");

        var parseResult = Step242SubsetParser.Parse(text);
        Assert.True(parseResult.IsSuccess);

        var import = Step242Importer.ImportBody(text);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Importer.StepSyntax.InlineEntity", diagnostic.Source);
        Assert.StartsWith("Inline ADVANCED_FACE.surface constructor 'CONICAL_SURFACE' has unsupported argument shape.", diagnostic.Message, StringComparison.Ordinal);
    }


    [Fact]
    public void Step242_SubsetDecoder_ReadEllipseCurve_DecodesPlacementAndRadii()
    {
        const string text = "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n"
            + "#10=ELLIPSE('',#20,6.,4.);\n"
            + "#20=AXIS2_PLACEMENT_3D('',#21,#22,#23);\n"
            + "#21=CARTESIAN_POINT('',(1.,2.,3.));\n"
            + "#22=DIRECTION('',(0.,0.,1.));\n"
            + "#23=DIRECTION('',(1.,0.,0.));\n"
            + "ENDSEC;\nEND-ISO-10303-21;";

        var parse = Step242SubsetParser.Parse(text);
        Assert.True(parse.IsSuccess);

        var ellipseEntityResult = parse.Value.TryGetEntity(10, "ELLIPSE");
        Assert.True(ellipseEntityResult.IsSuccess);

        var ellipse = Step242SubsetDecoder.ReadEllipseCurve(parse.Value, ellipseEntityResult.Value);
        Assert.True(ellipse.IsSuccess);

        Assert.Equal(6d, ellipse.Value.MajorRadius, 9);
        Assert.Equal(4d, ellipse.Value.MinorRadius, 9);
        Assert.True(((ellipse.Value.Evaluate(0d) - new Point3D(7d, 2d, 3d)).LengthSquared) < 1e-12d);
        Assert.True(((ellipse.Value.Evaluate(double.Pi / 2d) - new Point3D(1d, 6d, 3d)).LengthSquared) < 1e-12d);
    }

    [Fact]
    public void Step242_CurveSampler_Circle3_SamplesArcDeterministically()
    {
        var circle = new Circle3Curve(Point3D.Origin, Direction3D.Create(new Vector3D(0d, 0d, 1d)), 2d, Direction3D.Create(new Vector3D(1d, 0d, 0d)));

        var forward = CurveSampler.SampleCircleArc(circle, 0d, double.Pi / 2d);
        var second = CurveSampler.SampleCircleArc(circle, 0d, double.Pi / 2d);
        var reversed = CurveSampler.SampleCircleArc(circle, double.Pi / 2d, -double.Pi / 2d);

        Assert.Equal(forward.Count, second.Count);
        Assert.Equal(13, forward.Count);
        Assert.True(((forward[0] - circle.Evaluate(0d)).LengthSquared) < 1e-12d);
        Assert.True(((forward[^1] - circle.Evaluate(double.Pi / 2d)).LengthSquared) < 1e-12d);

        for (var i = 0; i < forward.Count; i++)
        {
            Assert.True(((forward[i] - second[i]).LengthSquared) < 1e-12d);
            Assert.True(((forward[i] - reversed[^(i + 1)]).LengthSquared) < 1e-12d);
        }
    }

    [Fact]
    public void Step242_0430200200_Import_NoCircle3LineOnlyError()
    {
        var text = LoadFixture("testdata/step242/tessellation-robustness/planar-rect-with-filleted-corners.step");

        var import = Step242Importer.ImportBody(text);
        Assert.True(import.IsSuccess);

        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);
        Assert.True(tessellation.IsSuccess);

        Assert.DoesNotContain(import.Diagnostics, d => d.Message.Contains("Circle3", StringComparison.Ordinal));
        Assert.DoesNotContain(import.Diagnostics, d => d.Message.Contains("line edges only", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(tessellation.Diagnostics, d => d.Message.Contains("Circle3", StringComparison.Ordinal));
        Assert.DoesNotContain(tessellation.Diagnostics, d => d.Message.Contains("line edges only", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public void CircleTrim_ShortArc_DoesNotWrapFullCircle()
    {
        var circle = new Circle3Curve(Point3D.Origin, Direction3D.Create(new Vector3D(0d, 0d, 1d)), 1d, Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        var start = circle.Evaluate(0d);
        var end = circle.Evaluate(double.Pi / 6d);

        var sampled = CurveSampler.TrySampleTrimmedCircleArc(circle, start, end, orientedEdgeSense: true, out var points, out _, out var shorterArcFallback);

        Assert.True(sampled);
        Assert.False(shorterArcFallback);
        Assert.True(points.Count >= 3);

        var angles = points
            .Select(p => NormalizeToZeroTwoPi(double.Atan2(p.Y, p.X)))
            .ToArray();

        for (var i = 1; i < angles.Length; i++)
        {
            Assert.True(angles[i] + 1e-8d >= angles[i - 1]);
        }

        var span = angles[^1] - angles[0];
        Assert.InRange(span, (double.Pi / 6d) - 0.05d, (double.Pi / 6d) + 0.05d);
        Assert.True(span < double.Pi);
    }

    [Fact]
    public void CircleTrim_ReversedSense_ReversesPointOrder()
    {
        var circle = new Circle3Curve(Point3D.Origin, Direction3D.Create(new Vector3D(0d, 0d, 1d)), 1d, Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        var start = circle.Evaluate(0d);
        var end = circle.Evaluate(double.Pi / 6d);

        var forwardOk = CurveSampler.TrySampleTrimmedCircleArc(circle, start, end, orientedEdgeSense: true, out var forward, out _, out _);
        var reverseOk = CurveSampler.TrySampleTrimmedCircleArc(circle, start, end, orientedEdgeSense: false, out var reversed, out _, out _);

        Assert.True(forwardOk);
        Assert.True(reverseOk);
        Assert.True(((forward[0] - reversed[0]).Length) < 1e-9d);
        Assert.True(forward[1].Y > 0d);
        Assert.True(reversed[1].Y < 0d);
    }

    [Fact]
    public void Step242_CircleTrimFixture_Tessellation_UsesShortArcsWithoutTrimDiagnostics()
    {
        var text = LoadFixture("testdata/step242/tessellation-robustness/planar-rect-with-filleted-corners.step");

        var import = Step242Importer.ImportBody(text);
        Assert.True(import.IsSuccess);

        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);
        Assert.True(tessellation.IsSuccess);
        Assert.DoesNotContain(tessellation.Diagnostics, d => string.Equals(d.Source, "Viewer.Tessellation.CircleTrimResolveFailed", StringComparison.Ordinal));

        var circularEdges = import.Value.Topology.Edges
            .Where(e => import.Value.TryGetEdgeCurveGeometry(e.Id, out var curve) && curve?.Kind == CurveGeometryKind.Circle3)
            .ToArray();

        Assert.NotEmpty(circularEdges);
        foreach (var edge in circularEdges)
        {
            var polyline = Assert.Single(tessellation.Value.EdgePolylines, p => p.EdgeId == edge.Id);
            Assert.True(polyline.Points.Count <= 20);
        }
    }


    [Fact]
    public void Step242_ToroidalFixture_ImportsAndTessellates()
    {
        var text = LoadFixture("testdata/step242/generated/v0-required/toroid.step");

        var import = Step242Importer.ImportBody(text);
        Assert.True(import.IsSuccess);
        Assert.Contains(import.Value.Geometry.Surfaces, s => s.Value.Kind == SurfaceGeometryKind.Torus);

        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);
        Assert.True(tessellation.IsSuccess);
        Assert.NotEmpty(tessellation.Value.FacePatches);
    }


    [Fact]
    public void Step242_HandcraftedSphereFixture_AcceptsVertexLoopFaceBound_AndImports()
    {
        var text = LoadFixture("testdata/step242/handcrafted/baseline/sphere.step");

        var import = Step242Importer.ImportBody(text);

        Assert.True(import.IsSuccess);
        Assert.Single(import.Value.Topology.Faces);
        var face = Assert.Single(import.Value.Topology.Faces);
        Assert.Empty(import.Value.GetLoopIds(face.Id));
    }

    [Fact]
    public void Step242_HandcraftedSphereFixture_AdvancesPastVertexLoopBlocker_AndTessellates()
    {
        var text = LoadFixture("testdata/step242/handcrafted/baseline/sphere.step");

        var import = Step242Importer.ImportBody(text);
        Assert.True(import.IsSuccess);

        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);

        Assert.True(tessellation.IsSuccess);
        Assert.DoesNotContain(tessellation.Diagnostics,
            d => d.Message.Contains("expected 'EDGE_LOOP' but found 'VERTEX_LOOP'", StringComparison.Ordinal));
        Assert.NotEmpty(tessellation.Value.FacePatches);
    }

    [Fact]
    public void Step242_HandcraftedToroidFixture_UsesCircularSeamTopology_AndTessellates()
    {
        var text = LoadFixture("testdata/step242/handcrafted/baseline/toroid.step");

        var import = Step242Importer.ImportBody(text);
        Assert.True(import.IsSuccess);

        var torusFace = Assert.Single(import.Value.Topology.Faces, face =>
        {
            Assert.True(import.Value.Bindings.TryGetFaceBinding(face.Id, out var binding));
            Assert.True(import.Value.Geometry.TryGetSurface(binding.SurfaceGeometryId, out var surface));
            return surface!.Kind == SurfaceGeometryKind.Torus;
        });

        var loopIds = import.Value.GetLoopIds(torusFace.Id);
        var loopId = Assert.Single(loopIds);
        var coedges = import.Value.GetCoedgeIds(loopId)
            .Select(id => import.Value.Topology.GetCoedge(id))
            .ToArray();

        Assert.Equal(4, coedges.Length);
        Assert.All(coedges, coedge => Assert.Equal(CurveGeometryKind.Circle3, import.Value.GetEdgeCurve(coedge.EdgeId).Kind));

        var seamUseCount = coedges.Count(coedge =>
        {
            var edge = import.Value.Topology.GetEdge(coedge.EdgeId);
            return edge.StartVertexId == edge.EndVertexId;
        });
        Assert.Equal(4, seamUseCount);
        Assert.Equal(2, coedges.Select(c => c.EdgeId).Distinct().Count());

        var tessellationA = BrepDisplayTessellator.Tessellate(import.Value);
        Assert.True(tessellationA.IsSuccess);
        Assert.DoesNotContain(tessellationA.Diagnostics,
            d => d.Message.Contains("two line seam uses and two circular trim uses", StringComparison.OrdinalIgnoreCase));

        var tessellationB = BrepDisplayTessellator.Tessellate(import.Value);
        Assert.True(tessellationB.IsSuccess);

        var patchA = Assert.Single(tessellationA.Value.FacePatches, p => p.FaceId == torusFace.Id);
        var patchB = Assert.Single(tessellationB.Value.FacePatches, p => p.FaceId == torusFace.Id);

        Assert.Equal(patchA.Positions.Count, patchB.Positions.Count);
        Assert.Equal(patchA.Normals.Count, patchB.Normals.Count);
        Assert.Equal(patchA.TriangleIndices.Count, patchB.TriangleIndices.Count);
        Assert.True(patchA.Positions.SequenceEqual(patchB.Positions));
        Assert.True(patchA.Normals.SequenceEqual(patchB.Normals));
        Assert.True(patchA.TriangleIndices.SequenceEqual(patchB.TriangleIndices));
    }



    [Fact]
    public void Step242_HandcraftedCylinderFixture_TessellatesSideAsPeriodicClosedGrid()
    {
        var text = LoadFixture("testdata/step242/handcrafted/baseline/cylinder.step");

        var import = Step242Importer.ImportBody(text);
        Assert.True(import.IsSuccess);

        var options = DisplayTessellationOptions.Create(double.Pi / 6d, 10d, minimumSegments: 12, maximumSegments: 12).Value;
        var tessellation = BrepDisplayTessellator.Tessellate(import.Value, options);
        Assert.True(tessellation.IsSuccess);

        var cylinderFace = Assert.Single(import.Value.Topology.Faces, face =>
        {
            Assert.True(import.Value.Bindings.TryGetFaceBinding(face.Id, out var binding));
            Assert.True(import.Value.Geometry.TryGetSurface(binding.SurfaceGeometryId, out var surface));
            return surface!.Kind == SurfaceGeometryKind.Cylinder;
        });

        var patch = Assert.Single(tessellation.Value.FacePatches, p => p.FaceId == cylinderFace.Id);
        Assert.Equal(26, patch.Positions.Count);
        Assert.Equal(26, patch.Normals.Count);
        Assert.Equal(72, patch.TriangleIndices.Count);

        const int rowWidth = 13;
        const int axialRows = 2;
        for (var row = 0; row < axialRows; row++)
        {
            var seamStart = patch.Positions[row * rowWidth];
            var seamEnd = patch.Positions[(row * rowWidth) + (rowWidth - 1)];
            Assert.True((seamStart - seamEnd).Length <= 1e-8d);
        }

        var cylinderSurface = import.Value.GetFaceSurface(cylinderFace.Id).Cylinder!.Value;
        var axis = cylinderSurface.Axis.ToVector();
        var sideAxials = patch.Positions
            .Select(point => (point - cylinderSurface.Origin).Dot(axis))
            .ToArray();
        var sideVMin = sideAxials.Min();
        var sideVMax = sideAxials.Max();

        var capPlanes = import.Value.Topology.Faces
            .Where(face => face.Id != cylinderFace.Id)
            .Select(face => import.Value.GetFaceSurface(face.Id))
            .Where(surface => surface.Kind == SurfaceGeometryKind.Plane)
            .Select(surface => surface.Plane!.Value)
            .ToArray();
        Assert.Equal(2, capPlanes.Length);

        var capAxials = capPlanes
            .Select(plane => (plane.Origin - cylinderSurface.Origin).Dot(axis))
            .OrderBy(v => v)
            .ToArray();

        Assert.True(double.Abs(capAxials[0] - sideVMin) <= 1e-8d);
        Assert.True(double.Abs(capAxials[1] - sideVMax) <= 1e-8d);

        Assert.All(patch.TriangleIndices, index => Assert.InRange(index, 0, patch.Positions.Count - 1));

        for (var i = 0; i < patch.TriangleIndices.Count; i += 3)
        {
            var p0 = patch.Positions[patch.TriangleIndices[i]];
            var p1 = patch.Positions[patch.TriangleIndices[i + 1]];
            var p2 = patch.Positions[patch.TriangleIndices[i + 2]];
            var area2 = (p1 - p0).Cross(p2 - p0).Length;
            Assert.True(area2 > 1e-9d);
        }
    }

    [Fact]
    public void Step242_HandcraftedCylinderFixture_Tessellation_IsDeterministic()
    {
        var text = LoadFixture("testdata/step242/handcrafted/baseline/cylinder.step");

        var import = Step242Importer.ImportBody(text);
        Assert.True(import.IsSuccess);

        var options = DisplayTessellationOptions.Create(double.Pi / 6d, 10d, minimumSegments: 12, maximumSegments: 12).Value;
        var tessellationA = BrepDisplayTessellator.Tessellate(import.Value, options);
        var tessellationB = BrepDisplayTessellator.Tessellate(import.Value, options);

        Assert.True(tessellationA.IsSuccess);
        Assert.True(tessellationB.IsSuccess);

        var cylinderFace = Assert.Single(import.Value.Topology.Faces, face =>
        {
            Assert.True(import.Value.Bindings.TryGetFaceBinding(face.Id, out var binding));
            Assert.True(import.Value.Geometry.TryGetSurface(binding.SurfaceGeometryId, out var surface));
            return surface!.Kind == SurfaceGeometryKind.Cylinder;
        });

        var patchA = Assert.Single(tessellationA.Value.FacePatches, p => p.FaceId == cylinderFace.Id);
        var patchB = Assert.Single(tessellationB.Value.FacePatches, p => p.FaceId == cylinderFace.Id);

        Assert.True(patchA.Positions.SequenceEqual(patchB.Positions));
        Assert.True(patchA.Normals.SequenceEqual(patchB.Normals));
        Assert.True(patchA.TriangleIndices.SequenceEqual(patchB.TriangleIndices));
    }

    [Fact]
    public void Step242_HandcraftedConeFixture_UsesThreeCoedgeSeamTopology_AndTessellates()
    {
        var text = LoadFixture("testdata/step242/handcrafted/baseline/cone.step");

        var import = Step242Importer.ImportBody(text);
        Assert.True(import.IsSuccess);

        var coneFace = Assert.Single(import.Value.Topology.Faces, face =>
        {
            Assert.True(import.Value.Bindings.TryGetFaceBinding(face.Id, out var binding));
            Assert.True(import.Value.Geometry.TryGetSurface(binding.SurfaceGeometryId, out var surface));
            return surface!.Kind == SurfaceGeometryKind.Cone;
        });

        var baseFace = Assert.Single(import.Value.Topology.Faces, face =>
        {
            Assert.True(import.Value.Bindings.TryGetFaceBinding(face.Id, out var binding));
            Assert.True(import.Value.Geometry.TryGetSurface(binding.SurfaceGeometryId, out var surface));
            return surface!.Kind == SurfaceGeometryKind.Plane;
        });

        Assert.True(import.Value.Bindings.TryGetFaceBinding(baseFace.Id, out var baseFaceBinding));
        Assert.True(import.Value.Geometry.TryGetSurface(baseFaceBinding.SurfaceGeometryId, out var baseFaceSurface));
        var basePlane = baseFaceSurface!.Plane!.Value;

        var loopIds = import.Value.GetLoopIds(coneFace.Id);
        var loopId = Assert.Single(loopIds);
        var coedges = import.Value.GetCoedgeIds(loopId)
            .Select(id => import.Value.Topology.GetCoedge(id))
            .ToArray();

        Assert.Equal(3, coedges.Length);
        Assert.Equal(2, coedges.Count(coedge => import.Value.GetEdgeCurve(coedge.EdgeId).Kind == CurveGeometryKind.Line3));
        Assert.Equal(1, coedges.Count(coedge => import.Value.GetEdgeCurve(coedge.EdgeId).Kind == CurveGeometryKind.Circle3));

        var seamUseCount = coedges.Count(coedge =>
        {
            var edge = import.Value.Topology.GetEdge(coedge.EdgeId);
            return edge.StartVertexId == edge.EndVertexId;
        });
        Assert.Equal(1, seamUseCount);

        var lineEdgeIds = coedges
            .Where(coedge => import.Value.GetEdgeCurve(coedge.EdgeId).Kind == CurveGeometryKind.Line3)
            .Select(coedge => coedge.EdgeId)
            .Distinct()
            .ToArray();
        Assert.Single(lineEdgeIds);

        var tessellationA = BrepDisplayTessellator.Tessellate(import.Value);
        Assert.True(tessellationA.IsSuccess);
        Assert.DoesNotContain(tessellationA.Diagnostics,
            d => d.Message.Contains("four-coedge torus/revolved loop layouts", StringComparison.OrdinalIgnoreCase));

        var tessellationB = BrepDisplayTessellator.Tessellate(import.Value);
        Assert.True(tessellationB.IsSuccess);

        var patchA = Assert.Single(tessellationA.Value.FacePatches, p => p.FaceId == coneFace.Id);
        var patchB = Assert.Single(tessellationB.Value.FacePatches, p => p.FaceId == coneFace.Id);

        Assert.Equal(patchA.Positions.Count, patchB.Positions.Count);
        Assert.Equal(patchA.Normals.Count, patchB.Normals.Count);
        Assert.Equal(patchA.TriangleIndices.Count, patchB.TriangleIndices.Count);
        Assert.True(patchA.Positions.SequenceEqual(patchB.Positions));
        Assert.True(patchA.Normals.SequenceEqual(patchB.Normals));
        Assert.True(patchA.TriangleIndices.SequenceEqual(patchB.TriangleIndices));

        var baseNormal = basePlane.Normal.ToVector();
        var signedDistances = patchA.Positions
            .Select(point => (point - basePlane.Origin).Dot(baseNormal))
            .ToArray();

        var minDistance = signedDistances.Min();
        var maxDistance = signedDistances.Max();

        Assert.True(minDistance >= -1e-8d || maxDistance <= 1e-8d);
        Assert.True(signedDistances.Count(distance => double.Abs(distance) <= 1e-8d) >= 8);
    }

    [Fact]
    public void Step242_NistFile_WithToroidalSurface_AdvancesPastToroidalBlocker()
    {
        var text = LoadFixture("testdata/step242/nist/FTC/nist_ftc_06_asme1_ap242-e2.stp");

        var import = Step242Importer.ImportBody(text);

        Assert.True(import.IsSuccess);
        Assert.DoesNotContain(import.Diagnostics,
            d => d.Message.Contains("TOROIDAL_SURFACE", StringComparison.OrdinalIgnoreCase));

        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);
        Assert.True(tessellation.IsSuccess);
        Assert.DoesNotContain(tessellation.Diagnostics,
            d => d.Message.Contains("TOROIDAL_SURFACE", StringComparison.OrdinalIgnoreCase));
    }

    private static double NormalizeToZeroTwoPi(double angle)
    {
        var twoPi = 2d * double.Pi;
        var normalized = angle % twoPi;
        if (normalized < 0d)
        {
            normalized += twoPi;
        }

        return normalized;
    }

    private static string LoadFixture(string relativePath)
    {
        var path = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(path, Encoding.UTF8);
    }

}
