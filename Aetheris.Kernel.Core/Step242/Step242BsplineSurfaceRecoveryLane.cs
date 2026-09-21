using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Step242;

/// <summary>
/// Recovers the analytic surface a vendor exporter encoded as a B-spline.
/// <para>
/// Aetheris keeps an analytic B-rep, so a spline face that is really a cylinder, cone, sphere, torus or plane must be
/// imported as that primitive; otherwise every later stage (trim evaluation, display, export, preflight) pays for a
/// representation the part never needed, and the file round-trips as NURBS. Exporters encode analytic geometry the
/// same way every time: one parametric direction is an exact rational circular section, the other sweeps it.
/// </para>
/// <para>
/// What separates an encoded primitive from a free-form blend that merely resembles one is exactness, not closeness.
/// Measured across the corpus, encoded primitives reproduce their sections to about 1e-14 of the part size while
/// genuine blends manage only about 1e-5 - eight orders of magnitude apart - so the section gate is set far below any
/// modelling tolerance. A candidate derived from the section family is then verified against the spline it would
/// replace, at the accuracy the source file itself declares, before it is accepted.
/// </para>
/// </summary>
internal static class Step242BsplineSurfaceRecoveryLane
{
    private const string AnalyticCylinderCandidate = "analytic_cylinder";
    private const string AnalyticConeCandidate = "analytic_cone";
    private const string AnalyticSphereCandidate = "analytic_sphere";
    private const string AnalyticTorusCandidate = "analytic_torus";
    private const string RejectCandidate = "reject";

    /// <summary>Samples taken along one candidate circular section.</summary>
    private const int SectionSampleCount = 9;

    /// <summary>Sections taken across the sweep direction.</summary>
    private const int SectionCount = 7;

    /// <summary>Grid resolution used to verify a recovered surface against the spline it replaces.</summary>
    private const int VerificationSamples = 13;

    /// <summary>Relative exactness a section must reach to count as an encoded circle rather than a blend.</summary>
    private const double SectionExactnessRelativeTolerance = 1e-9d;

    /// <summary>Ceiling on a file-declared accuracy, so one sloppy header cannot admit a wrong surface.</summary>
    private const double DeclaredAccuracyRelativeCeiling = 1e-3d;

    /// <summary>Model tolerance used when the source file declares no distance accuracy.</summary>
    private const double UndeclaredAccuracyRelativeTolerance = 1e-6d;

    private const double MinimumTolerance = 1e-12d;

    private const string NoSectionsReason = "spline sweeps no exact circular section.";

    /// <summary>A radius slope below this is a constant-radius sweep, which belongs to the cylinder candidate.</summary>
    private const double ConeMinimumRadiusSlope = 1e-9d;

    /// <summary>Fixed power iterations used to fit the sweep axis; the centres are near-collinear, so this converges at once.</summary>
    private const int AxisFitIterations = 32;

    internal readonly record struct RecoveryDecision(
        string CandidateName,
        SurfaceGeometry? RecoveredSurface,
        string Reason);

    /// <summary>
    /// Decides whether <paramref name="surface"/> is an encoded analytic primitive.
    /// <paramref name="declaredAccuracyMillimetres"/> is the source file's own distance accuracy when it states one.
    /// </summary>
    public static RecoveryDecision Decide(BSplineSurfaceWithKnots surface, double? declaredAccuracyMillimetres = null)
    {
        var context = RecoveryContext.Create(surface, declaredAccuracyMillimetres);
        var engine = new JudgmentEngine<RecoveryContext>();
        var judgment = engine.Evaluate(context, BuildCandidates());

        if (judgment.Selection is { } selection)
        {
            var probe = context.Select(selection.Candidate.Name);
            return new RecoveryDecision(selection.Candidate.Name, probe.Surface, probe.Reason);
        }

        // Every candidate carries why it was not admissible; that list is the whole account of the decision, so it is
        // reported verbatim rather than collapsed into "unsupported".
        var reasons = judgment.Rejections
            .Select(rejection => $"{rejection.CandidateName}: {rejection.Reason}")
            .ToArray();
        return new RecoveryDecision(
            RejectCandidate,
            null,
            reasons.Length == 0 ? "No analytic recovery candidate was admissible." : string.Join(" ", reasons));
    }

    private static IReadOnlyList<JudgmentCandidate<RecoveryContext>> BuildCandidates()
    {
        // Scores descend with representational complexity so the simplest primitive that verifies wins.
        return
        [
            Candidate(AnalyticCylinderCandidate, context => context.Cylinder, 103d, 0),
            Candidate(AnalyticConeCandidate, context => context.Cone, 102d, 1),
            Candidate(AnalyticSphereCandidate, context => context.Sphere, 101d, 2),
            Candidate(AnalyticTorusCandidate, context => context.Torus, 100d, 3)
        ];
    }

    private static JudgmentCandidate<RecoveryContext> Candidate(
        string name,
        Func<RecoveryContext, SurfaceProbe> select,
        double score,
        int priority)
        => new(
            Name: name,
            IsAdmissible: context => select(context).IsAdmissible,
            Score: _ => score,
            RejectionReason: context => select(context).Reason,
            TieBreakerPriority: priority);

    private readonly record struct SurfaceProbe(SurfaceGeometry? Surface, double Deviation, string Reason)
    {
        public bool IsAdmissible => Surface is not null;

        public static SurfaceProbe Rejected(string reason) => new(null, double.NaN, reason);
    }

    private readonly record struct CircularSection(Point3D Center, double Radius, Direction3D Normal, Point3D Start);

    private readonly record struct RecoveryContext(
        SurfaceProbe Cylinder,
        SurfaceProbe Cone,
        SurfaceProbe Sphere,
        SurfaceProbe Torus)
    {
        public SurfaceProbe Select(string candidateName) => candidateName switch
        {
            AnalyticCylinderCandidate => Cylinder,
            AnalyticConeCandidate => Cone,
            AnalyticSphereCandidate => Sphere,
            AnalyticTorusCandidate => Torus,
            _ => SurfaceProbe.Rejected($"Unknown candidate '{candidateName}'.")
        };

        public static RecoveryContext Create(BSplineSurfaceWithKnots surface, double? declaredAccuracyMillimetres)
        {
            var diagonal = ControlNetDiagonal(surface);
            var exactness = double.Max(diagonal * SectionExactnessRelativeTolerance, MinimumTolerance);
            var model = ResolveModelTolerance(diagonal, declaredAccuracyMillimetres);
            // Either parametric direction can carry the swept circle, and on a sphere or torus both do. Which one is
            // useful depends on the primitive - a torus is only recognisable from its minor sections - so every
            // family that passes the exactness gate is probed and the first primitive that verifies wins.
            var cylinder = SurfaceProbe.Rejected(NoSectionsReason);
            var cone = cylinder;
            var sphere = cylinder;
            var torus = cylinder;
            var probed = false;

            foreach (var alongU in new[] { true, false })
            {
                if (!TryExtractCircularSections(surface, alongU, exactness, out var sections, out var sectionReason))
                {
                    if (!probed)
                    {
                        var rejected = SurfaceProbe.Rejected(sectionReason);
                        cylinder = Preferred(cylinder, rejected);
                        cone = Preferred(cone, rejected);
                        sphere = Preferred(sphere, rejected);
                        torus = Preferred(torus, rejected);
                    }

                    continue;
                }

                probed = true;
                cylinder = Preferred(cylinder, ProbeCylinder(surface, sections, model));
                cone = Preferred(cone, ProbeCone(surface, sections, model));
                sphere = Preferred(sphere, ProbeSphere(surface, sections, model));
                torus = Preferred(torus, ProbeTorus(surface, sections, model));
            }

            return new RecoveryContext(cylinder, cone, sphere, torus);
        }
    }

    /// <summary>Keeps an admissible probe, and otherwise the most recent account of why none was.</summary>
    private static SurfaceProbe Preferred(SurfaceProbe current, SurfaceProbe candidate)
        => current.IsAdmissible ? current : candidate;

    private static double ResolveModelTolerance(double diagonal, double? declaredAccuracyMillimetres)
    {
        var ceiling = double.Max(diagonal * DeclaredAccuracyRelativeCeiling, MinimumTolerance);
        if (declaredAccuracyMillimetres is { } declared && double.IsFinite(declared) && declared > 0d)
        {
            return double.Min(declared, ceiling);
        }

        return double.Max(diagonal * UndeclaredAccuracyRelativeTolerance, MinimumTolerance);
    }

    private static double ControlNetDiagonal(BSplineSurfaceWithKnots surface)
    {
        var points = surface.ControlPoints.SelectMany(row => row).ToArray();
        if (points.Length == 0)
        {
            return 0d;
        }

        return new Vector3D(
            points.Max(point => point.X) - points.Min(point => point.X),
            points.Max(point => point.Y) - points.Min(point => point.Y),
            points.Max(point => point.Z) - points.Min(point => point.Z)).Length;
    }

    private static bool TryExtractCircularSections(
        BSplineSurfaceWithKnots surface,
        bool alongU,
        double exactness,
        out IReadOnlyList<CircularSection> sections,
        out string reason)
    {
        var found = new List<CircularSection>(SectionCount);
        sections = found;
        var label = alongU ? "u" : "v";
        var sweepStart = alongU ? surface.DomainStartU : surface.DomainStartV;
        var sweepEnd = alongU ? surface.DomainEndU : surface.DomainEndV;
        var acrossStart = alongU ? surface.DomainStartV : surface.DomainStartU;
        var acrossEnd = alongU ? surface.DomainEndV : surface.DomainEndU;
        var worst = 0d;

        for (var sectionIndex = 0; sectionIndex < SectionCount; sectionIndex++)
        {
            var across = Lerp(acrossStart, acrossEnd, sectionIndex / (double)(SectionCount - 1));
            var samples = new Point3D[SectionSampleCount];
            for (var sampleIndex = 0; sampleIndex < SectionSampleCount; sampleIndex++)
            {
                var sweep = Lerp(sweepStart, sweepEnd, sampleIndex / (double)(SectionSampleCount - 1));
                samples[sampleIndex] = alongU ? surface.Evaluate(sweep, across) : surface.Evaluate(across, sweep);
            }

            if (!TryCircleThroughPoints(samples[0], samples[SectionSampleCount / 2], samples[^1], out var center, out var radius, out var normal))
            {
                reason = $"a {label} section is straight or degenerate, so the spline sweeps no circle.";
                return false;
            }

            var axis = normal.ToVector();
            foreach (var sample in samples)
            {
                var radial = RadialDistance(sample, center, axis, out var axial);
                var deviation = double.Sqrt(((radial - radius) * (radial - radius)) + (axial * axial));
                worst = double.Max(worst, deviation);
                if (deviation > exactness)
                {
                    reason = $"a {label} section departs from an exact circle by {deviation:G4} mm, above the {exactness:G4} mm exactness gate, so the spline is free-form rather than an encoded primitive.";
                    return false;
                }
            }

            if (found.Count > 0 && axis.Dot(found[0].Normal.ToVector()) < 0d)
            {
                normal = Direction3D.Create(axis * -1d);
            }

            found.Add(new CircularSection(center, radius, normal, samples[0]));
        }

        reason = $"{label} sections are exact circles to {worst:G4} mm.";
        return true;
    }

    private static SurfaceProbe ProbeCylinder(BSplineSurfaceWithKnots surface, IReadOnlyList<CircularSection> sections, double tolerance)
    {
        var radius = sections.Average(section => section.Radius);
        var spread = sections.Max(section => double.Abs(section.Radius - radius));
        if (spread > tolerance)
        {
            return SurfaceProbe.Rejected($"section radii vary by {spread:G4} mm, above the {tolerance:G4} mm model tolerance, so the sweep is not a cylinder.");
        }

        if (!TryFitSweepAxis(sections, out var axis, out var origin))
        {
            return SurfaceProbe.Rejected("section centres coincide, so the sweep has no cylinder axis.");
        }

        if (!TryPerpendicularReference(sections[0].Start - origin, axis, out var reference))
        {
            return SurfaceProbe.Rejected("first section has no usable reference direction.");
        }

        try
        {
            var geometry = SurfaceGeometry.FromCylinder(new CylinderSurface(origin, axis, radius, reference));
            return Verified(surface, geometry, tolerance, $"cylinder of radius {radius:G6} mm");
        }
        catch (ArgumentException exception)
        {
            return SurfaceProbe.Rejected($"cylinder frame is degenerate ({exception.Message}).");
        }
    }

    private static SurfaceProbe ProbeCone(BSplineSurfaceWithKnots surface, IReadOnlyList<CircularSection> sections, double tolerance)
    {
        if (!TryFitSweepAxis(sections, out var axis, out var origin))
        {
            return SurfaceProbe.Rejected("section centres coincide, so the sweep has no cone axis.");
        }

        var axials = AxialCoordinates(sections, axis, origin);
        var radii = sections.Select(section => section.Radius).ToArray();
        var (slope, radiusAtOrigin) = FitLine(axials, radii);
        if (double.Abs(slope) <= ConeMinimumRadiusSlope)
        {
            return SurfaceProbe.Rejected("sweep radius is constant, so the sweep is a cylinder rather than a cone.");
        }

        for (var index = 0; index < sections.Count; index++)
        {
            var expected = radiusAtOrigin + (slope * axials[index]);
            if (double.Abs(radii[index] - expected) > tolerance)
            {
                return SurfaceProbe.Rejected($"section radii are not linear along the axis (off by {double.Abs(radii[index] - expected):G4} mm, above {tolerance:G4} mm), so the sweep is not a cone.");
            }
        }

        // The cone axis runs from the apex towards the widening end; flipping it negates the axial coordinate, so the
        // radius at the origin is unchanged and only the sign of the slope moves.
        if (slope < 0d)
        {
            axis = Direction3D.Create(axis.ToVector() * -1d);
            slope = -slope;
        }

        if (radiusAtOrigin <= 0d)
        {
            return SurfaceProbe.Rejected("fitted radius at the axis origin is not positive, so no cone placement is defined.");
        }

        var semiAngle = double.Atan(slope);
        if (!TryPerpendicularReference(sections[0].Start - origin, axis, out var reference))
        {
            return SurfaceProbe.Rejected("first section has no usable reference direction.");
        }

        try
        {
            var geometry = SurfaceGeometry.FromCone(new ConeSurface(origin, axis, radiusAtOrigin, semiAngle, reference));
            return Verified(surface, geometry, tolerance, $"cone of semi-angle {semiAngle:G6} rad");
        }
        catch (ArgumentException exception)
        {
            return SurfaceProbe.Rejected($"cone frame is degenerate ({exception.Message}).");
        }
    }

    private static SurfaceProbe ProbeSphere(BSplineSurfaceWithKnots surface, IReadOnlyList<CircularSection> sections, double tolerance)
    {
        // Meridian sections of a sphere are concentric great circles: they share the sphere's own centre and radius,
        // and fix no axis of their own, so they are read directly rather than through a sweep.
        var centreSpread = sections.Max(section => (section.Center - sections[0].Center).Length);
        if (centreSpread <= tolerance)
        {
            var meridianRadius = sections.Average(section => section.Radius);
            var meridianSpread = sections.Max(section => double.Abs(section.Radius - meridianRadius));
            if (meridianSpread > tolerance)
            {
                return SurfaceProbe.Rejected($"concentric section radii vary by {meridianSpread:G4} mm, above the {tolerance:G4} mm model tolerance, so the sweep is not a sphere.");
            }

            if (!TryPerpendicularReference(sections[0].Start - sections[0].Center, sections[0].Normal, out var meridianReference))
            {
                return SurfaceProbe.Rejected("concentric sections have no usable reference direction.");
            }

            try
            {
                var meridianGeometry = SurfaceGeometry.FromSphere(new SphereSurface(sections[0].Center, sections[0].Normal, meridianRadius, meridianReference));
                return Verified(surface, meridianGeometry, tolerance, $"sphere of radius {meridianRadius:G6} mm");
            }
            catch (ArgumentException exception)
            {
                return SurfaceProbe.Rejected($"sphere frame is degenerate ({exception.Message}).");
            }
        }

        if (!TryFitSweepAxis(sections, out var axis, out var origin))
        {
            return SurfaceProbe.Rejected("section centres coincide, so the sweep has no sphere axis.");
        }

        // Latitude sections of a sphere satisfy (h - hCentre)^2 + r^2 = R^2, which is linear in h once expanded:
        // h^2 + r^2 = 2*hCentre*h + (R^2 - hCentre^2). Fitting that line gives the centre height and the radius.
        var axials = AxialCoordinates(sections, axis, origin);
        var squares = sections.Select((section, index) => (axials[index] * axials[index]) + (section.Radius * section.Radius)).ToArray();
        var (slope, intercept) = FitLine(axials, squares);
        var centreHeight = slope * 0.5d;
        var radiusSquared = intercept + (centreHeight * centreHeight);
        if (!(radiusSquared > 0d) || !double.IsFinite(radiusSquared))
        {
            return SurfaceProbe.Rejected("section radii do not resolve a finite sphere radius.");
        }

        var radius = double.Sqrt(radiusSquared);
        var center = origin + (axis.ToVector() * centreHeight);
        if (!TryPerpendicularReference(sections[0].Start - center, axis, out var reference))
        {
            return SurfaceProbe.Rejected("sphere has no usable reference direction.");
        }

        try
        {
            var geometry = SurfaceGeometry.FromSphere(new SphereSurface(center, axis, radius, reference));
            return Verified(surface, geometry, tolerance, $"sphere of radius {radius:G6} mm");
        }
        catch (ArgumentException exception)
        {
            return SurfaceProbe.Rejected($"sphere frame is degenerate ({exception.Message}).");
        }
    }

    private static SurfaceProbe ProbeTorus(BSplineSurfaceWithKnots surface, IReadOnlyList<CircularSection> sections, double tolerance)
    {
        if (sections.Count < 3)
        {
            return SurfaceProbe.Rejected("fewer than three sections, so no spine circle can be derived.");
        }

        var minorRadius = sections.Average(section => section.Radius);
        var spread = sections.Max(section => double.Abs(section.Radius - minorRadius));
        if (spread > tolerance)
        {
            return SurfaceProbe.Rejected($"minor radii vary by {spread:G4} mm, above the {tolerance:G4} mm model tolerance, so the sweep is not a torus.");
        }

        // The section centres of a torus ride its spine circle, whose plane normal is the torus axis.
        if (!TryCircleThroughPoints(sections[0].Center, sections[sections.Count / 2].Center, sections[^1].Center, out var center, out var majorRadius, out var axis))
        {
            return SurfaceProbe.Rejected("section centres are collinear, so they trace no spine circle.");
        }

        var axisVector = axis.ToVector();
        var angularTolerance = tolerance / double.Max(majorRadius, tolerance);
        foreach (var section in sections)
        {
            if (double.Abs(axisVector.Dot(section.Normal.ToVector())) > angularTolerance)
            {
                return SurfaceProbe.Rejected("section planes do not contain the spine axis, so the sweep is not a torus.");
            }

            var offAxis = double.Abs((center - section.Center).Dot(section.Normal.ToVector()));
            if (offAxis > tolerance)
            {
                return SurfaceProbe.Rejected($"spine centre lies {offAxis:G4} mm off a section plane, above the {tolerance:G4} mm model tolerance.");
            }
        }

        if (!Direction3D.TryCreate(sections[0].Center - center, out var reference))
        {
            return SurfaceProbe.Rejected("torus has no usable reference direction.");
        }

        try
        {
            var geometry = SurfaceGeometry.FromTorus(new TorusSurface(center, axis, majorRadius, minorRadius, reference));
            return Verified(surface, geometry, tolerance, $"torus of radii {majorRadius:G6}/{minorRadius:G6} mm");
        }
        catch (ArgumentException exception)
        {
            return SurfaceProbe.Rejected($"torus frame is degenerate ({exception.Message}).");
        }
    }

    private static SurfaceProbe Verified(BSplineSurfaceWithKnots surface, SurfaceGeometry geometry, double tolerance, string description)
    {
        if (!TryVerify(surface, geometry, tolerance, out var deviation))
        {
            return SurfaceProbe.Rejected($"{description} reproduces the spline only to {deviation:G4} mm, above the {tolerance:G4} mm model tolerance.");
        }

        return new SurfaceProbe(geometry, deviation, $"{description}; reproduces the spline to {deviation:G4} mm within the {tolerance:G4} mm accuracy the source file declares.");
    }

    private static bool TryVerify(BSplineSurfaceWithKnots surface, SurfaceGeometry geometry, double tolerance, out double deviation)
    {
        deviation = 0d;
        for (var uIndex = 0; uIndex < VerificationSamples; uIndex++)
        {
            var u = Lerp(surface.DomainStartU, surface.DomainEndU, uIndex / (double)(VerificationSamples - 1));
            for (var vIndex = 0; vIndex < VerificationSamples; vIndex++)
            {
                var v = Lerp(surface.DomainStartV, surface.DomainEndV, vIndex / (double)(VerificationSamples - 1));
                var distance = DistanceTo(geometry, surface.Evaluate(u, v));
                if (!double.IsFinite(distance))
                {
                    deviation = double.PositiveInfinity;
                    return false;
                }

                deviation = double.Max(deviation, distance);
            }
        }

        return deviation <= tolerance;
    }

    private static double DistanceTo(SurfaceGeometry geometry, Point3D point)
    {
        switch (geometry.Kind)
        {
            case SurfaceGeometryKind.Plane when geometry.Plane is { } plane:
                return double.Abs((point - plane.Origin).Dot(plane.Normal.ToVector()));
            case SurfaceGeometryKind.Cylinder when geometry.Cylinder is { } cylinder:
                return double.Abs(RadialDistance(point, cylinder.Origin, cylinder.Axis.ToVector(), out _) - cylinder.Radius);
            case SurfaceGeometryKind.Cone when geometry.Cone is { } cone:
            {
                var radial = RadialDistance(point, cone.Apex, cone.Axis.ToVector(), out var axial);
                // Points behind the apex belong to the mirrored nappe, which is not the recovered face.
                return axial < 0d
                    ? double.PositiveInfinity
                    : double.Abs((radial * double.Cos(cone.SemiAngleRadians)) - (axial * double.Sin(cone.SemiAngleRadians)));
            }
            case SurfaceGeometryKind.Sphere when geometry.Sphere is { } sphere:
                return double.Abs((point - sphere.Center).Length - sphere.Radius);
            case SurfaceGeometryKind.Torus when geometry.Torus is { } torus:
            {
                var radial = RadialDistance(point, torus.Center, torus.Axis.ToVector(), out var axial);
                var fromSpine = radial - torus.MajorRadius;
                return double.Abs(double.Sqrt((fromSpine * fromSpine) + (axial * axial)) - torus.MinorRadius);
            }
            default:
                return double.PositiveInfinity;
        }
    }

    private static double RadialDistance(Point3D point, Point3D origin, Vector3D axis, out double axial)
    {
        var delta = point - origin;
        axial = delta.Dot(axis);
        return (delta - (axis * axial)).Length;
    }

    private static bool TryCircleThroughPoints(
        Point3D first,
        Point3D second,
        Point3D third,
        out Point3D center,
        out double radius,
        out Direction3D normal)
    {
        center = first;
        radius = 0d;
        var firstSpan = second - first;
        var secondSpan = third - first;
        var cross = firstSpan.Cross(secondSpan);
        var crossLengthSquared = cross.LengthSquared;
        if (!(crossLengthSquared > 0d) || !double.IsFinite(crossLengthSquared) || !Direction3D.TryCreate(cross, out normal))
        {
            normal = default;
            return false;
        }

        var offset = ((secondSpan * firstSpan.LengthSquared) - (firstSpan * secondSpan.LengthSquared)).Cross(cross)
            * (1d / (2d * crossLengthSquared));
        center = first + offset;
        radius = offset.Length;
        return double.IsFinite(radius) && radius > 0d;
    }

    /// <summary>
    /// Fits the sweep axis through the section centres by least squares. Taking only the first and last centre makes
    /// the axis hostage to two samples' approximation error, which on a vendor cylinder was enough to push a true
    /// primitive past the file's own declared accuracy.
    /// </summary>
    private static bool TryFitSweepAxis(IReadOnlyList<CircularSection> sections, out Direction3D axis, out Point3D origin)
    {
        axis = default;
        origin = new Point3D(
            sections.Average(section => section.Center.X),
            sections.Average(section => section.Center.Y),
            sections.Average(section => section.Center.Z));

        double xx = 0d, xy = 0d, xz = 0d, yy = 0d, yz = 0d, zz = 0d;
        foreach (var section in sections)
        {
            var delta = section.Center - origin;
            xx += delta.X * delta.X;
            xy += delta.X * delta.Y;
            xz += delta.X * delta.Z;
            yy += delta.Y * delta.Y;
            yz += delta.Y * delta.Z;
            zz += delta.Z * delta.Z;
        }

        var seed = sections[^1].Center - sections[0].Center;
        if (!Direction3D.TryCreate(seed, out var seeded))
        {
            return false;
        }

        var vector = seeded.ToVector();
        for (var iteration = 0; iteration < AxisFitIterations; iteration++)
        {
            var next = new Vector3D(
                (xx * vector.X) + (xy * vector.Y) + (xz * vector.Z),
                (xy * vector.X) + (yy * vector.Y) + (yz * vector.Z),
                (xz * vector.X) + (yz * vector.Y) + (zz * vector.Z));
            if (!(next.LengthSquared > 0d) || !double.IsFinite(next.LengthSquared))
            {
                break;
            }

            vector = next * (1d / next.Length);
        }

        if (vector.Dot(seed) < 0d)
        {
            vector *= -1d;
        }

        return Direction3D.TryCreate(vector, out axis);
    }

    private static double[] AxialCoordinates(IReadOnlyList<CircularSection> sections, Direction3D axis, Point3D origin)
    {
        var axisVector = axis.ToVector();
        return sections.Select(section => (section.Center - origin).Dot(axisVector)).ToArray();
    }

    private static (double Slope, double Intercept) FitLine(IReadOnlyList<double> xs, IReadOnlyList<double> ys)
    {
        var meanX = xs.Average();
        var meanY = ys.Average();
        var sxx = 0d;
        var sxy = 0d;
        for (var index = 0; index < xs.Count; index++)
        {
            var dx = xs[index] - meanX;
            sxx += dx * dx;
            sxy += dx * (ys[index] - meanY);
        }

        if (!(sxx > 0d))
        {
            return (0d, meanY);
        }

        var slope = sxy / sxx;
        return (slope, meanY - (slope * meanX));
    }

    private static bool TryPerpendicularReference(Vector3D candidate, Direction3D axis, out Direction3D reference)
    {
        var axisVector = axis.ToVector();
        return Direction3D.TryCreate(candidate - (axisVector * candidate.Dot(axisVector)), out reference);
    }

    private static double Lerp(double start, double end, double fraction) => start + ((end - start) * fraction);
}
