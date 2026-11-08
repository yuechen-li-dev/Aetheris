using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Blends;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests.Integration;

/// <summary><c>EdgeFinish&lt;Fillet&gt; { target: AllEdges }</c> on a body the Box/Polyhedron stock route does not
/// admit: the body is built without the finish and every edge of the result is rounded, or the finish is refused
/// with what the rounding found.</summary>
public sealed class UniformRoundingRouteTests
{
    internal static string Prism(double radius, string extra = "") => $$"""
        Model Prism {
            units: mm
            Path Outline {
                start: Point2(0mm, 0mm)
                heading: 0deg
                Line South { length: 40mm }
                Line East { turn: 120deg; length: 40mm }
                Close West
            }
            Profile Section from Outline
            Struct Body { Extrude Stock { profile: Section; from: 0mm; to: 12mm } }
            Modify Body { EdgeFinish<Fillet> Round { target: AllEdges; radius: {{radius}}mm } }
        {{extra}}}
        """;

    [Fact]
    public void ExtrudedPrism_EveryEdgeAStripEveryVertexACorner()
    {
        var built = FirmamentBuildAndExport.CompileSource(Prism(2));
        Assert.True(built.IsSuccess, string.Join("\n", built.Diagnostics.Select(d => d.Message)));
        var body = built.Value.RuntimeBody!;
        Assert.True(BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true).IsSuccess);
        int Count(SurfaceGeometryKind kind) => body.Topology.Faces.Count(f => body.TryGetFaceSurfaceGeometry(f.Id, out var g) && g!.Kind == kind);
        Assert.Equal((5, 9, 6), (Count(SurfaceGeometryKind.Plane), Count(SurfaceGeometryKind.Cylinder), Count(SurfaceGeometryKind.Sphere)));
        var read = BlendRecognition.Recognize(body);
        Assert.All(read.Where(r => r.Role == BlendFaceRole.Strip), s => Assert.Equal(2, s.Blend!.Size, 9));
        Assert.Contains(built.Value.MaterializedFeatures!, r => r is { Host: "Body", Kind: "EdgeFinish", Name: "Round" });
        Assert.Equal("Prism", built.Value.DocumentName);

        // The exported STEP is the rounded body.
        var imported = Step242Importer.ImportBody(built.Value.StepText);
        Assert.True(imported.IsSuccess);
        Assert.Equal(20, imported.Value.Topology.Faces.Count());
        Assert.Equal(built.Value.StepText, FirmamentBuildAndExport.CompileSource(Prism(2)).Value.StepText);
    }

    [Fact]
    public void Assertions_AreCheckedOnTheRoundedBody()
    {
        var held = FirmamentBuildAndExport.CompileSource(Prism(2, "    Assert Topology Body { faces: 20; edges: 36; vertices: 18; shells: 1 }\n"));
        Assert.True(held.IsSuccess, string.Join("\n", held.Diagnostics.Select(d => d.Message)));
        var sharp = FirmamentBuildAndExport.CompileSource(Prism(2, "    Assert Topology Body { faces: 5; edges: 9; vertices: 6; shells: 1 }\n"));
        Assert.False(sharp.IsSuccess, "sharp counts held on the rounded body");
        Assert.Contains(sharp.Diagnostics, d => d.Message.StartsWith("firmament-v2-assert-topology-failed", StringComparison.Ordinal));
    }

    [Fact]
    public void RadiusTooLarge_NamesTheLargestThatFits()
    {
        var refused = FirmamentBuildAndExport.CompileSource(Prism(30));
        var message = Assert.Single(refused.Diagnostics).Message;
        Assert.StartsWith(UniformRoundingRoute.Code + ": target: AllEdges (fillet every edge) on 'Round'", message);
        Assert.Contains("uniform-rounding-radius-too-large", message);
        Assert.Matches(@"largest that fits is under 6(\.0+)? mm|largest that fits is under 5\.99", message);
        Assert.NotNull(refused.Diagnostics.Single().SourceSpan);
    }

    /// <summary>An L-shaped extrusion: its inner vertical edge is concave and meets the convex top and bottom edges.
    /// The concave edge is filled first (fillets before rounds): its strip ends square on the top and bottom, whose
    /// new arcs round to tori running on into the neighbouring strips. Every blend reads back at the radius.</summary>
    [Fact]
    public void MixedCorner_FilledThenRounded()
    {
        var built = FirmamentBuildAndExport.CompileSource(File.ReadAllText(Path.Combine(Root(), "fixtures/Regression/UniformRounding/ell-rounded.firmament")));
        Assert.True(built.IsSuccess, string.Join("\n", built.Diagnostics.Select(d => d.Message)));
        var body = Step242Importer.ImportBody(built.Value.StepText).Value;
        int Count(SurfaceGeometryKind kind) => body.Topology.Faces.Count(f => body.TryGetFaceSurfaceGeometry(f.Id, out var g) && g!.Kind == kind);
        Assert.Equal((8, 18, 2, 10), (Count(SurfaceGeometryKind.Plane), Count(SurfaceGeometryKind.Cylinder), Count(SurfaceGeometryKind.Torus), Count(SurfaceGeometryKind.Sphere)));
        var read = BlendRecognition.Recognize(body);
        Assert.Equal(30, read.Count(r => r.Role is BlendFaceRole.Strip or BlendFaceRole.Joiner));
        Assert.All(read.Where(r => r.Role == BlendFaceRole.Strip), b => Assert.Equal(1, b.Blend!.Size, 9));
        // The concave fill is the only concave strip; its tori are major radius 2r around its axis.
        Assert.All(body.Topology.Faces.Select(f => body.TryGetFaceSurfaceGeometry(f.Id, out var g) ? g!.Torus : null).Where(t => t is not null), t => Assert.Equal(2, t!.Value.MajorRadius, 9));
    }

    /// <summary>A boss on a plate: at each foot corner two concave edges meet a convex one. Filling the concave
    /// edges first would leave the convex edge between two fills; refused, naming the corner.</summary>
    [Fact]
    public void TwoConcaveAndAConvex_RefusesNamingTheVertex()
    {
        var message = Assert.Single(FirmamentBuildAndExport.CompileSource(File.ReadAllText(Path.Combine(Root(), "fixtures/Regression/UniformRounding/boss-mixed-corner.firmament"))).Diagnostics).Message;
        Assert.Contains("uniform-rounding-mixed-vertex-unsupported", message);
        Assert.Contains("two concave edges and a convex one meet", message);
        Assert.Matches(@"vertex \d+ joins edges \d+ \((convex|concave)\), \d+ \((convex|concave)\), \d+ \((convex|concave)\)", message);
    }

    /// <summary>A plate with a pocket, built by composition, has each side wall split at the pocket's depth into two
    /// faces on one plane. Refused by name rather than rounded along the split.</summary>
    [Fact]
    public void SplitFace_RefusesByName()
    {
        var message = Assert.Single(FirmamentBuildAndExport.CompileSource(File.ReadAllText(Path.Combine(Root(), "fixtures/Regression/UniformRounding/pocket-split-face.firmament"))).Diagnostics).Message;
        Assert.Contains("uniform-rounding-split-face-unsupported", message);
        Assert.Contains("lie on one plane", message);
    }

    /// <summary>A blind hole's floor rounds to a concave torus around the hole's axis with major radius R - r: it
    /// needs R - r > r, so a 8 mm hole takes radii under 2 mm.</summary>
    [Fact]
    public void BlindHoleFloor_RefusesARadiusThatReachesTheAxis()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "fixtures/Regression/UniformRounding/blind-hole-rounded.firmament")).Replace("radius: 0.5mm", "radius: 2.5mm");
        var message = Assert.Single(FirmamentBuildAndExport.CompileSource(source).Diagnostics).Message;
        Assert.Contains("uniform-rounding-radius-too-large", message);
        Assert.Contains("reaches the circle's axis", message);
        var fit = double.Parse(System.Text.RegularExpressions.Regex.Match(message, @"fits is under ([0-9.Ee+-]+) mm").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(2, fit, 3);
        Assert.True(FirmamentBuildAndExport.CompileSource(source.Replace("radius: 2.5mm", "radius: 1.9mm")).IsSuccess);
    }

    /// <summary>Bodies with circular edges: every rim a torus, every line edge a cylinder, every three-edge corner a
    /// sphere, read back by the rolling-ball certificate at the radius, exported and imported again.</summary>
    [Theory]
    [InlineData("cylinder-rounded", 2, 2, 1, 2, 0, 0, 2)]
    [InlineData("frustum-rounded", 1.5, 2, 0, 2, 0, 1, 2)]
    [InlineData("box-hole-rounded", 1, 6, 13, 2, 8, 0, 14)]
    [InlineData("d-profile-rounded", 1, 3, 5, 2, 4, 0, 6)]
    // Concave edges too: the bushing's shoulder corner, the counterbore's step, the blind hole's floor.
    [InlineData("bushing-rounded", 1, 3, 3, 6, 0, 0, 6)]
    [InlineData("counterbore-rounded", 1, 7, 14, 4, 8, 0, 16)]
    [InlineData("blind-hole-rounded", 0.5, 7, 13, 2, 8, 0, 14)]
    // Strips running on into each other where flat and curved walls meet tangentially.
    [InlineData("rounded-rect-rounded", 1, 6, 12, 8, 0, 0, 16)]
    public void CircularEdges_RoundToTori(string name, double radius, int planes, int cylinders, int tori, int spheres, int cones, int edges)
    {
        var built = FirmamentBuildAndExport.CompileSource(File.ReadAllText(Path.Combine(Root(), "fixtures/Regression/UniformRounding", name + ".firmament")));
        Assert.True(built.IsSuccess, string.Join("\n", built.Diagnostics.Select(d => d.Message)));
        var body = Step242Importer.ImportBody(built.Value.StepText).Value;
        Assert.True(BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true).IsSuccess);
        int Count(SurfaceGeometryKind kind) => body.Topology.Faces.Count(f => body.TryGetFaceSurfaceGeometry(f.Id, out var g) && g!.Kind == kind);
        Assert.Equal((planes, cylinders, tori, spheres, cones),
            (Count(SurfaceGeometryKind.Plane), Count(SurfaceGeometryKind.Cylinder), Count(SurfaceGeometryKind.Torus), Count(SurfaceGeometryKind.Sphere), Count(SurfaceGeometryKind.Cone)));
        var read = BlendRecognition.Recognize(body);
        var strips = read.Where(r => r.Role == BlendFaceRole.Strip).ToArray();
        Assert.Equal(edges, strips.Length); // one per edge of the body before rounding
        Assert.All(strips, s => Assert.Equal(radius, s.Blend!.Size, 9));
        Assert.Equal(spheres, read.Count(r => r.Role == BlendFaceRole.Joiner));
        Assert.True(BrepMassProperties.Evaluate(body).AbsoluteVolume > 0);
    }

    /// <summary>A bore 2 mm from a wall: the bore's rim torus and the wall's edge strip meet at a 1 mm radius, so
    /// 1 mm is refused (it would pinch the top face to nothing) and the reported fit is under 1 mm.</summary>
    [Fact]
    public void BoreNearAWall_RefusesTheRadiusThatPinchesTheTop()
    {
        string Source(double r) => $"Model Near {{ units: mm\n Box stock {{ size: [20mm, 20mm, 20mm] }}\n Modify stock {{ Hole<Shaft> Bore {{ on: +Z; center: [5mm, 0mm]; diameter: 6mm; end: ThroughAll }} EdgeFinish<Fillet> Round {{ target: AllEdges; radius: {r}mm }} }} }}";
        var message = Assert.Single(FirmamentBuildAndExport.CompileSource(Source(1)).Diagnostics).Message;
        Assert.Contains("uniform-rounding-radius-too-large", message);
        Assert.Contains("reaches face", message);
        Assert.Matches(@"largest that fits is under (1|0\.99\d*) mm", message);
        var fits = FirmamentBuildAndExport.CompileSource(Source(0.9));
        Assert.True(fits.IsSuccess, string.Join("\n", fits.Diagnostics.Select(d => d.Message)));
        Assert.True(BrepMassProperties.Evaluate(Step242Importer.ImportBody(fits.Value.StepText).Value).AbsoluteVolume > 0);
    }

    /// <summary>The diagnostic an LLM or a person feeds back: the body has splines, how many, and which.</summary>
    [Fact]
    public void SplineEdges_RefuseByNameWithCountsAndIds()
    {
        const string source = """
            Model NistRound {
                units: mm
                InlineStep Source {
                    path: "../../../../testdata/step242/nist/FTC/nist_ftc_09_asme1_ap242-e1.stp"
                    textSha256: "daad981598736d2ee6a104a7213b8d7813deaaabd7f683bc8cf8bbbf401a579a"
                    geometryOnly: true
                }
                Modify Source { EdgeFinish<Fillet> Round { target: AllEdges; radius: 1mm } }
            }
            """;
        var refused = FirmamentBuildAndExport.CompileSource(source, Path.Combine(Root(), "fixtures/Regression/InlineStep/production"));
        var message = Assert.Single(refused.Diagnostics).Message;
        Assert.Contains("uniform-rounding-spline-edge-unsupported", message);
        Assert.Contains("does not support spline or elliptical edges yet", message);
        Assert.Matches(@"\d+ spline edges \(\d+(, \d+)*(, \.\.\.)?\)", message);
    }

    [Fact]
    public void Validate_AgreesWithBuild()
    {
        var parse = FirmamentV2Parser.Parse(Prism(2));
        var report = FirmamentV2ValidationReportBuilder.Build(parse, "prism.firmament", null, null, Prism(2));
        Assert.Equal("invalid", report.Status);
        var admitted = FirmamentBuildAdmissionValidation.Admit(report, Prism(2));
        Assert.Equal(("valid", "Built"), (admitted.Status, admitted.ValidationLevel));
        var refused = FirmamentBuildAdmissionValidation.Admit(FirmamentV2ValidationReportBuilder.Build(FirmamentV2Parser.Parse(Prism(30)), "p.firmament", null, null, Prism(30)), Prism(30));
        Assert.Equal("invalid", refused.Status);
        Assert.Contains(refused.Diagnostics, d => d.Code == UniformRoundingRoute.Code && d.Message.Contains("radius-too-large", StringComparison.Ordinal));
    }

    /// <summary>Every fixture across radii from far below to beyond its limit: each result is either a body that
    /// meshes watertight and outward with every strip at the radius, or a named refusal whose reported fit, taken
    /// just under, builds.</summary>
    [Theory]
    [InlineData("cylinder-rounded")]
    [InlineData("frustum-rounded")]
    [InlineData("box-hole-rounded")]
    [InlineData("d-profile-rounded")]
    [InlineData("bushing-rounded")]
    [InlineData("counterbore-rounded")]
    [InlineData("blind-hole-rounded")]
    [InlineData("rounded-rect-rounded")]
    [InlineData("ell-rounded")]
    public void RadiusSweep_EitherSoundOrRefusedWithAFitThatBuilds(string name)
    {
        var source = File.ReadAllText(Path.Combine(Root(), "fixtures/Regression/UniformRounding", name + ".firmament"));
        string With(double r) => System.Text.RegularExpressions.Regex.Replace(source, @"(target: AllEdges; radius: )[0-9.]+mm", m => m.Groups[1].Value + r.ToString(System.Globalization.CultureInfo.InvariantCulture) + "mm");
        foreach (var r in new[] { 0.02, 0.5, 1.5, 3, 6 })
        {
            var built = FirmamentBuildAndExport.CompileSource(With(r));
            if (!built.IsSuccess)
            {
                var message = Assert.Single(built.Diagnostics).Message;
                Assert.Matches("uniform-rounding-[a-z-]+", message);
                var fit = System.Text.RegularExpressions.Regex.Match(message, @"fits is under ([0-9.Ee+-]+) mm");
                if (!fit.Success) continue;
                var limit = double.Parse(fit.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                Assert.True(limit <= r, message);
                Assert.True(FirmamentBuildAndExport.CompileSource(With(limit * 0.98)).IsSuccess, $"{limit * 0.98} should fit: {message}");
                continue;
            }
            var body = Step242Importer.ImportBody(built.Value.StepText).Value;
            Assert.All(body.Topology.Coedges.GroupBy(c => c.EdgeId), g => Assert.True(g.Count() == 2 && g.First().IsReversed != g.Last().IsReversed));
            Assert.True(Aetheris.Kernel.Core.Brep.Tessellation.SurfaceMeshIrTessellator.TryBuild(body,
                Aetheris.Kernel.Core.Brep.Tessellation.SurfaceMeshPolicy.FromDisplayOptions(Aetheris.Kernel.Core.Brep.Tessellation.DisplayTessellationOptions.Default), out var document, out var failure), $"r={r}: {failure}");
            Assert.True(Aetheris.Kernel.Core.Brep.Tessellation.SurfaceMeshIrTessellator.TryLowerToTriangleMesh(document, out _, out var mesh));
            Assert.True(mesh.IsWatertight && mesh.IsOutwardOriented && mesh.SignedVolume > 0, $"r={r}: watertight={mesh.IsWatertight} outward={mesh.IsOutwardOriented}");
            Assert.All(BlendRecognition.Recognize(body).Where(x => x.Role == BlendFaceRole.Strip), x => Assert.Equal(r, x.Blend!.Size, 6));
        }
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aetheris.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root.");
    }
}
