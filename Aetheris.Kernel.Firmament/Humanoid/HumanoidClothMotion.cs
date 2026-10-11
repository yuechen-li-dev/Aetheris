using System.Numerics;
using Aetheris.Cloth3D;
using Aetheris.Humanoid;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Garment;

namespace Aetheris.Kernel.Firmament.Humanoid;

/// <summary>One-way authored motion. Skinning preserves the bind skeleton; only collision positions are refitted.</summary>
internal sealed class HumanoidClothMotion : IClothKinematicBodySurface3D
{
    private readonly HumanoidGameplayBody body;
    private readonly FirmamentHumanoidPose from;
    private readonly FirmamentHumanoidPose to;
    private readonly FirmamentPoseTransition? transition;
    private readonly IReadOnlyList<AnatomicalJointRequest> fromRequests;
    private readonly IReadOnlyList<AnatomicalJointRequest> toRequests;
    private double previousTime;
    private double previousFraction = -1;

    public HumanoidClothMotion(HumanoidGameplayBody body, FirmamentPoseDocument document,
        string figure, string pose, string? transitionName)
    {
        this.body = body;
        from = document.Resolve(pose, figure);
        to = from;
        if (transitionName is not null)
        {
            transition = document.Transitions[transitionName];
            to = document.Resolve(transition.To, figure);
        }
        CurrentPose = HumanoidPoseAuthoring.Solve(from, body);
        fromRequests = HumanoidPoseAuthoring.Requests(from, body);
        toRequests = HumanoidPoseAuthoring.Requests(to, body);
        Positions = body.EvaluatePositions(CurrentPose);
        Surface = GarmentDraping.BodySurface(body, Positions);
        previousFraction = 0;
    }

    public ClothTriangleBody3D Surface { get; }
    public SolvedHumanoidPose CurrentPose { get; private set; }
    public IReadOnlyList<Point3D> Positions { get; private set; }
    public double DurationSeconds => transition?.DurationSeconds ?? 0;
    public float MaximumSubstepTravel { get; private set; }

    public ClothBodySample3D Query(Vector3 position)
    {
        return Surface.Query(position);
    }

    public void Sample(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }
        double fraction = DurationSeconds == 0 ? 0 : Math.Clamp(seconds / DurationSeconds, 0, 1);
        if (fraction == previousFraction)
        {
            previousTime = seconds;
            return;
        }
        double eased = fraction;
        if (transition?.Easing == "SmoothStep")
        {
            eased = fraction * fraction * (3 - 2 * fraction);
        }
        var requests = HumanoidPoseRecipes.Blend(body, fromRequests, toRequests, eased);
        var recipe = new FirmamentHumanoidPose(transition!.Name, from.Figure, requests);
        var solved = HumanoidPoseAuthoring.Solve(recipe, body);
        var updated = body.EvaluatePositions(solved);
        var collisionPositions = updated.Select(point => new Vector3((float)point.X, (float)point.Y, (float)point.Z) * .001f).ToArray();
        Surface.Refit(collisionPositions);
        // Backward samples are legal for replay; only forward substep travel is measured.
        if (seconds > previousTime && seconds - previousTime <= .1)
        {
            for (int vertex = 0; vertex < updated.Count; vertex++)
            {
                float distance = (float)(updated[vertex] - Positions[vertex]).Length * .001f;
                MaximumSubstepTravel = MathF.Max(MaximumSubstepTravel, distance);
            }
        }
        Positions = updated;
        CurrentPose = solved;
        previousFraction = fraction;
        previousTime = seconds;
    }

    public ConstrainedSurfaceEvidence Inspect()
    {
        return HumanoidConstrainedSurface.Inspect(body.Surface, body.Skeleton, CurrentPose.GlobalTransforms, Positions);
    }
}
