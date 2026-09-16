using System.Security.Cryptography;
using System.Text.Json;
using Aetheris.Humanoid;
using Aetheris.Kernel.Core.Math;

internal static class ReferenceRigQualification
{
    public static int Run(string input, string rig, string output)
    {
        var candidate = JsonSerializer.Deserialize<AntoniaAdoptionCandidate>(File.ReadAllText(input), HumanoidArtifactIO.JsonOptions)
            ?? throw new InvalidDataException("Missing Antonia candidate.");
        var adoption = AntoniaReferenceRigAdapter.Adopt(candidate, rig);
        var surface = adoption.Surface;
        var skeleton = adoption.Skeleton;
        Directory.CreateDirectory(output);
        var cases = new List<object>();
        var allAdmissible = true;

        foreach (var (name, joints) in Corpus())
        {
            var solve = HumanoidKinematicSolver.Solve(skeleton,
                new(name, skeleton.SkeletonId, skeleton.RestPoseId, 0, joints));
            if (solve.Pose is not { } posed)
                throw new InvalidOperationException(name + " failed: " + string.Join("; ", solve.Diagnostics.Select(d => d.Message)));

            IReadOnlyList<Point3D> positions;
            object? judgment = null;
            if (joints.Length == 1 && joints[0].Joint == HumanoidJointKind.LeftHip)
            {
                var context = HumanoidDeformationContext.Create(surface, skeleton, 0, posed, joints[0].Joint);
                var decision = HumanoidDeformationJudgment.Evaluate(context, DeformationPolicy.HipV1);
                foreach (var evaluated in decision.Candidates)
                    Export(Path.Combine(output, name + "." + evaluated.Candidate.Id + ".diagnostic.obj"), surface,
                        context.AssembleDiagnostic(evaluated.Candidate), "diagnostic candidate; may fail admission");
                positions = decision.Positions ?? HumanoidConstrainedSurface.Evaluate(surface, skeleton, 0, posed).Positions;
                judgment = new { decision.PolicyVersion, decision.IsSuccess, decision.WinnerId, decision.RunnerUpId,
                    decision.TieBroken, decision.Trace, metrics = decision.Candidates.Select(c => new { c.Candidate.Id, c.Metrics }),
                    decision.Diagnostics, decision.FinalSurfaceEvidence };
            }
            else
            {
                positions = HumanoidConstrainedSurface.Evaluate(surface, skeleton, 0, posed).Positions;
            }

            var evidence = HumanoidConstrainedSurface.Inspect(surface, skeleton, posed.GlobalTransforms, positions);
            allAdmissible &= evidence.IsAdmissible;
            var deformation = HumanoidSourcePoseComparison.Compare(surface,
                surface.Vertices.Select(vertex => vertex.Position).ToArray(), positions);
            var filename = name + (evidence.IsAdmissible ? ".screened.obj" : ".failed-diagnostic.obj");
            Export(Path.Combine(output, filename), surface, positions, evidence.IsAdmissible ? "screened reference-rig pose" : "failed reference-rig pose");
            cases.Add(new { name, requested = joints, solve.Diagnostics, solve.Projections,
                maximumCenterResidualMm = posed.Residuals.Count == 0 ? 0 : posed.Residuals.Max(r => r.CenterMm),
                maximumLinkResidualMm = posed.Residuals.Count == 0 ? 0 : posed.Residuals.Max(r => r.LinkLengthMm),
                surfaceEvidence = evidence, deformation, judgment, obj = filename });
        }

        var evidencePath = Path.Combine(output, "evidence.json");
        File.WriteAllText(evidencePath, JsonSerializer.Serialize(new
        {
            schema = "aetheris.humanoid.x5.reference-rig-qualification.v1",
            verdict = allAdmissible ? "Pose corpus mechanically screened" : "Pose corpus contains failed surface screens",
            adapter = AntoniaReferenceRigAdapter.AdapterId,
            solverVersion = HumanoidKinematicSolver.Version,
            sourceCandidateSha256 = Sha(input), referenceRigSha256 = Sha(rig),
            surface.TopologyId, surface.ConnectivityHash, adoption.Evidence,
            sourceJoints = adoption.Artifact.SourceJoints.Count, mappedJoints = skeleton.Joints.Count,
            ignoredSourceJoints = adoption.Artifact.IgnoredSourceJoints.Count,
            cases,
            limitations = new[]
            {
                "The existing X1 Aetheris-generated skin weights are deliberately unchanged.",
                "The source metarig supplies rest frames and hierarchy only; generated Rigify controls are not runtime dependencies.",
                "Shoulder and elbow poses use the bounded mechanism plus unchanged linear blend skinning; Judgment candidates remain hip-only.",
                "No Antonia landmarks, attachment frames, measurements, or morph channels exist to recompute in this research candidate.",
                "Mechanical screening is not a clinical biomechanics claim or human visual signoff."
            }
        }, HumanoidArtifactIO.JsonOptions));
        Console.WriteLine($"X5 reference-rig evidence: {evidencePath}; all mechanically admissible: {allAdmissible}.");
        // Evidence determines milestone acceptance; artifact generation remains useful when a
        // pose is rejected because those failures are the bounded downstream work list.
        return 0;
    }

    private static IEnumerable<(string Name, AnatomicalJointRequest[] Joints)> Corpus()
    {
        yield return ("neutral", []);
        foreach (var angle in new[] { 30d, 45, 70, 90 }) yield return ($"hip-flexion-{angle}", [new(HumanoidJointKind.LeftHip, angle)]);
        foreach (var angle in new[] { 15d, 30, 45 }) yield return ($"hip-abduction-{angle}", [new(HumanoidJointKind.LeftHip, AbductionDegrees: angle)]);
        foreach (var angle in new[] { 45d, 90 }) yield return ($"knee-flexion-{angle}", [new(HumanoidJointKind.LeftKnee, angle)]);
        foreach (var angle in new[] { 30d, 60, 90 }) yield return ($"shoulder-abduction-{angle}", [new(HumanoidJointKind.LeftShoulder, AbductionDegrees: angle)]);
        foreach (var angle in new[] { 45d, 90, 120 }) yield return ($"elbow-flexion-{angle}", [new(HumanoidJointKind.LeftElbow, angle)]);
    }

    private static string Sha(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private static void Export(string path, HumanoidSurface surface, IReadOnlyList<Point3D> positions, string status)
    {
        using var writer = new StreamWriter(path, false, new System.Text.UTF8Encoding(false));
        writer.WriteLine("# Antonia Polygon derived reference-rig qualification; CC-BY-3.0 Olaf Delgado-Friedrichs; no endorsement; " + status);
        foreach (var point in positions) writer.WriteLine(FormattableString.Invariant($"v {point.X:R} {point.Y:R} {point.Z:R}"));
        foreach (var face in surface.Faces) writer.WriteLine($"f {face.A + 1} {face.B + 1} {face.C + 1}");
    }
}
