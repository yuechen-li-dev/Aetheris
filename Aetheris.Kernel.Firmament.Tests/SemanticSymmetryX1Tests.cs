using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Xunit;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SemanticSymmetryX1Tests
{
    [Fact]
    public void Point_vector_axis_plane_and_frame_reflect_analytically()
    {
        var mirror = new SemanticSymmetryTransform.Plane(new(0, 0, 0), Direction3D.Create(new(1, 0, 0)));
        Assert.Equal(new Point3D(-3, 4, 5), SemanticSymmetryTransform.Reflect(new Point3D(3, 4, 5), mirror));
        Assert.Equal(new Vector3D(-3, 4, 5), SemanticSymmetryTransform.Reflect(new Vector3D(3, 4, 5), mirror));

        var axis = SemanticSymmetryTransform.Reflect(new SemanticSymmetryTransform.Axis(new(2, 1, 0), Direction3D.Create(new(1, 1, 0))), mirror);
        Assert.Equal(-2, axis.Origin.X, 12);
        Assert.Equal(-1 / Math.Sqrt(2), axis.Direction.X, 12);
        var plane = SemanticSymmetryTransform.Reflect(new SemanticSymmetryTransform.Plane(new(2, 0, 0), Direction3D.Create(new(1, 1, 0))), mirror);
        Assert.Equal(-2, plane.Origin.X, 12);
        Assert.Equal(-1 / Math.Sqrt(2), plane.Normal.X, 12);

        var frame = new SemanticSymmetryTransform.Frame(new(2, 0, 0), Direction3D.Create(new(1, 0, 0)), Direction3D.Create(new(0, 1, 0)), Direction3D.Create(new(0, 0, 1)));
        var naive = SemanticSymmetryTransform.Reflect(frame.X.ToVector(), mirror).Cross(SemanticSymmetryTransform.Reflect(frame.Y.ToVector(), mirror)).Dot(SemanticSymmetryTransform.Reflect(frame.Z.ToVector(), mirror));
        Assert.Equal(-1, naive, 12);
        var reflected = SemanticSymmetryTransform.Reflect(frame, mirror);
        Assert.Equal(1, reflected.Determinant, 12);
        var restored = SemanticSymmetryTransform.Reflect(reflected, mirror);
        Assert.Equal(frame.Origin, restored.Origin);
        Assert.Equal(1, restored.Determinant, 12);
    }

    [Fact]
    public void Mirrored_profile_reverses_reflected_traversal_and_retains_member_provenance()
    {
        var diagnostics = new List<string>();
        var expansion = Assert.IsType<SemanticSymmetryAuthoring.Result>(SemanticSymmetryAuthoring.Expand(ProfileSource, diagnostics));
        Assert.Empty(diagnostics);
        var profile = ProfileAuthoringParser.ResolveNamedProfile(expansion.Source, "RightCutout", out var profileDiagnostics);
        Assert.NotNull(profile);
        Assert.DoesNotContain(profileDiagnostics, item => item.Contains("invalid", StringComparison.OrdinalIgnoreCase));
        var points = profile!.Loops.Single().Segments.Select(segment => segment.Geometry).OfType<Aetheris.Kernel.Firmament.Materializer.LineArcLineSegment2D>().SelectMany(line => new[] { line.Start, line.End }).ToArray();
        Assert.Equal(-8, points.Min(point => point.X), 10);
        Assert.Equal(-2, points.Max(point => point.X), 10);
        Assert.Single(expansion.Mirrors);
        Assert.All(expansion.Mirrors[0].Members, member => Assert.StartsWith("LeftCutout.", member.SourceIdentity));
    }

    [Fact]
    public void Mirror_twice_recovers_profile_geometry()
    {
        var source = ProfileSource.Replace("Mirrored Profile RightCutout From LeftCutout { Across: CenterPlane }",
            "Mirrored Profile RightCutout From LeftCutout { Across: CenterPlane }\nMirrored Profile Restored From RightCutout { Across: CenterPlane }");
        var diagnostics = new List<string>();
        var first = SemanticSymmetryAuthoring.Expand(source, diagnostics);
        Assert.NotNull(first);
        Assert.Empty(diagnostics);
        var original = ProfileAuthoringParser.ResolveNamedProfile(first!.Source, "LeftCutout", out _)!;
        var restored = ProfileAuthoringParser.ResolveNamedProfile(first.Source, "Restored", out _)!;
        var originalPoints = original.Loops.Single().Segments.Select(segment => segment.Geometry).OfType<Aetheris.Kernel.Firmament.Materializer.LineArcLineSegment2D>().Select(line => line.Start).ToArray();
        var restoredPoints = restored.Loops.Single().Segments.Select(segment => segment.Geometry).OfType<Aetheris.Kernel.Firmament.Materializer.LineArcLineSegment2D>().Select(line => line.Start).ToArray();
        Assert.Equal(originalPoints.OrderBy(point => point.X).ThenBy(point => point.Y), restoredPoints.OrderBy(point => point.X).ThenBy(point => point.Y));
    }

    [Fact]
    public void Mirrored_arc_preserves_locus_and_canonical_loop_traversal()
    {
        var diagnostics = new List<string>();
        var expansion = SemanticSymmetryAuthoring.Expand(ArcProfileSource, diagnostics);
        Assert.NotNull(expansion);
        Assert.Empty(diagnostics);
        var mirrored = ProfileAuthoringParser.ResolveNamedProfile(expansion!.Source, "RightD", out var profileDiagnostics);
        Assert.NotNull(mirrored);
        Assert.Empty(profileDiagnostics);
        var arc = Assert.Single(mirrored!.Loops.Single().Segments.Select(segment => segment.Geometry).OfType<Aetheris.Kernel.Firmament.Materializer.LineArcCircularArc2D>());
        Assert.Equal(-5, arc.Center.X, 10);
        Assert.Equal(5, arc.Radius, 10);
        Assert.Equal(Math.PI, Math.Abs(arc.SweepAngleRadians), 10);
    }

    [Fact]
    public void Full_and_partial_radial_distributions_are_deterministic()
    {
        var fullDiagnostics = new List<string>();
        var full = Assert.IsType<SemanticSymmetryAuthoring.Result>(SemanticSymmetryAuthoring.Expand(RadialSource("6", "full"), fullDiagnostics));
        Assert.Empty(fullDiagnostics);
        var pattern = Assert.Single(full.RadialPatterns);
        Assert.Equal(Enumerable.Range(0, 6).Select(i => i * Math.PI / 3), pattern.Instances.Select(instance => instance.RotationAngleRadians), new DoubleComparer());
        Assert.Equal(6, RegexCount(full.Source, @"Hole\s*<\s*Shaft\s*>\s+BoltCircle_Instance"));

        var partialDiagnostics = new List<string>();
        var partial = Assert.IsType<SemanticSymmetryAuthoring.Result>(SemanticSymmetryAuthoring.Expand(RadialSource("5", "half"), partialDiagnostics));
        Assert.Empty(partialDiagnostics);
        Assert.Equal(new[] { 0d, Math.PI / 4, Math.PI / 2, 3 * Math.PI / 4, Math.PI }, partial.RadialPatterns.Single().Instances.Select(instance => instance.RotationAngleRadians), new DoubleComparer());

        var singleDiagnostics = new List<string>();
        var single = SemanticSymmetryAuthoring.Expand(RadialSource("1", "full"), singleDiagnostics);
        Assert.NotNull(single);
        Assert.Empty(singleDiagnostics);
        Assert.Equal(0, Assert.Single(single!.RadialPatterns.Single().Instances).RotationAngleRadians);
    }

    [Fact]
    public void Mirror_then_rotate_is_not_reordered()
    {
        var mirror = new SemanticSymmetryTransform.Plane(new(0, 0, 0), Direction3D.Create(new(1, 0, 0)));
        var axis = new SemanticSymmetryTransform.Axis(new(0, 0, 0), Direction3D.Create(new(0, 0, 1)));
        var point = new Point3D(3, 4, 0);
        var mirrorThenRotate = SemanticSymmetryTransform.Rotate(SemanticSymmetryTransform.Reflect(point, mirror), axis, Math.PI / 2);
        var rotateThenMirror = SemanticSymmetryTransform.Reflect(SemanticSymmetryTransform.Rotate(point, axis, Math.PI / 2), mirror);
        Assert.NotEqual(mirrorThenRotate, rotateThenMirror);
    }

    [Fact]
    public void Coincident_feature_mirror_is_rejected_before_materialization()
    {
        var source = ArcProfileSource.Replace("Mirrored Profile RightD From LeftD { Across: CenterPlane }",
            "Hole<Shaft> Centered { On: +Z; Center: Point2(0mm,0mm); Diameter: 4mm; End: ThroughAll }\nMirrored Feature Duplicate From Centered { Across: CenterPlane }");
        var diagnostics = new List<string>();
        Assert.Null(SemanticSymmetryAuthoring.Expand(source, diagnostics));
        Assert.Contains("firmament-symmetry-redundant-feature-mirror:Duplicate", diagnostics);
    }

    [Theory]
    [InlineData("0", "full", "firmament-symmetry-radial-count-invalid")]
    [InlineData("6", "nonsense", "firmament-symmetry-radial-angle-invalid")]
    public void Invalid_radial_inputs_fail_semantically(string count, string angle, string expected)
    {
        var diagnostics = new List<string>();
        Assert.Null(SemanticSymmetryAuthoring.Expand(RadialSource(count, angle), diagnostics));
        Assert.Contains(diagnostics, item => item.StartsWith(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void Unsupported_mirror_type_fails_typed()
    {
        var diagnostics = new List<string>();
        Assert.Null(SemanticSymmetryAuthoring.Expand("Mirrored Material Right From Left { Across: CenterPlane }", diagnostics));
        Assert.Contains("firmament-symmetry-type-unsupported:Material", diagnostics);
    }

    [Theory]
    [InlineData("fixtures/Canonical/Symmetry/mirrored-hole.firmament")]
    [InlineData("fixtures/Canonical/Symmetry/mirrored-profile.firmament")]
    [InlineData("fixtures/Canonical/Symmetry/radial-bolt-circle.firmament")]
    [InlineData("fixtures/Canonical/Symmetry/mirrored-feature-with-override.firmament")]
    public void Canonical_feature_witnesses_parse_through_production_route(string path)
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), path));
        var expanded = FirmamentV2Parser.TryExpandCanonicalStaticAuthoring(source, out var normalized, out var expansionDiagnostics);
        Assert.True(expanded, string.Join(Environment.NewLine, expansionDiagnostics) + Environment.NewLine + normalized);
        var parsed = FirmamentV2Parser.Parse(source[source.IndexOf("Model ", StringComparison.Ordinal)..]);
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics) + Environment.NewLine + normalized);
    }

    [Fact]
    public void Profile_and_revolve_witnesses_use_the_strongest_closed_boundary_semantics()
    {
        var profile = File.ReadAllText(Path.Combine(RepositoryRoot(), "fixtures/Canonical/Symmetry/mirrored-profile.firmament"));
        Assert.Contains("Triangle2<Explicit> LeftBoundary", profile, StringComparison.Ordinal);
        Assert.Contains("LeftBoundary |> TraceLoop", profile, StringComparison.Ordinal);
        Assert.DoesNotContain("Line2 AB", profile, StringComparison.Ordinal);

        var revolve = File.ReadAllText(Path.Combine(RepositoryRoot(), "fixtures/Canonical/Symmetry/mirrored-revolve.firmament"));
        Assert.Contains("Rect2 LowerBoundary", revolve, StringComparison.Ordinal);
        Assert.Contains("LowerBoundary |> TraceLoop", revolve, StringComparison.Ordinal);
        Assert.DoesNotContain("Line2 AB", revolve, StringComparison.Ordinal);
    }

    private const string ProfileSource = """
        Model MirrorProfile {
          Units: mm
          Concept Struct Supports { Center: Plane { Origin: [0mm,0mm,0mm]; Normal: [1,0,0]; Up: [0,0,1] } }
          Construction Plane CenterPlane { Trace: Supports.Center }
          Point2 A { Position: [2mm, 0mm] }
          Point2 B { Position: [8mm, 0mm] }
          Point2 C { Position: [5mm, 6mm] }
          Line2 AB { From: A; To: B }
          Line2 BC { From: B; To: C }
          Line2 CA { From: C; To: A }
          Profile LeftCutout {
            Loop Outer {
              Segment Bottom { Trace: AB; From: A; To: B }
              Segment Diagonal { Trace: BC; From: B; To: C }
              Segment Return { Trace: CA; From: C; To: A }
            }
          }
          Mirrored Profile RightCutout From LeftCutout { Across: CenterPlane }
        }
        """;

    private const string ArcProfileSource = """
        Model ArcMirror {
          Units: mm
          Plane CenterPlane { Origin: [0mm,0mm,0mm]; Normal: [1,0,0]; Up: [0,0,1] }
          Point2 East { Position: [10mm, 0mm] }
          Point2 West { Position: [0mm, 0mm] }
          Point2 Center { Position: [5mm, 0mm] }
          Circle2 Rim { Center: Center; Radius: 5mm }
          Line2 Diameter { From: West; To: East }
          Profile LeftD { Loop Outer {
            Segment RimArc { Trace: Rim; From: East; To: West; Sweep: CounterClockwise }
            Segment Closure { Trace: Diameter; From: West; To: East }
          } }
          Mirrored Profile RightD From LeftD { Across: CenterPlane }
        }
        """;

    private static string RadialSource(string count, string angle) => $$"""
        Model Radial {
          Units: mm
          Axis MainAxis { Origin: [0mm,0mm,0mm]; Direction: [0,0,1] }
          Hole<Shaft> Seed { On: +Z Center: Point2(20mm, 0mm) Diameter: 4mm End: ThroughAll }
          Radial Pattern BoltCircle { Source: Seed About: MainAxis Count: {{count}} Angle: {{angle}} }
        }
        """;

    private static int RegexCount(string source, string pattern) => System.Text.RegularExpressions.Regex.Matches(source, pattern).Count;
    private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    private sealed class DoubleComparer : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) < 1e-12;
        public int GetHashCode(double obj) => 0;
    }
}
