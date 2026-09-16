using System.Security.Cryptography;
using System.Text.Json;
using Aetheris.Humanoid;
using Aetheris.Kernel.Core.Math;

internal sealed record SourcePoseRequest(string Joint, string Side, double FlexionDegrees, double AbductionDegrees,
    double TwistDegrees, string Frame, string Convention, string RestBasis);

internal sealed record SourcePoseDump(string Schema, string PoseId, string TopologyId, string ConnectivityHash,
    string MappingSha256, string SourceAssetSha256, bool TopologyVerified, IReadOnlyList<Point3D> Positions,
    SourcePoseRequest RequestedPose, IReadOnlyList<string> Limitations);

internal static class SourcePoseComparison
{
    public static int Run(string input, string sourcePose, string output)
    {
        var candidate = JsonSerializer.Deserialize<AntoniaAdoptionCandidate>(File.ReadAllText(input), HumanoidArtifactIO.JsonOptions)
            ?? throw new InvalidDataException("Missing Antonia adoption candidate.");
        var source = JsonSerializer.Deserialize<SourcePoseDump>(File.ReadAllText(sourcePose), HumanoidArtifactIO.JsonOptions)
            ?? throw new InvalidDataException("Missing Blender source-pose dump.");
        if (!source.TopologyVerified || source.TopologyId != candidate.SourcePoseSurface.TopologyId ||
            source.ConnectivityHash != candidate.SourcePoseSurface.ConnectivityHash)
            throw new InvalidDataException("HUM403: Source pose lacks the verified canonical topology identity/connectivity.");
        if (source.Positions.Count != candidate.SourcePoseSurface.Vertices.Count)
            throw new InvalidDataException("HUM400: Source pose is not in canonical vertex order.");
        if (source.RequestedPose.Joint != "LeftHip" || source.RequestedPose.Side != "Left")
            throw new InvalidDataException("HUM404: X4 comparison currently admits only the declared left-hip corpus.");

        var prepared = candidate.SourcePoseSurface with
        {
            Vertices = candidate.SourcePoseSurface.Vertices.Select((vertex, index) =>
                vertex with { Position = candidate.PreparedPositions[index] }).ToArray()
        };
        var request = new AnatomicalJointRequest(HumanoidJointKind.LeftHip, source.RequestedPose.FlexionDegrees,
            source.RequestedPose.AbductionDegrees, source.RequestedPose.TwistDegrees);
        var solve = HumanoidKinematicSolver.Solve(candidate.PreparedSkeleton,
            new(source.PoseId, candidate.PreparedSkeleton.SkeletonId, candidate.PreparedSkeleton.RestPoseId, 0, [request]));
        if (solve.Pose is not { } solved)
            throw new InvalidDataException("HUM405: Aetheris did not solve the source pose request: " +
                string.Join("; ", solve.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var context = HumanoidDeformationContext.Create(prepared, candidate.PreparedSkeleton, 0, solved, HumanoidJointKind.LeftHip);
        var decision = HumanoidDeformationJudgment.Evaluate(context, DeformationPolicy.HipV1);
        var candidateDiffs = decision.Candidates.Select(measured => new
        {
            measured.Candidate.Id,
            measured.Candidate.Construction,
            admissible = decision.Trace.Single(trace => trace.Id == measured.Candidate.Id).Admissible,
            diff = HumanoidSourcePoseComparison.Compare(prepared, source.Positions, context.AssembleDiagnostic(measured.Candidate))
        }).ToArray();

        var report = new
        {
            schema = "aetheris.humanoid.source-pose-comparison.v1",
            source.PoseId,
            source.RequestedPose,
            topology = new
            {
                source.TopologyId,
                source.ConnectivityHash,
                vertices = source.Positions.Count,
                source.MappingSha256,
                source.TopologyVerified,
                correspondence = "Explicit canonical-index to source-index bijection over the admitted topology subset; no nearest-surface pose matching."
            },
            inputs = new
            {
                candidateSha256 = Hash(input),
                sourceDumpSha256 = Hash(sourcePose),
                source.SourceAssetSha256
            },
            aetheris = new
            {
                decision.IsSuccess,
                decision.WinnerId,
                decision.RunnerUpId,
                decision.Diagnostics,
                maximumCenterResidualMm = solved.Residuals.Max(residual => residual.CenterMm),
                maximumLinkLengthResidualMm = solved.Residuals.Max(residual => residual.LinkLengthMm)
            },
            selectedDiff = decision.Positions is null ? null : HumanoidSourcePoseComparison.Compare(prepared, source.Positions, decision.Positions),
            candidates = candidateDiffs,
            source.Limitations
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(report, HumanoidArtifactIO.JsonOptions));
        Console.WriteLine($"Source-pose comparison: {output}. Judgment success={decision.IsSuccess}; selected={decision.WinnerId ?? "none"}.");
        return 0;
    }

    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}
