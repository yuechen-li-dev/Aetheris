using System.Collections.Immutable;
using System.Diagnostics;
using System.Numerics;
using Aetheris.Cloth3D;
using Aetheris.Humanoid;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Humanoid;

namespace Aetheris.Kernel.Firmament.Garment;

public sealed record GarmentDrapeSettings
{
    public int Substeps { get; init; } = 8;
    public int Iterations { get; init; } = 24;
    public bool StopWhenSettled { get; init; }
    public float StationarySpeed { get; init; } = .01f;

    public void Validate()
    {
        if (Substeps is < 1 or > 64 || Iterations is < 1 or > 64
            || !float.IsFinite(StationarySpeed) || StationarySpeed <= 0 || StationarySpeed > .1f)
        {
            throw new ArgumentException("Invalid bounded garment drape settings.");
        }
    }
}

public static class GarmentDrapeLimits
{
    public const float MaximumStretch = .15f;
    public const float MaximumSeamGap = .01f;
    public const float MaximumPenetration = .005f;

    public static bool Accept(ClothMetrics3D metrics, float seamGap)
    {
        return metrics.Finite && metrics.MaximumStretch <= MaximumStretch
            && metrics.MaximumSurfacePenetration <= MaximumPenetration && seamGap <= MaximumSeamGap;
    }
}

public sealed record GarmentDrapeSample(string Phase, int Frame, float MaximumSpeed,
    ClothMetrics3D Metrics, float MaximumSeamGap, bool Settled)
{
    public IReadOnlyList<GarmentGuideResidual> Guides { get; init; } = [];
}

public sealed record GarmentGuideResidual(string Name, string ConceptPlane, int ReleaseAfter,
    bool Active, float MaximumViolation);
public sealed record GarmentFittingCheckpoint(string Name, ClothSnapshot3D Snapshot, IReadOnlyList<Point3D> BodyPositions);

public sealed record GarmentDrapeResult(ClothSnapshot3D Snapshot, ClothMetrics3D Metrics,
    float MaximumSeamGap, float InitialSeamGap, bool ReplayExact, string BodyId, string BodyTopology,
    IReadOnlyList<GarmentContactOutlier> ContactOutliers, IReadOnlyList<GarmentStretchOutlier> StretchOutliers)
{
    public IReadOnlyList<GarmentSurfaceContactOutlier> SurfaceContactOutliers { get; init; } = [];
    public IReadOnlyList<GarmentDrapeFrame> Frames { get; init; } = [];
    public IReadOnlyList<Point3D> BodyPositions { get; init; } = [];
    public bool TransitionReplayExact { get; init; } = true;
    public float MaximumBodySubstepTravel { get; init; }
    public GarmentDrapeSettings Settings { get; init; } = new();
    public IReadOnlyList<GarmentDrapeSample> Samples { get; init; } = [];
    public int CompletedSettlingTicks { get; init; }
    public string StopReason { get; init; } = "FrameBudget";
    public bool GuidesReleased { get; init; } = true;
    public IReadOnlyList<GarmentFittingCheckpoint> FittingCheckpoints { get; init; } = [];
}

public sealed record GarmentDrapeFrame(string Name, double MotionSeconds, ClothSnapshot3D Snapshot,
    IReadOnlyList<Point3D> BodyPositions, ClothMetrics3D Metrics, float MaximumSeamGap,
    ConstrainedSurfaceEvidence BodyEvidence, IReadOnlyList<AnatomicalJointRequest> Joints);

public sealed record GarmentContactOutlier(int Vertex, string Panel, bool Pinned, float Penetration, float[] Position);
public sealed record GarmentStretchOutlier(int A, int B, float Stretch, float RestLength)
{
    public string Panel { get; init; } = "";
    public string Kind { get; init; } = "";
    public float[] MaterialA { get; init; } = [];
    public float[] MaterialB { get; init; } = [];
}
public sealed record GarmentSurfaceContactOutlier(int Triangle, string Panel, float Penetration, float[] Position);
public sealed record GarmentDrapeProgress(string Phase, int Completed, int Total, double ElapsedSeconds);

public static class GarmentDraping
{
    public static ClothTriangleBody3D BodySurface(HumanoidGameplayBody body, IReadOnlyList<Point3D>? posed = null)
    {
        ArgumentNullException.ThrowIfNull(body);
        var positions = (posed ?? body.Surface.Vertices.Select(vertex => vertex.Position).ToArray())
            .Select(point => new Vector3((float)point.X, (float)point.Y, (float)point.Z) * .001f).ToImmutableArray();
        var outer = body.Surface.Components.Where(component => component.Kind == HumanoidComponentKind.OuterSkin)
            .SelectMany(component => component.FaceIndices).Distinct().Order().ToArray();
        if (outer.Length == 0)
        {
            throw new InvalidDataException("garment-body-skin-missing: Body needs its declared OuterSkin component.");
        }
        var indices = outer.SelectMany(index =>
        {
            var face = body.Surface.Faces[index];
            return new[] { face.A, face.B, face.C };
        }).ToImmutableArray();
        return new(positions, indices);
    }

    /// <summary>Sew and settle on an authored figure pose, optionally following an explicit kinematic transition.
    /// This remains a one-way CPU contact experiment without friction, CCD or self-contact.</summary>
    public static GarmentDrapeResult Settle(CompiledGarment garment, HumanoidGameplayBody body, int ticks = 60,
        Action<GarmentDrapeProgress>? progress = null, GarmentDrapeSettings? settings = null)
    {
        if (ticks is < 1 or > 600)
        {
            throw new ArgumentOutOfRangeException(nameof(ticks));
        }
        if (garment.Source.Figure is null || garment.Source.Pose is null)
        {
            throw new InvalidOperationException("garment-drape-binding-required: Declare a Drape figure and pose, and supply its body artifact.");
        }
        if (garment.Source.FigureFits.Count > 0)
        {
            string surfaceHash = HumanoidFigureFitting.SurfaceHash(body);
            if (garment.Source.FigureFits.Any(fit => fit.Measurements.BodyId != body.Id
                || fit.Measurements.RestSurfaceHash != surfaceHash
                || fit.Measurements.SkeletonId != body.Skeleton.SkeletonId
                || fit.Measurements.RestPoseId != body.Skeleton.RestPoseId))
            {
                throw new InvalidOperationException("garment-fit-body-changed: Recompile the pattern for the supplied body before draping.");
            }
        }
        HumanoidClothMotion? motion = null;
        if (garment.Source.Pose != "Rest" || garment.Source.Transition is not null)
        {
            var posing = garment.Source.Posing ?? throw new InvalidOperationException(
                "garment-pose-program-required: Compile the authored pose declarations before draping.");
            motion = new(body, posing, garment.Source.Figure, garment.Source.Pose, garment.Source.Transition);
        }
        return SettleCore(garment, motion?.Surface ?? BodySurface(body), body.Id, body.Surface.ConnectivityHash,
            body.Surface.Vertices.Select(vertex => vertex.Position).ToArray(), motion, ticks, progress, settings ?? new());
    }

    /// <summary>Static reference-body experiment using the same sewing, settling and replay path.
    /// This accepts an existing collider without inventing a humanoid rig for an unrigged reference mesh.</summary>
    public static GarmentDrapeResult SettleOnSurface(CompiledGarment garment, IClothBodySurface3D surface,
        string bodyId, string topologyId, int ticks = 60, Action<GarmentDrapeProgress>? progress = null,
        GarmentDrapeSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (ticks is < 1 or > 600)
        {
            throw new ArgumentOutOfRangeException(nameof(ticks));
        }
        if (string.IsNullOrWhiteSpace(bodyId) || string.IsNullOrWhiteSpace(topologyId))
        {
            throw new ArgumentException("A reference surface requires explicit body and topology identities.");
        }
        if (garment.Source.Figure is not null || garment.Source.FigureFits.Count > 0 || garment.Source.Pose is not (null or "Rest")
            || garment.Source.Transition is not null || surface is IClothKinematicBodySurface3D)
        {
            throw new InvalidOperationException("garment-reference-surface-static: Measured fits and poses require their actual humanoid binding.");
        }
        return SettleCore(garment, surface, bodyId, topologyId, [], null, ticks, progress, settings ?? new());
    }

    private static GarmentDrapeResult SettleCore(CompiledGarment garment, IClothBodySurface3D surface,
        string bodyId, string topologyId, IReadOnlyList<Point3D> bodyPositions, HumanoidClothMotion? motion,
        int ticks, Action<GarmentDrapeProgress>? progress, GarmentDrapeSettings settings)
    {
        settings.Validate();
        int releaseFrame = garment.FittingGuides.IsEmpty ? 0 : garment.FittingGuides.Max(guide => guide.ReleaseAfter);
        if (!garment.FittingGuides.IsEmpty && ticks < releaseFrame + 15)
        {
            throw new InvalidOperationException("garment-fitting-release-budget: Provide at least 15 unsupported settling frames after the last guide release.");
        }
        var contacts = new ClothContacts3D
        {
            BodySurface = surface,
            BodyClearance = garment.Source.BodyClearance,
            Guides = garment.FittingGuides.Select(guide => guide.Constraint).ToImmutableArray(),
        };
        var options = new ClothStepOptions3D
        {
            Gravity = new(0, 0, -9.81f),
            Substeps = settings.Substeps,
            Iterations = settings.Iterations,
            ContactIterations = 1,
            VelocityDamping = 8,
        };
        using var solver = new ClothSolver3D(garment.Cloth);
        var watch = Stopwatch.StartNew();
        var initial = solver.Capture();
        float initialGap = garment.MaximumSeamGap(initial);
        var frames = new List<GarmentDrapeFrame>();
        var samples = new List<GarmentDrapeSample>();
        var fittingCheckpoints = new List<GarmentFittingCheckpoint>();
        int completedSettlingTicks = 0;
        int consecutiveSettled = 0;
        string stopReason = "FrameBudget";
        bool transitionReplay = true;
        // Sew with gravity disabled and a decreasing compliance, then settle.
        // This keeps the initial closing impulse from dominating garment motion.
        for (int stage = 0; stage < 30; stage++)
        {
            float scale = MathF.Pow(10, 6 * (1 - stage / 29f));
            solver.Step(options with { Gravity = Vector3.Zero, StitchComplianceScale = scale }, contacts);
            if ((stage + 1) % 5 == 0)
            {
                Sample("sewing", stage + 1);
                progress?.Invoke(new("sewing", stage + 1, 30, watch.Elapsed.TotalSeconds));
            }
        }
        for (int tick = 0; tick < ticks; tick++)
        {
            if (!contacts.Guides.IsEmpty && tick == releaseFrame)
            {
                fittingCheckpoints.Add(new("guided", solver.Capture(), motion?.Positions.ToArray() ?? bodyPositions));
            }
            contacts = contacts with
            {
                Guides = garment.FittingGuides.Where(guide => tick < guide.ReleaseAfter)
                    .Select(guide => guide.Constraint).ToImmutableArray(),
            };
            solver.Step(options, contacts);
            completedSettlingTicks = tick + 1;
            if ((tick + 1) % 5 == 0 || tick + 1 == ticks)
            {
                bool settled = Sample("settling", tick + 1);
                progress?.Invoke(new("settling", tick + 1, ticks, watch.Elapsed.TotalSeconds));
                consecutiveSettled = settled ? consecutiveSettled + 1 : 0;
                if (settings.StopWhenSettled && tick + 1 >= 30 && consecutiveSettled >= 3)
                {
                    stopReason = "Settled";
                    break;
                }
            }
        }
        if (motion is not null)
        {
            RecordFrame("fitting", 0);
            int transitionTicks = (int)Math.Ceiling(motion.DurationSeconds / options.DeltaSeconds);
            for (int tick = 0; tick < transitionTicks; tick++)
            {
                var movingContacts = contacts with { BodySurface = motion, BodyTimeSeconds = tick * (double)options.DeltaSeconds };
                bool midpoint = tick + 1 == Math.Max(1, transitionTicks / 2);
                var before = midpoint ? solver.Capture() : null;
                solver.Step(options, movingContacts);
                if (midpoint)
                {
                    var expectedMoving = solver.Capture();
                    solver.Restore(before!);
                    solver.Step(options, movingContacts);
                    transitionReplay = SameSnapshot(expectedMoving, solver.Capture());
                    RecordFrame("midway", Math.Min(motion.DurationSeconds, (tick + 1d) * options.DeltaSeconds));
                }
                if ((tick + 1) % 5 == 0 || tick + 1 == transitionTicks)
                {
                    progress?.Invoke(new("transition", tick + 1, transitionTicks, watch.Elapsed.TotalSeconds));
                }
            }
            if (transitionTicks > 0)
            {
                for (int tick = 0; tick < ticks; tick++)
                {
                    solver.Step(options, contacts);
                    if ((tick + 1) % 5 == 0 || tick + 1 == ticks)
                    {
                        Sample("wearing", tick + 1);
                        progress?.Invoke(new("wearing", tick + 1, ticks, watch.Elapsed.TotalSeconds));
                    }
                }
                RecordFrame("wearing", motion.DurationSeconds);
            }
        }
        var final = solver.Capture();
        var beforeReplay = final;
        solver.Step(options, contacts);
        var expected = solver.Capture();
        solver.Restore(beforeReplay);
        solver.Step(options, contacts);
        var replayed = solver.Capture();
        bool replay = SameSnapshot(expected, replayed);
        var pins = garment.Cloth.Definition.Pins.ToHashSet();
        var contactOutliers = final.Positions.Select((position, index) => new GarmentContactOutlier(index,
            garment.Panels.Single(panel => index >= panel.VertexStart && index < panel.VertexStart + panel.VertexCount).Identity,
            pins.Contains(index), garment.Cloth.Definition.Material.Thickness * .5f + contacts.BodyClearance - contacts.BodySurface!.Query(position).SignedDistance,
            [position.X, position.Y, position.Z])).Where(item => item.Penetration > .001f)
            .OrderByDescending(item => item.Penetration).Take(8).ToArray();
        var stretchOutliers = garment.Cloth.Constraints.Where(constraint => constraint.Vertices.Length == 2
            && constraint.Weights.IsEmpty).Select(constraint => new GarmentStretchOutlier(constraint.Vertices[0], constraint.Vertices[1],
                Vector3.Distance(final.Positions[constraint.Vertices[0]], final.Positions[constraint.Vertices[1]]) / constraint.RestLength - 1,
                constraint.RestLength)
            {
                Panel = PanelOfVertex(constraint.Vertices[0]),
                Kind = constraint.Kind.ToString(),
                MaterialA = MaterialPoint(constraint.Vertices[0]),
                MaterialB = MaterialPoint(constraint.Vertices[1]),
            }).OrderByDescending(item => item.Stretch).Take(8).ToArray();
        var surfaceOutliers = new List<GarmentSurfaceContactOutlier>();
        var indices = garment.Cloth.Definition.Indices;
        for (int face = 0; face < indices.Length; face += 3)
        {
            foreach (var barycentric in ClothBodyContact3D.Samples)
            {
                Vector3 point = final.Positions[indices[face]] * barycentric.X
                    + final.Positions[indices[face + 1]] * barycentric.Y
                    + final.Positions[indices[face + 2]] * barycentric.Z;
                float penetration = garment.Cloth.Definition.Material.Thickness * .5f + contacts.BodyClearance
                    - contacts.BodySurface!.Query(point).SignedDistance;
                if (penetration > .001f)
                {
                    surfaceOutliers.Add(new(face / 3, PanelOfVertex(indices[face]), penetration, [point.X, point.Y, point.Z]));
                }
            }
        }
        return new(final, ClothState3D.Measure(garment.Cloth, final, contacts), garment.MaximumSeamGap(final),
            initialGap, replay, bodyId, topologyId, contactOutliers, stretchOutliers)
        {
            SurfaceContactOutliers = surfaceOutliers.OrderByDescending(outlier => outlier.Penetration).Take(8).ToArray(),
            Frames = frames,
            BodyPositions = motion?.Positions.ToArray() ?? bodyPositions,
            TransitionReplayExact = transitionReplay,
            MaximumBodySubstepTravel = motion?.MaximumSubstepTravel ?? 0,
            Settings = settings,
            Samples = samples,
            CompletedSettlingTicks = completedSettlingTicks,
            StopReason = stopReason,
            GuidesReleased = contacts.Guides.IsEmpty,
            FittingCheckpoints = fittingCheckpoints,
        };

        bool Sample(string phase, int frame)
        {
            var snapshot = solver.Capture();
            var metrics = ClothState3D.Measure(garment.Cloth, snapshot, contacts);
            float gap = garment.MaximumSeamGap(snapshot);
            float speed = snapshot.Velocities.Max(velocity => velocity.Length());
            bool settled = float.IsFinite(speed) && speed <= settings.StationarySpeed
                && GarmentDrapeLimits.Accept(metrics, gap)
                && contacts.Guides.IsEmpty
                && (garment.FittingGuides.IsEmpty || (phase != "sewing" && frame >= releaseFrame + 15));
            var guideResiduals = garment.FittingGuides.Select(guide => new GarmentGuideResidual(
                guide.Constraint.Id, guide.ConceptPlane, guide.ReleaseAfter,
                contacts.Guides.Any(active => active.Id == guide.Constraint.Id),
                guide.Constraint.Vertices.Max(vertex => MathF.Max(0,
                    guide.Constraint.Offset - Vector3.Dot(guide.Constraint.Normal, snapshot.Positions[vertex]))))).ToArray();
            samples.Add(new(phase, frame, speed, metrics, gap, settled) { Guides = guideResiduals });
            return settled;
        }

        void RecordFrame(string name, double seconds)
        {
            var snapshot = solver.Capture();
            frames.Add(new(name, seconds, snapshot, motion!.Positions.ToArray(), ClothState3D.Measure(garment.Cloth, snapshot, contacts),
                garment.MaximumSeamGap(snapshot), motion.Inspect(), motion.CurrentPose.Joints));
        }

        string PanelOfVertex(int vertex)
        {
            return garment.Panels.Single(panel => vertex >= panel.VertexStart && vertex < panel.VertexStart + panel.VertexCount).Identity;
        }
        float[] MaterialPoint(int vertex)
        {
            var point = garment.Cloth.Definition.Positions[vertex];
            return [point.X, point.Y];
        }
    }

    private static bool SameSnapshot(ClothSnapshot3D expected, ClothSnapshot3D actual)
    {
        return expected.Tick == actual.Tick && expected.ContentKey == actual.ContentKey
            && expected.Positions.SequenceEqual(actual.Positions) && expected.Velocities.SequenceEqual(actual.Velocities)
            && expected.PinTargets.SequenceEqual(actual.PinTargets);
    }
}
